using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Application.ProductAdvice;
using Loan.Application.Verification;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class ApplicantController : Controller
{
    private readonly AskProductQuestionQueryHandler _productQuestionHandler;
    private readonly IDocumentExtractor _documentExtractor;
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly EvaluateEligibilityCommandHandler _evaluateEligibilityHandler;

    public ApplicantController(
        AskProductQuestionQueryHandler productQuestionHandler,
        IDocumentExtractor documentExtractor,
        ILoanApplicationRepository applicationRepository,
        EvaluateEligibilityCommandHandler evaluateEligibilityHandler)
    {
        _productQuestionHandler = productQuestionHandler;
        _documentExtractor = documentExtractor;
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
    public async Task<IActionResult> UploadDocument(IFormFile document)
    {
        ViewData["ActiveNav"] = "Applicant";
        if (document != null && document.Length > 0)
        {
            using var stream = document.OpenReadStream();
            var fields = await _documentExtractor.ExtractFieldsAsync(stream, document.FileName, document.ContentType);
            ViewBag.ExtractedFields = fields;
            ViewBag.UploadedFileName = document.FileName;
        }

        var apps = await _applicationRepository.GetByApplicantIdAsync("APP-100");
        return View("Index", apps);
    }
}
