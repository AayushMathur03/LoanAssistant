using System.Text.Json;
using Loan.Application.Abstractions;

namespace Loan.Infrastructure.MCP;

public record McpToolDefinition(
    string Name,
    string Description,
    object InputSchema);

public record McpToolResponse(
    bool IsError,
    string ContentJson);

public class McpToolServer
{
    private readonly IIdentityReader _identityReader;
    private readonly ICreditReader _creditReader;
    private readonly IPolicyRetriever _policyRetriever;

    public McpToolServer(
        IIdentityReader identityReader,
        ICreditReader creditReader,
        IPolicyRetriever policyRetriever)
    {
        _identityReader = identityReader;
        _creditReader = creditReader;
        _policyRetriever = policyRetriever;
    }

    public IEnumerable<McpToolDefinition> GetApprovedTools()
    {
        return new List<McpToolDefinition>
        {
            new(
                Name: "get_identity_status",
                Description: "Retrieves synthetic verified identity status for an applicant synthetic ID.",
                InputSchema: new { type = "object", properties = new { syntheticId = new { type = "string" } }, required = new[] { "syntheticId" } }),

            new(
                Name: "get_credit",
                Description: "Retrieves synthetic credit score and bureau verification details.",
                InputSchema: new { type = "object", properties = new { syntheticId = new { type = "string" } }, required = new[] { "syntheticId" } }),

            new(
                Name: "search_policy",
                Description: "Searches effective versioned loan underwriting and compliance policies.",
                InputSchema: new { type = "object", properties = new { query = new { type = "string" }, productId = new { type = "string" } }, required = new[] { "query" } })
        };
    }

    public async Task<McpToolResponse> ExecuteToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            switch (toolName.ToLowerInvariant())
            {
                case "get_identity_status":
                    var syntheticId = root.GetProperty("syntheticId").GetString() ?? "";
                    var identity = await _identityReader.VerifyIdentityAsync(syntheticId, cancellationToken);
                    return new McpToolResponse(false, JsonSerializer.Serialize(identity));

                case "get_credit":
                    var creditId = root.GetProperty("syntheticId").GetString() ?? "";
                    var credit = await _creditReader.GetCreditScoreAsync(creditId, cancellationToken);
                    return new McpToolResponse(false, JsonSerializer.Serialize(credit));

                case "search_policy":
                    var query = root.GetProperty("query").GetString() ?? "";
                    string? prodId = root.TryGetProperty("productId", out var p) ? p.GetString() : null;
                    var results = await _policyRetriever.SearchPolicyAsync(query, prodId, cancellationToken: cancellationToken);
                    return new McpToolResponse(false, JsonSerializer.Serialize(results));

                default:
                    return new McpToolResponse(true, JsonSerializer.Serialize(new { error = $"Tool '{toolName}' is not approved or recognized." }));
            }
        }
        catch (Exception ex)
        {
            return new McpToolResponse(true, JsonSerializer.Serialize(new { error = ex.Message }));
        }
    }
}
