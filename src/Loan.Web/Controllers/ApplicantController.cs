using Loan.Application.Abstractions;
using Loan.Application.Documents;
using Loan.Application.DTOs;
using Loan.Application.ProductAdvice;
using Loan.Application.Verification;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class ApplicantController : Controller
{
    private readonly AskProductQuestionQueryHandler _productQuestionHandler;
    private readonly UploadAndExtractDocumentCommandHandler _uploadHandler;
    private readonly ConfirmOrOverrideExtractedFieldsCommandHandler _confirmHandler;
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly EvaluateEligibilityCommandHandler _evaluateEligibilityHandler;

    public ApplicantController(
        AskProductQuestionQueryHandler productQuestionHandler,
        UploadAndExtractDocumentCommandHandler uploadHandler,
        ConfirmOrOverrideExtractedFieldsCommandHandler confirmHandler,
        ILoanApplicationRepository applicationRepository,
        EvaluateEligibilityCommandHandler evaluateEligibilityHandler)
    {
        _productQuestionHandler = productQuestionHandler;
        _uploadHandler = uploadHandler;
        _confirmHandler = confirmHandler;
        _applicationRepository = applicationRepository;
        _evaluateEligibilityHandler = evaluateEligibilityHandler;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "Applicant";
        var apps = await _applicationRepository.GetByApplicantIdAsync("APP-100");
        return View(apps);
    }

    [HttpPost]
    public async Task<IActionResult> AskQuestion([FromForm] string question)
    {
        ViewData["ActiveNav"] = "Applicant";
        if (string.IsNullOrWhiteSpace(question))
        {
            return RedirectToAction("Index");
        }

        var response = await _productQuestionHandler.HandleAsync(new AskProductQuestionQuery(question));
        ViewBag.Question = question;
        ViewBag.AdviceResponse = response;

        var apps = await _applicationRepository.GetByApplicantIdAsync("APP-100");
        return View("Index", apps);
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument([FromForm] string applicationId, IFormFile document)
    {
        ViewData["ActiveNav"] = "Applicant";

        var targetAppId = string.IsNullOrWhiteSpace(applicationId) ? "APP-2026-001" : applicationId;

        if (document != null && document.Length > 0)
        {
            try
            {
                using var stream = document.OpenReadStream();
                var command = new UploadAndExtractDocumentCommand(targetAppId, document.FileName, document.ContentType, stream);
                var docRecord = await _uploadHandler.HandleAsync(command);

                TempData["SuccessMessage"] = $"Successfully uploaded and extracted '{document.FileName}'.";
                ViewBag.ExtractedRecord = docRecord;
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Document upload failed: {ex.Message}";
            }
        }

        var apps = await _applicationRepository.GetByApplicantIdAsync("APP-100");
        return View("Index", apps);
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmField([FromForm] string applicationId, [FromForm] string documentId, [FromForm] string fieldName, [FromForm] string confirmedValue, [FromForm] string reason)
    {
        ViewData["ActiveNav"] = "Applicant";

        var targetAppId = string.IsNullOrWhiteSpace(applicationId) ? "APP-2026-001" : applicationId;

        try
        {
            var item = new FieldConfirmationItem(documentId, fieldName, confirmedValue, reason);
            var command = new ConfirmOrOverrideExtractedFieldsCommand(
                ApplicationId: targetAppId,
                ActorId: "APP-100", // Authenticated Applicant ID
                ActorRole: "Applicant", // Authenticated Role
                FieldConfirmations: new[] { item });

            await _confirmHandler.HandleAsync(command);
            TempData["SuccessMessage"] = $"Field '{fieldName}' confirmed successfully.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Field confirmation failed: {ex.Message}";
        }

        var apps = await _applicationRepository.GetByApplicantIdAsync("APP-100");
        return View("Index", apps);
    }
}
