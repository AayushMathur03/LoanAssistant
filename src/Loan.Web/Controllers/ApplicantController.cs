using System.Text.Json;
using Loan.Application.Abstractions;
using Loan.Application.Documents;
using Loan.Application.DTOs;
using Loan.Application.ProductAdvice;
using Loan.Application.Recommendations;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

[Authorize(Roles = "Applicant,Administrator")]
public class ApplicantController : Controller
{
    private readonly AskProductQuestionQueryHandler _productQuestionHandler;
    private readonly UploadAndExtractDocumentCommandHandler _uploadHandler;
    private readonly ConfirmOrOverrideExtractedFieldsCommandHandler _confirmHandler;
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly EvaluateEligibilityCommandHandler _evaluateEligibilityHandler;
    private readonly GenerateRecommendationDraftCommandHandler? _draftHandler;
    private readonly IPolicyRetriever? _policyRetriever;
    private readonly IChatModel? _chatModel;
    private readonly UserManager<ApplicationUser>? _userManager;

    public ApplicantController(
        AskProductQuestionQueryHandler productQuestionHandler,
        UploadAndExtractDocumentCommandHandler uploadHandler,
        ConfirmOrOverrideExtractedFieldsCommandHandler confirmHandler,
        ILoanApplicationRepository applicationRepository,
        EvaluateEligibilityCommandHandler evaluateEligibilityHandler,
        GenerateRecommendationDraftCommandHandler? draftHandler = null,
        IPolicyRetriever? policyRetriever = null,
        IChatModel? chatModel = null,
        UserManager<ApplicationUser>? userManager = null)
    {
        _productQuestionHandler = productQuestionHandler;
        _uploadHandler = uploadHandler;
        _confirmHandler = confirmHandler;
        _applicationRepository = applicationRepository;
        _evaluateEligibilityHandler = evaluateEligibilityHandler;
        _draftHandler = draftHandler;
        _policyRetriever = policyRetriever;
        _chatModel = chatModel;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? tab = null, [FromQuery] string? applicationId = null)
    {
        ViewData["ActiveNav"] = "Applicant";
        var vm = await BuildDashboardViewModelAsync(applicationId);
        if (!string.IsNullOrWhiteSpace(tab))
        {
            vm.ActiveTab = tab.Equals("applications", StringComparison.OrdinalIgnoreCase) ? "applications" : "catalogue";
        }
        else if (!string.IsNullOrWhiteSpace(applicationId))
        {
            vm.ActiveTab = "applications";
        }
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> CreateApplication([FromForm] CreateApplicationInputModel input)
    {
        ViewData["ActiveNav"] = "Applicant";

        var (currentUser, applicantId) = await GetCurrentApplicantInfoAsync();
        var appId = $"APP-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var productRules = input.ProductType.Equals("AutoLoan", StringComparison.OrdinalIgnoreCase)
            ? ProductRules.CreateAutoLoan("v1.1")
            : input.ProductType.Equals("PersonalLoan", StringComparison.OrdinalIgnoreCase)
                ? ProductRules.CreatePersonalLoan("v2.0")
                : ProductRules.CreateStandardMortgage("v1.2");

        var facts = new ApplicantFacts(
            applicantId: applicantId,
            fullName: currentUser?.FullName ?? "Alice Cooper",
            syntheticId: "SYN-888777",
            monthlyGrossIncome: new Money(input.MonthlyGrossIncome),
            monthlyDebts: new Money(input.MonthlyDebts),
            requestedLoanAmount: new Money(input.RequestedAmount),
            estimatedPropertyValue: new Money(input.EstimatedPropertyValue),
            creditScore: input.CreditScore,
            employmentStatus: input.EmploymentStatus,
            loanPurpose: input.LoanPurpose);

        var newApp = new LoanApplication(appId, applicantId, productRules, facts, DateTime.UtcNow);
        newApp.Submit(DateTime.UtcNow);

        await _applicationRepository.AddAsync(newApp);
        await _evaluateEligibilityHandler.HandleAsync(new EvaluateEligibilityCommand(appId));

        if (_draftHandler != null)
        {
            try
            {
                await _draftHandler.HandleAsync(new GenerateRecommendationDraftCommand(appId));
            }
            catch { }
        }

        if (currentUser != null && _userManager != null)
        {
            currentUser.LinkedApplicationId = appId;
            await _userManager.UpdateAsync(currentUser);
        }

        SetFlashMessage("SuccessMessage", $"New {productRules.ProductName} application '{appId}' submitted successfully!");
        return RedirectToAction(nameof(Index), new { tab = "applications", applicationId = appId });
    }

    [HttpPost]
    public async Task<IActionResult> SaveDraft([FromForm] string applicationId, [FromForm] decimal requestedAmount, [FromForm] string loanPurpose)
    {
        ViewData["ActiveNav"] = "Applicant";
        var (currentUser, applicantId) = await GetCurrentApplicantInfoAsync();
        if (!IsAuthorizedForApplication(applicationId, currentUser))
        {
            return Forbid();
        }

        var app = await _applicationRepository.GetByIdAsync(applicationId);
        if (app != null)
        {
            // Update permitted applicant facts
            var currentFacts = app.Facts;
            var updatedFacts = new ApplicantFacts(
                currentFacts.ApplicantId,
                currentFacts.FullName,
                currentFacts.SyntheticId,
                currentFacts.MonthlyGrossIncome,
                currentFacts.MonthlyDebts,
                new Money(requestedAmount > 0 ? requestedAmount : currentFacts.RequestedLoanAmount.Amount),
                currentFacts.EstimatedPropertyValue,
                currentFacts.CreditScore,
                currentFacts.EmploymentStatus,
                !string.IsNullOrWhiteSpace(loanPurpose) ? loanPurpose : currentFacts.LoanPurpose);

            typeof(LoanApplication).GetProperty(nameof(LoanApplication.Facts))!.SetValue(app, updatedFacts);
            await _applicationRepository.UpdateAsync(app);
            await _evaluateEligibilityHandler.HandleAsync(new EvaluateEligibilityCommand(applicationId));

            SetFlashMessage("SuccessMessage", "Application parameters updated and eligibility re-calculated.");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> AskQuestion([FromForm] string question)
    {
        ViewData["ActiveNav"] = "Applicant";
        if (string.IsNullOrWhiteSpace(question))
        {
            return RedirectToAction(nameof(Index));
        }

        var response = await _productQuestionHandler.HandleAsync(new AskProductQuestionQuery(question));
        ViewBag.Question = question;
        ViewBag.AdviceResponse = response;

        var vm = await BuildDashboardViewModelAsync();
        return View("Index", vm);
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument([FromForm] string applicationId, IFormFile? document, [FromForm] string? sampleDocumentName)
    {
        ViewData["ActiveNav"] = "Applicant";
        var targetAppId = string.IsNullOrWhiteSpace(applicationId) ? "APP-2026-001" : applicationId;
        var (currentUser, _) = await GetCurrentApplicantInfoAsync();
        if (!IsAuthorizedForApplication(targetAppId, currentUser))
        {
            return Forbid();
        }

        Stream? stream = null;
        string? uploadFileName = null;
        string? uploadContentType = null;

        if (document != null && document.Length > 0)
        {
            stream = document.OpenReadStream();
            uploadFileName = document.FileName;
            uploadContentType = document.ContentType;
        }
        else if (!string.IsNullOrWhiteSpace(sampleDocumentName))
        {
            var samplePath = FindSampleDocumentPath(sampleDocumentName);
            if (samplePath != null && System.IO.File.Exists(samplePath))
            {
                stream = System.IO.File.OpenRead(samplePath);
                uploadFileName = Path.GetFileName(samplePath);
                uploadContentType = "text/plain";
            }
        }

        if (stream != null && uploadFileName != null)
        {
            try
            {
                using (stream)
                {
                    var command = new UploadAndExtractDocumentCommand(targetAppId, uploadFileName, uploadContentType ?? "text/plain", stream);
                    var docRecord = await _uploadHandler.HandleAsync(command);

                    // Re-evaluate eligibility after new document facts
                    await _evaluateEligibilityHandler.HandleAsync(new EvaluateEligibilityCommand(targetAppId));

                    if (_draftHandler != null)
                    {
                        try
                        {
                            await _draftHandler.HandleAsync(new GenerateRecommendationDraftCommand(targetAppId));
                        }
                        catch { }
                    }

                    SetFlashMessage("SuccessMessage", $"Document '{uploadFileName}' successfully uploaded and stored in private container. {docRecord.Fields.Count} facts extracted.");
                    ViewBag.ExtractedRecord = docRecord;
                }
            }
            catch (Exception ex)
            {
                SetFlashMessage("ErrorMessage", $"Document upload failed: {ex.Message}");
            }
        }
        else
        {
            SetFlashMessage("ErrorMessage", "Please select a file to upload or choose a synthetic demo document.");
        }

        return RedirectToAction(nameof(Index), new { tab = "applications", applicationId = targetAppId });
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmField([FromForm] string applicationId, [FromForm] string documentId, [FromForm] string fieldName, [FromForm] string confirmedValue, [FromForm] string reason)
    {
        ViewData["ActiveNav"] = "Applicant";
        var targetAppId = string.IsNullOrWhiteSpace(applicationId) ? "APP-2026-001" : applicationId;
        var (currentUser, actorId) = await GetCurrentApplicantInfoAsync();
        if (!IsAuthorizedForApplication(targetAppId, currentUser))
        {
            return Forbid();
        }

        try
        {
            var item = new FieldConfirmationItem(documentId, fieldName, confirmedValue, reason);

            var command = new ConfirmOrOverrideExtractedFieldsCommand(
                ApplicationId: targetAppId,
                ActorId: actorId,
                ActorRole: "Applicant",
                FieldConfirmations: new[] { item });

            await _confirmHandler.HandleAsync(command);
            await _evaluateEligibilityHandler.HandleAsync(new EvaluateEligibilityCommand(targetAppId));

            if (_draftHandler != null)
            {
                try
                {
                    await _draftHandler.HandleAsync(new GenerateRecommendationDraftCommand(targetAppId));
                }
                catch { }
            }

            SetFlashMessage("SuccessMessage", $"Field '{fieldName}' confirmed as '{confirmedValue}'. Precedence rules updated effective facts.");
        }
        catch (Exception ex)
        {
            SetFlashMessage("ErrorMessage", $"Field confirmation failed: {ex.Message}");
        }

        return RedirectToAction(nameof(Index), new { tab = "applications", applicationId = targetAppId });
    }

    [HttpGet]
    public async Task ChatStream([FromQuery] string question, [FromQuery] string? applicationId, CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        if (string.IsNullOrWhiteSpace(question))
        {
            await Response.WriteAsync("data: {\"error\":\"Empty question\"}\n\n", cancellationToken);
            return;
        }

        // Prompt injection guard check
        if (Loan.Application.Common.PromptInjectionGuard.IsInjectionAttempt(question))
        {
            var refusalData = JsonSerializer.Serialize(new
            {
                chunk = Loan.Application.Common.PromptInjectionGuard.RefusalMessage,
                isFinal = true,
                disclaimer = "Security Intercept: Unauthorized prompt injection instructions refused."
            });
            await Response.WriteAsync($"data: {refusalData}\n\n", cancellationToken);
            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return;
        }

        // Retrieve RAG policy context
        var citations = new List<object>();
        var effectivePolicy = "LOAN-PERSONAL v2.0 & MORTGAGE-STD v1.2";

        if (_policyRetriever != null && _chatModel != null)
        {
            var hits = (await _policyRetriever.SearchPolicyAsync(question, topK: 5, cancellationToken: cancellationToken)).ToList();
            var evidenceContext = hits.Any()
                ? string.Join("\n\n", hits.Select(h => $"[Source: {h.Title} (Version: {h.Version}, Section {h.Section})]\n{h.Content}"))
                : "No explicit policy evidence found.";

            foreach (var hit in hits.Take(3))
            {
                citations.Add(new
                {
                    documentTitle = hit.Title,
                    policyVersion = hit.Version,
                    section = hit.Section,
                    excerpt = hit.Content.Length > 160 ? hit.Content[..160] + "..." : hit.Content
                });
            }

            var messages = new List<ChatMessage>
            {
                new("system", "You are the Apex Lending AI Assistant. Explain loan guidelines clearly and concisely based ONLY on the provided policy evidence. Never issue binding approval decisions. Always cite the relevant policy section."),
                new("user", $"Policy Evidence:\n{evidenceContext}\n\nApplicant Question:\n{question}")
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
            // Fallback response
            var fallback = await _productQuestionHandler.HandleAsync(new AskProductQuestionQuery(question), cancellationToken);
            var payload = JsonSerializer.Serialize(new { chunk = fallback.Answer });
            await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        // Final payload with citations and disclaimer
        var finalPayload = JsonSerializer.Serialize(new
        {
            isFinal = true,
            citations,
            effectivePolicy,
            disclaimer = "Informational Only: Explanations do not constitute a credit approval or binding commitment per Fair Lending and TRID regulations."
        });

        await Response.WriteAsync($"data: {finalPayload}\n\n", cancellationToken);
        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task<ApplicantDashboardViewModel> BuildDashboardViewModelAsync(string? requestedAppId = null)
    {
        var (currentUser, applicantId) = await GetCurrentApplicantInfoAsync();

        var allApps = (await _applicationRepository.GetByApplicantIdAsync(applicantId)).ToList();
        if (!allApps.Any())
        {
            var defaultApp = await _applicationRepository.GetByIdAsync(currentUser?.LinkedApplicationId ?? "APP-2026-001");
            if (defaultApp != null)
            {
                allApps.Add(defaultApp);
            }
        }

        LoanApplication? activeApp = null;
        if (!string.IsNullOrWhiteSpace(requestedAppId))
        {
            activeApp = allApps.FirstOrDefault(a => a.ApplicationId.Equals(requestedAppId, StringComparison.OrdinalIgnoreCase))
                ?? await _applicationRepository.GetByIdAsync(requestedAppId);
            if (activeApp != null && !allApps.Any(a => a.ApplicationId == activeApp.ApplicationId))
            {
                allApps.Add(activeApp);
            }
        }

        if (activeApp == null && currentUser?.LinkedApplicationId != null)
        {
            activeApp = allApps.FirstOrDefault(a => a.ApplicationId.Equals(currentUser.LinkedApplicationId, StringComparison.OrdinalIgnoreCase));
        }

        activeApp ??= allApps.FirstOrDefault();

        return new ApplicantDashboardViewModel
        {
            CurrentUserName = currentUser?.FullName ?? User?.Identity?.Name ?? "Alice Cooper",
            CurrentUserEmail = currentUser?.Email ?? "applicant@apex.local",
            ApplicantId = applicantId,
            ActiveApplication = activeApp,
            AllApplications = allApps
        };
    }

    private bool IsAuthorizedForApplication(string applicationId, ApplicationUser? currentUser)
    {
        if (User?.IsInRole("Administrator") == true) return true;
        if (currentUser?.LinkedApplicationId != null &&
            !string.Equals(currentUser.LinkedApplicationId, applicationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    private async Task<(ApplicationUser? User, string ApplicantId)> GetCurrentApplicantInfoAsync()
    {
        if (_userManager != null && User?.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var appRole = await _userManager.IsInRoleAsync(user, "Applicant");
                var applicantId = user.LinkedApplicationId != null ? "APP-100" : $"APP-{user.Id[..6].ToUpperInvariant()}";
                return (user, applicantId);
            }
        }

        // Check ClaimsPrincipal directly for LinkedApplicationId (e.g. testing or lightweight auth)
        if (User?.Identity?.IsAuthenticated == true)
        {
            var linkedApp = User.FindFirst("LinkedApplicationId")?.Value;
            var syntheticUser = new ApplicationUser
            {
                UserName = User.Identity.Name ?? "applicant@apex.local",
                Email = User.Identity.Name ?? "applicant@apex.local",
                FullName = User.Identity.Name ?? "Alice Cooper",
                LinkedApplicationId = linkedApp
            };
            return (syntheticUser, linkedApp != null ? "APP-100" : "APP-100");
        }

        return (null, "APP-100");
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

    private string? FindSampleDocumentPath(string fileName)
    {
        var sanitized = Path.GetFileName(fileName);
        string[] candidateDirs = [
            Path.Combine(Directory.GetCurrentDirectory(), "SampleDocuments"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Loan.Web", "SampleDocuments"),
            Path.Combine(AppContext.BaseDirectory, "SampleDocuments"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "SampleDocuments"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "src", "Loan.Web", "SampleDocuments")
        ];

        foreach (var dir in candidateDirs)
        {
            var fullPath = Path.Combine(dir, sanitized);
            if (System.IO.File.Exists(fullPath)) return fullPath;
        }

        return null;
    }
}
