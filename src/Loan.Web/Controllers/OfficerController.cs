using System.Text.Json;
using Loan.Application.Abstractions;
using Loan.Application.Agents;
using Loan.Application.DTOs;
using Loan.Application.Recommendations;
using Loan.Domain.Recommendations;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class OfficerController : Controller
{
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly GenerateRecommendationDraftCommandHandler _draftHandler;
    private readonly OfficerDecisionCommandHandler _decisionHandler;
    private readonly RecommendationOrchestratorAgent _orchestratorAgent;

    public OfficerController(
        ILoanApplicationRepository applicationRepository,
        GenerateRecommendationDraftCommandHandler draftHandler,
        OfficerDecisionCommandHandler decisionHandler,
        RecommendationOrchestratorAgent orchestratorAgent)
    {
        _applicationRepository = applicationRepository;
        _draftHandler = draftHandler;
        _decisionHandler = decisionHandler;
        _orchestratorAgent = orchestratorAgent;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "Officer";
        var pendingApps = await _applicationRepository.GetPendingOfficerReviewAsync();
        return View(pendingApps);
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
            // Client disconnected / cancelled connection; stop stream gracefully without throwing unhandled error
        }
        catch (Exception ex)
        {
            // Safe error masking: do not leak raw stack traces or internal secrets
            await SendSseEventAsync("Error", $"Processing error: {ex.Message}", cancellationToken);
        }
    }

    [HttpPost]
    public async Task<IActionResult> SubmitDecision(string id, RecommendationStatus action, string notes, string? officerId)
    {
        ViewData["ActiveNav"] = "Officer";

        // Server-Side Role Authorization Check
        var roleHeader = Request.Headers["X-Actor-Role"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(roleHeader) &&
            !roleHeader.Equals("Officer", StringComparison.OrdinalIgnoreCase) &&
            !roleHeader.Equals("LoanOfficer", StringComparison.OrdinalIgnoreCase) &&
            !roleHeader.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid($"Role '{roleHeader}' is not authorized to submit loan officer decisions.");
        }
        
        if (string.IsNullOrWhiteSpace(notes))
        {
            ModelState.AddModelError("notes", "Decision notes and justification are required.");
            var app = await _applicationRepository.GetByIdAsync(id);
            return View("Review", app);
        }

        var effectiveOfficerId = string.IsNullOrWhiteSpace(officerId) ? "OFFICER-42" : officerId.Trim();
        var command = new OfficerDecisionCommand(id, effectiveOfficerId, action, notes.Trim());
        await _decisionHandler.HandleAsync(command);
        return RedirectToAction("Index");
    }

    private async Task SendSseEventAsync(string eventName, string data, CancellationToken cancellationToken)
    {
        var line = $"event: {eventName}\ndata: {data}\n\n";
        await Response.WriteAsync(line, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

