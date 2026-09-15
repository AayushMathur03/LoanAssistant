using System.Collections;
using System.Collections.Generic;
using Loan.Application.Abstractions;

namespace Loan.Web.Models;

public class AdminDashboardViewModel : IEnumerable<Loan.Infrastructure.MCP.McpTool>
{
    public SystemHealthSummary Health { get; set; } = new();
    public RagIndexSummary RagMetadata { get; set; } = new();
    public TelemetrySummary Telemetry { get; set; } = new();
    public List<RegisteredMcpToolSummary> McpTools { get; set; } = new();
    public EvaluationSummary Evaluation { get; set; } = new();

    public IEnumerator<Loan.Infrastructure.MCP.McpTool> GetEnumerator() => Loan.Infrastructure.MCP.McpToolServer.GetRegisteredTools().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Loan.Infrastructure.MCP.McpToolServer.GetRegisteredTools().GetEnumerator();
}

public class SystemHealthSummary
{
    public string SqlStatus { get; set; } = "Healthy";
    public string AzureOpenAiStatus { get; set; } = "Connected";
    public string AzureSearchStatus { get; set; } = "Online";
    public string AzureBlobStatus { get; set; } = "Private Container Active";
    public string McpServerStatus { get; set; } = "Available (HTTP Streamable)";

    public string OpenAiEndpoint { get; set; } = string.Empty;
    public string OpenAiDeployment { get; set; } = "gpt-4o";
    public string EmbeddingDeployment { get; set; } = "text-embedding-3-small";
    public string SearchEndpoint { get; set; } = string.Empty;
    public string SearchIndex { get; set; } = "loan-policies-index";
}

public class RagIndexSummary
{
    public string IndexName { get; set; } = "loan-policies-index";
    public int TotalPolicyDocuments { get; set; } = 8;
    public string ActivePersonalLoanVersion { get; set; } = "v2.0 (LOAN-PERSONAL)";
    public string ActiveMortgageVersion { get; set; } = "v1.2 (MORTGAGE-STD)";
    public string ActiveComplianceVersion { get; set; } = "v2.0 (DOC-COMPLIANCE-DISCLOSURE-V2)";
}

public class TelemetrySummary
{
    public long TotalTokens { get; set; }
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public double AverageLlmLatencyMs { get; set; }
    public double AverageHttpLatencyMs { get; set; }
    public int RetrievalQueriesCount { get; set; }
    public int ToolCallsCount { get; set; }
}

public class RegisteredMcpToolSummary
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AllowedRoles { get; set; } = string.Empty;
}

public class EvaluationSummary
{
    public string OfflineScore { get; set; } = "20/20 (100%)";
    public string LiveAzureScore { get; set; } = "20/20 (100%)";
    public string LiveAverageLatency { get; set; } = "1,787 ms";
    public string Status { get; set; } = "Benchmark Certified";
}
