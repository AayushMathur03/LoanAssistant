using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Applicant");
    }
}
