using Loan.Infrastructure.MCP;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class AdminController : Controller
{
    private readonly McpToolServer _mcpToolServer;

    public AdminController(McpToolServer mcpToolServer)
    {
        _mcpToolServer = mcpToolServer;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "Admin";
        var tools = _mcpToolServer.GetApprovedTools();
        return View(tools);
    }
}
