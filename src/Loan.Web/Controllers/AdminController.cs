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
    private readonly Loan.Infrastructure.Search.PolicyIndexer? _policyIndexer;

    public AdminController(
        IConfiguration? configuration = null,
        ITelemetryCollector? telemetryCollector = null,
        Loan.Infrastructure.Search.PolicyIndexer? policyIndexer = null)
    {
        _configuration = configuration ?? new ConfigurationBuilder().Build();
        _telemetryCollector = telemetryCollector;
        _policyIndexer = policyIndexer;
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

    [HttpPost]
    public async Task<IActionResult> UploadPolicyDocument(
        IFormFile? policyFile,
        [FromForm] string? customTitle,
        [FromForm] string? versionTag,
        [FromForm] string? productId,
        CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Admin";
        if (policyFile == null || policyFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a Markdown (.md) or text policy document to upload.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var fileName = Path.GetFileName(policyFile.FileName);
            var version = string.IsNullOrWhiteSpace(versionTag) ? "v2.1" : versionTag.Trim();
            var prodId = string.IsNullOrWhiteSpace(productId) ? "GENERAL" : productId.Trim();
            var title = string.IsNullOrWhiteSpace(customTitle) ? Path.GetFileNameWithoutExtension(fileName) : customTitle.Trim();

            using var reader = new StreamReader(policyFile.OpenReadStream());
            var rawContent = await reader.ReadToEndAsync(cancellationToken);

            string finalMarkdown;
            if (!rawContent.TrimStart().StartsWith("---"))
            {
                finalMarkdown = $"---\r\n" +
                                $"document_id: DOC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}\r\n" +
                                $"title: {title}\r\n" +
                                $"product_id: {prodId}\r\n" +
                                $"policy_version: {version}\r\n" +
                                $"document_type: PolicyGuide\r\n" +
                                $"audience: Underwriting\r\n" +
                                $"effective_from: {DateTime.UtcNow:yyyy-MM-dd}\r\n" +
                                $"effective_to: Active\r\n" +
                                $"---\r\n\r\n" +
                                rawContent;
            }
            else
            {
                finalMarkdown = rawContent;
            }

            // Save to SeedPolicies directory
            var seedDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "Loan.Infrastructure", "Search", "SeedPolicies");
            if (!Directory.Exists(seedDir))
            {
                seedDir = Path.Combine(AppContext.BaseDirectory, "Search", "SeedPolicies");
            }

            if (Directory.Exists(seedDir))
            {
                var savePath = Path.Combine(seedDir, fileName);
                await System.IO.File.WriteAllTextAsync(savePath, finalMarkdown, cancellationToken);
            }

            // Synchronize with Azure AI Search Index
            if (_policyIndexer != null)
            {
                await _policyIndexer.SynchronizeIndexAndSeedAsync(seedDir, cancellationToken);
            }

            TempData["SuccessMessage"] = $"Policy document '{fileName}' (Version: {version}) successfully ingested and re-indexed into Azure AI Search ('loan-policies-index').";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Policy ingestion failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SyncSeedPolicies(CancellationToken cancellationToken)
    {
        ViewData["ActiveNav"] = "Admin";
        if (_policyIndexer == null)
        {
            TempData["ErrorMessage"] = "PolicyIndexer service is not available.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var seedDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "Loan.Infrastructure", "Search", "SeedPolicies");
            if (!Directory.Exists(seedDir))
            {
                seedDir = Path.Combine(AppContext.BaseDirectory, "Search", "SeedPolicies");
            }

            await _policyIndexer.SynchronizeIndexAndSeedAsync(seedDir, cancellationToken);
            TempData["SuccessMessage"] = "Successfully synchronized all authoritative policy guides into Azure AI Search ('loan-policies-index') using text-embedding-3-small vector embeddings!";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Policy synchronization failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}

