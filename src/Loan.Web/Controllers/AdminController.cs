using Loan.Infrastructure.MCP;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class AdminController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "Admin";
        var tools = McpToolServer.GetRegisteredTools();
        return View(tools);
    }
}
