using Loan.Application.Abstractions;
using Loan.Infrastructure.Persistence.DbContext;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Loan.Web.Controllers;

public record HealthStatusResponse(
    string Status,
    string Type,
    DateTime Timestamp,
    string? Database = null,
    string? Error = null,
    IDictionary<string, DependencyHealth>? Dependencies = null);

public record DependencyHealth(
    string Status,
    string State,
    string? Details = null);

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly LoanDbContext _dbContext;
    private readonly IChatModel _chatModel;
    private readonly IPolicyRetriever _policyRetriever;
    private readonly IConfiguration _configuration;

    public HealthController(
        LoanDbContext dbContext,
        IChatModel chatModel,
        IPolicyRetriever policyRetriever,
        IConfiguration configuration)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _chatModel = chatModel ?? throw new ArgumentNullException(nameof(chatModel));
        _policyRetriever = policyRetriever ?? throw new ArgumentNullException(nameof(policyRetriever));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// GET /health
    /// Process liveness check - fast, non-blocking, zero external calls.
    /// </summary>
    [HttpGet]
    public IActionResult GetLiveness()
    {
        return Ok(new HealthStatusResponse(
            Status: "Healthy",
            Type: "Liveness",
            Timestamp: DateTime.UtcNow));
    }

    /// <summary>
    /// GET /health/ready
    /// Core application readiness check - tests SQL database connectivity with a 3-second bounded timeout.
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cts.Token);
            if (canConnect)
            {
                return Ok(new HealthStatusResponse(
                    Status: "Healthy",
                    Type: "Readiness",
                    Database: "Connected",
                    Timestamp: DateTime.UtcNow));
            }

            return StatusCode(503, new HealthStatusResponse(
                Status: "Unhealthy",
                Type: "Readiness",
                Database: "Unavailable",
                Timestamp: DateTime.UtcNow));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(503, new HealthStatusResponse(
                Status: "Unhealthy",
                Type: "Readiness",
                Database: "Timeout",
                Timestamp: DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            return StatusCode(503, new HealthStatusResponse(
                Status: "Unhealthy",
                Type: "Readiness",
                Database: "Error",
                Error: ex.Message,
                Timestamp: DateTime.UtcNow));
        }
    }

    /// <summary>
    /// GET /health/details
    /// Exposes detailed dependency status for SQL Server, Azure OpenAI, and Azure AI Search.
    /// AI dependency failures mark overall status as "Degraded" (HTTP 200), not "Unhealthy".
    /// </summary>
    [HttpGet("details")]
    public async Task<IActionResult> GetDetails(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        var dependencies = new Dictionary<string, DependencyHealth>();
        var isDbHealthy = false;
        var hasAiDegradation = false;

        // 1. SQL Server Database Check
        try
        {
            var dbConnected = await _dbContext.Database.CanConnectAsync(cts.Token);
            isDbHealthy = dbConnected;
            dependencies["database"] = new DependencyHealth(
                Status: dbConnected ? "Healthy" : "Unhealthy",
                State: dbConnected ? "Connected" : "Unavailable");
        }
        catch (Exception ex)
        {
            dependencies["database"] = new DependencyHealth(
                Status: "Unhealthy",
                State: "Error",
                Details: ex.Message);
        }

        // 2. Azure OpenAI Dependency Check
        try
        {
            var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
            var openAiKey = _configuration["AzureOpenAI:ApiKey"];
            var isConfigured = !string.IsNullOrWhiteSpace(openAiEndpoint) &&
                              !openAiEndpoint.Contains("YOUR-RESOURCE-NAME", StringComparison.OrdinalIgnoreCase) &&
                              !string.IsNullOrWhiteSpace(openAiKey);

            if (isConfigured)
            {
                dependencies["azureOpenAi"] = new DependencyHealth(
                    Status: "Healthy",
                    State: "Configured");
            }
            else
            {
                dependencies["azureOpenAi"] = new DependencyHealth(
                    Status: "Degraded",
                    State: "NotConfigured",
                    Details: "Azure OpenAI endpoint or API key missing in environment configuration.");
                hasAiDegradation = true;
            }
        }
        catch (Exception ex)
        {
            dependencies["azureOpenAi"] = new DependencyHealth(
                Status: "Degraded",
                State: "Error",
                Details: ex.Message);
            hasAiDegradation = true;
        }

        // 3. Azure AI Search Dependency Check
        try
        {
            var searchEndpoint = _configuration["AzureAISearch:Endpoint"];
            var searchKey = _configuration["AzureAISearch:ApiKey"];
            var isConfigured = !string.IsNullOrWhiteSpace(searchEndpoint) &&
                              !searchEndpoint.Contains("YOUR-RESOURCE-NAME", StringComparison.OrdinalIgnoreCase) &&
                              !string.IsNullOrWhiteSpace(searchKey);

            if (isConfigured)
            {
                dependencies["azureAiSearch"] = new DependencyHealth(
                    Status: "Healthy",
                    State: "Configured");
            }
            else
            {
                dependencies["azureAiSearch"] = new DependencyHealth(
                    Status: "Degraded",
                    State: "NotConfigured",
                    Details: "Azure AI Search endpoint or API key missing in environment configuration.");
                hasAiDegradation = true;
            }
        }
        catch (Exception ex)
        {
            dependencies["azureAiSearch"] = new DependencyHealth(
                Status: "Degraded",
                State: "Error",
                Details: ex.Message);
            hasAiDegradation = true;
        }

        var overallStatus = !isDbHealthy
            ? "Unhealthy"
            : (hasAiDegradation ? "Degraded" : "Healthy");

        var statusCode = isDbHealthy ? 200 : 503;

        return StatusCode(statusCode, new HealthStatusResponse(
            Status: overallStatus,
            Type: "DetailedDependencies",
            Timestamp: DateTime.UtcNow,
            Dependencies: dependencies));
    }
}
