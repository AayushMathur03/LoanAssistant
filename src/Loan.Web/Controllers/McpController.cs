using System.Security.Claims;
using Loan.Infrastructure.MCP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Loan.Web.Controllers;

[ApiController]
[Route("api/mcp")]
public class McpController : ControllerBase
{
    private readonly McpToolServer _mcpServer;
    private readonly IHostEnvironment _env;

    public McpController(McpToolServer mcpServer, IHostEnvironment env)
    {
        _mcpServer = mcpServer;
        _env = env;
    }

    [HttpPost]
    public async Task<IActionResult> HandleMcpRequest([FromBody] JsonRpcRequest request, CancellationToken cancellationToken)
    {
        string? actorId = null;
        string? actorRole = null;

        // 1. Trust ASP.NET Core authenticated HttpContext User Claims in priority
        if (User.Identity?.IsAuthenticated == true)
        {
            actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity.Name;
            actorRole = User.FindFirst(ClaimTypes.Role)?.Value;
        }

        // 2. Development / Test transport isolation:
        // Client headers X-Actor-Id / X-Actor-Role are ONLY extracted in Development environment or when test key header is present.
        if (string.IsNullOrWhiteSpace(actorRole) && (_env.IsDevelopment() || Request.Headers.ContainsKey("X-MCP-Test-Key")))
        {
            actorId ??= Request.Headers["X-Actor-Id"].FirstOrDefault() ?? Request.Headers["Actor-Id"].FirstOrDefault();
            actorRole = Request.Headers["X-Actor-Role"].FirstOrDefault() ?? Request.Headers["Actor-Role"].FirstOrDefault();
        }

        // Default fallback: SystemWorker (cannot perform loan officer final approvals/rejections)
        actorId ??= "SystemWorker";
        actorRole ??= "SystemWorker";

        var response = await _mcpServer.HandleRequestAsync(request, actorId, actorRole, cancellationToken);
        return Ok(response);
    }
}
