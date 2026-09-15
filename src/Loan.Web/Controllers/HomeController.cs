using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Administrator")) return RedirectToAction("Index", "Admin");
            if (User.IsInRole("LoanOfficer")) return RedirectToAction("Index", "Officer");
            if (User.IsInRole("ComplianceReviewer")) return RedirectToAction("Index", "Compliance");
            return RedirectToAction("Index", "Applicant");
        }

        return RedirectToAction("Login", "Account");
    }

    public IActionResult Error()
    {
        return View(new Models.ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }
}
