using Loan.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class ComplianceController : Controller
{
    private readonly ILoanApplicationRepository _applicationRepository;

    public ComplianceController(ILoanApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "Compliance";
        var apps = await _applicationRepository.GetByApplicantIdAsync("APP-100");
        return View(apps);
    }
}
