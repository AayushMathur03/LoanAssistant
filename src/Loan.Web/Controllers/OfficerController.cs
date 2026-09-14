using Loan.Application.Abstractions;
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

    public OfficerController(
        ILoanApplicationRepository applicationRepository,
        GenerateRecommendationDraftCommandHandler draftHandler,
        OfficerDecisionCommandHandler decisionHandler)
    {
        _applicationRepository = applicationRepository;
        _draftHandler = draftHandler;
        _decisionHandler = decisionHandler;
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
            await _draftHandler.HandleAsync(new GenerateRecommendationDraftCommand(id));
            app = await _applicationRepository.GetByIdAsync(id);
        }

        return View(app);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitDecision(string id, RecommendationStatus action, string notes)
    {
        ViewData["ActiveNav"] = "Officer";
        var command = new OfficerDecisionCommand(id, "OFFICER-42", action, notes ?? "Decision submitted by officer.");
        await _decisionHandler.HandleAsync(command);
        return RedirectToAction("Index");
    }
}
