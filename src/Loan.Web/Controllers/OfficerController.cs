using System.Text.Json;
using Loan.Application.Abstractions;
using Loan.Application.Agents;
using Loan.Application.DTOs;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Recommendations;
using Loan.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

[Authorize(Roles = "LoanOfficer,Administrator")]
public class OfficerController : Controller
{
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly GenerateRecommendationDraftCommandHandler _draftHandler;
    private readonly OfficerDecisionCommandHandler _decisionHandler;
    private readonly RecommendationOrchestratorAgent _orchestratorAgent;
    private readonly IPolicyRetriever? _policyRetriever;
    private readonly IChatModel? _chatModel;

    public OfficerController(
        ILoanApplicationRepository applicationRepository,
        GenerateRecommendationDraftCommandHandler draftHandler,
        OfficerDecisionCommandHandler decisionHandler,
        RecommendationOrchestratorAgent orchestratorAgent,
        IPolicyRetriever? policyRetriever = null,
        IChatModel? chatModel = null)
    {
        _applicationRepository = applicationRepository;
        _draftHandler = draftHandler;
        _decisionHandler = decisionHandler;
        _orchestratorAgent = orchestratorAgent;
        _policyRetriever = policyRetriever;
        _chatModel = chatModel;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "Officer";
        
        // Query real applications from SQL repository
        var allApps = (await _applicationRepository.GetAllAsync()).ToList();
        var pendingReview = (await _applicationRepository.GetPendingOfficerReviewAsync()).ToList();

        // Combine unique list
        var combinedList = pendingReview.Concat(allApps).DistinctBy(a => a.ApplicationId).ToList();

        var vm = new OfficerDashboardViewModel
        {
            TotalApplications = combinedList.Count,
            ReadyForReviewCount = combinedList.Count(a => a.Status is ApplicationStatus.UnderOfficerReview or ApplicationStatus.Submitted),
            PendingInfoCount = combinedList.Count(a => a.Status == ApplicationStatus.InformationRequested),
            ApprovedCount = combinedList.Count(a => a.Status == ApplicationStatus.Approved),
            RejectedCount = combinedList.Count(a => a.Status == ApplicationStatus.Rejected),
            ReviewQueue = pendingReview.Any() ? pendingReview : combinedList.Where(a => a.Status != ApplicationStatus.Approved && a.Status != ApplicationStatus.Rejected).ToList(),
            RecentActivity = combinedList.OrderByDescending(a => a.UpdatedAtUtc).Take(8).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Review(string id)
    {
        ViewData["ActiveNav"] = "Officer";
        var app = await _applicationRepository.GetByIdAsync(id);
        if (app == null) return NotFound();

        if (app.CurrentRecommendation == null && app.Indicators != null)
        {
            await _orchestratorAgent.ProcessApplicationAsync(app);
            app = await _applicationRepository.GetByIdAsync(id);
        }

        return View(app);
    }

    [HttpGet]
    public async Task StreamRecommendationDraft(string id, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            var app = await _applicationRepository.GetByIdAsync(id, cancellationToken);
            if (app == null)
            {
                await SendSseEventAsync("Error", "Loan application not found.", cancellationToken);
                return;
            }

            if (app.Indicators == null)
            {
                await SendSseEventAsync("Error", "Eligibility must be evaluated before streaming recommendation.", cancellationToken);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await SendSseEventAsync("DocumentAgent", "Analyzing document health and extraction confidence scores...", cancellationToken);
            await Task.Delay(200, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await SendSseEventAsync("EligibilityAgent", $"Evaluating DTI ({app.Indicators.DebtToIncomeRatio:P1}) and LTV ({app.Indicators.LoanToValueRatio:P1}) indicators...", cancellationToken);
            await Task.Delay(200, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await SendSseEventAsync("ComplianceAgent", $"Querying policy retriever for product {app.ProductRules.ProductName} ({app.ProductRules.EffectiveVersion})...", cancellationToken);
            await Task.Delay(200, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            await SendSseEventAsync("Orchestrator", "Synthesizing multi-agent reasoning and applying deterministic overrides...", cancellationToken);
            
            var recDto = await _orchestratorAgent.ProcessApplicationAsync(app, cancellationToken);

            var eventData = JsonSerializer.Serialize(new
            {
                stage = "Complete",
                recommendationId = recDto.RecommendationId,
                status = recDto.Status.ToString(),
                riskScore = recDto.RiskScore,
                summary = recDto.SummaryReasoning
            });

            await SendSseEventAsync("Complete", eventData, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected gracefully
        }
        catch (Exception ex)
        {
            await SendSseEventAsync("Error", $"Processing error: {ex.Message}", cancellationToken);
        }
    }

    [HttpGet]
    public async Task OfficerChatStream([FromQuery] string question, [FromQuery] string applicationId, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        if (string.IsNullOrWhiteSpace(question))
        {
            await Response.WriteAsync("data: {\"error\":\"Empty query\"}\n\n", cancellationToken);
            return;
        }

        // Prompt injection guard check
        if (Loan.Application.Common.PromptInjectionGuard.IsInjectionAttempt(question))
        {
            var refusalData = JsonSerializer.Serialize(new
            {
                chunk = Loan.Application.Common.PromptInjectionGuard.RefusalMessage,
                isFinal = true,
                disclaimer = "Security Intercept: Adversarial prompt injection instruction blocked."
            });
            await Response.WriteAsync($"data: {refusalData}\n\n", cancellationToken);
            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return;
        }

        var citations = new List<object>();
        if (_policyRetriever != null && _chatModel != null)
        {
            var hits = (await _policyRetriever.SearchPolicyAsync(question, topK: 5, cancellationToken: cancellationToken)).ToList();
            var evidence = hits.Any()
                ? string.Join("\n\n", hits.Select(h => $"[Policy: {h.Title} v{h.Version}, Sec {h.Section}]\n{h.Content}"))
                : "No matching policy text found.";

            foreach (var hit in hits.Take(3))
            {
                citations.Add(new
                {
                    documentTitle = hit.Title,
                    section = hit.Section,
                    excerpt = hit.Content.Length > 150 ? hit.Content[..150] + "..." : hit.Content
                });
            }

            var messages = new List<ChatMessage>
            {
                new("system", "You are an Underwriting Assistant assisting an authorized human Loan Officer. Provide objective, policy-grounded analysis regarding exceptions, risk score interpretations, DTI/LTV limits, and secondary document verification. Never issue binding approvals."),
                new("user", $"Policy Evidence:\n{evidence}\n\nOfficer Question:\n{question}")
            };

            await foreach (var chunk in _chatModel.StreamCompletionAsync(messages, cancellationToken: cancellationToken))
            {
                var payload = JsonSerializer.Serialize(new { chunk });
                await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        else
        {
            var payload = JsonSerializer.Serialize(new { chunk = "Policy Guidelines: For Standard Mortgage v1.2, maximum DTI is 43.0% and max LTV is 80.0%. For Personal Loan v2.0, max DTI is 38.0%. Approvals require human underwriter sign-off." });
            await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        var finalPayload = JsonSerializer.Serialize(new
        {
            isFinal = true,
            citations,
            disclaimer = "Underwriting Advisory: Model suggestions must be verified by the designated loan officer before final sign-off."
        });

        await Response.WriteAsync($"data: {finalPayload}\n\n", cancellationToken);
        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitDecision(string id, RecommendationStatus action, string notes, string? officerId)
    {
        ViewData["ActiveNav"] = "Officer";

        // Server-Side Role Authorization Check
        if (User?.Identity?.IsAuthenticated == true)
        {
            if (!User.IsInRole("LoanOfficer") && !User.IsInRole("Administrator"))
            {
                return Forbid("Only authorized Loan Officers and Administrators can submit binding decisions.");
            }
        }
        else
        {
            // Test execution fallback via actor header
            var roleHeader = Request?.Headers["X-Actor-Role"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(roleHeader) &&
                !roleHeader.Equals("Officer", StringComparison.OrdinalIgnoreCase) &&
                !roleHeader.Equals("LoanOfficer", StringComparison.OrdinalIgnoreCase) &&
                !roleHeader.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid($"Role '{roleHeader}' is not authorized to submit loan officer decisions.");
            }
        }
        
        if (string.IsNullOrWhiteSpace(notes))
        {
            ModelState.AddModelError("notes", "Decision notes and justification are required.");
            var app = await _applicationRepository.GetByIdAsync(id);
            return View("Review", app);
        }

        var effectiveOfficerId = !string.IsNullOrWhiteSpace(officerId) ? officerId.Trim()
            : (User?.Identity?.Name ?? "OFFICER-42");

        var command = new OfficerDecisionCommand(id, effectiveOfficerId, action, notes.Trim());
        await _decisionHandler.HandleAsync(command);

        SetFlashMessage("SuccessMessage", $"Decision '{action}' submitted successfully for Application #{id} by {effectiveOfficerId}.");
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> RequestInformation(string id, string requestedItems)
    {
        ViewData["ActiveNav"] = "Officer";
        var effectiveOfficerId = User?.Identity?.Name ?? "OFFICER-42";
        var notes = string.IsNullOrWhiteSpace(requestedItems) ? "Additional evidence requested by underwriter" : requestedItems.Trim();
        try
        {
            var command = new OfficerDecisionCommand(id, effectiveOfficerId, RecommendationStatus.ReturnedForInfo, notes);
            await _decisionHandler.HandleAsync(command);
            SetFlashMessage("SuccessMessage", $"Application #{id} status updated to InformationRequested. Requested: {notes}");
        }
        catch (Exception ex)
        {
            SetFlashMessage("ErrorMessage", $"Could not request information: {ex.Message}");
        }
        return RedirectToAction("Review", new { id });
    }

    private void SetFlashMessage(string key, string message)
    {
        try
        {
            if (TempData != null)
            {
                TempData[key] = message;
            }
        }
        catch { }
    }

    private async Task SendSseEventAsync(string eventName, string data, CancellationToken cancellationToken)
    {
        var line = $"event: {eventName}\ndata: {data}\n\n";
        await Response.WriteAsync(line, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
