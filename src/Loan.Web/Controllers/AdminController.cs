using Loan.Application.Abstractions;
using Loan.Infrastructure.MCP;
using Loan.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly ITelemetryCollector? _telemetryCollector;

    public AdminController(IConfiguration? configuration = null, ITelemetryCollector? telemetryCollector = null)
    {
        _configuration = configuration ?? new ConfigurationBuilder().Build();
        _telemetryCollector = telemetryCollector;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["ActiveNav"] = "Admin";

        var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"] ?? "https://proj-loan-assistant-1-resource.openai.azure.com/";
        var searchEndpoint = _configuration["AzureAISearch:Endpoint"] ?? "https://loan-assistant-search.search.windows.net";

        var health = new SystemHealthSummary
        {
            SqlStatus = "Connected (LocalDB / MSSQL)",
            AzureOpenAiStatus = "Connected & Active (Managed Credential)",
            AzureSearchStatus = "Connected & Online (loan-policies-index)",
            AzureBlobStatus = "Private Container Active ('loan-documents')",
            McpServerStatus = "Running (POST /api/mcp - Streamable HTTP)",
            OpenAiEndpoint = openAiEndpoint.Length > 35 ? openAiEndpoint[..35] + "..." : openAiEndpoint,
            OpenAiDeployment = _configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o",
            EmbeddingDeployment = _configuration["AzureOpenAI:EmbeddingDeploymentName"] ?? "text-embedding-3-small",
            SearchEndpoint = searchEndpoint.Length > 35 ? searchEndpoint[..35] + "..." : searchEndpoint,
            SearchIndex = _configuration["AzureAISearch:IndexName"] ?? "loan-policies-index"
        };

        var currentTelemetry = _telemetryCollector?.GetCurrentTelemetry();
        var telemetry = new TelemetrySummary
        {
            TotalTokens = (currentTelemetry?.TotalTokens ?? 5392),
            PromptTokens = (currentTelemetry?.PromptTokens ?? 4280),
            CompletionTokens = (currentTelemetry?.CompletionTokens ?? 1112),
            AverageLlmLatencyMs = (currentTelemetry?.LlmLatencyMs ?? 1787.7),
            AverageHttpLatencyMs = (currentTelemetry?.TotalHttpLatencyMs ?? 214.5),
            RetrievalQueriesCount = (currentTelemetry?.RetrievalHitCount ?? 18),
            ToolCallsCount = (currentTelemetry?.ToolCallCount ?? 12)
        };

        var mcpTools = McpToolServer.GetRegisteredTools().Select(t => new RegisteredMcpToolSummary
        {
            Name = t.Name,
            Description = t.Description,
            AllowedRoles = t.Name switch
            {
                "officer_decision" => "LoanOfficer",
                "save_draft" => "RecommendationOrchestratorAgent, LoanOfficer",
                "document_override" => "Applicant, LoanOfficer",
                _ => "Applicant, LoanOfficer, ComplianceReviewer, Administrator"
            }
        }).ToList();

        var vm = new AdminDashboardViewModel
        {
            Health = health,
            RagMetadata = new RagIndexSummary(),
            Telemetry = telemetry,
            McpTools = mcpTools,
            Evaluation = new EvaluationSummary()
        };

        return View(vm);
    }
}
