using System.Text.Json;
using Loan.Application.Abstractions;
using Loan.Application.Recommendations;

namespace Loan.Infrastructure.MCP;

public class McpToolServer
{
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly IIdentityReader _identityReader;
    private readonly IIncomeReader _incomeReader;
    private readonly ICreditReader _creditReader;
    private readonly IPolicyRetriever _policyRetriever;
    private readonly SaveRecommendationDraftCommandHandler _saveDraftHandler;
    private readonly ITelemetryCollector? _telemetryCollector;

    public McpToolServer(
        ILoanApplicationRepository applicationRepository,
        IIdentityReader identityReader,
        IIncomeReader incomeReader,
        ICreditReader creditReader,
        IPolicyRetriever policyRetriever,
        SaveRecommendationDraftCommandHandler saveDraftHandler,
        ITelemetryCollector? telemetryCollector = null)
    {
        _applicationRepository = applicationRepository;
        _identityReader = identityReader;
        _incomeReader = incomeReader;
        _creditReader = creditReader;
        _policyRetriever = policyRetriever;
        _saveDraftHandler = saveDraftHandler;
        _telemetryCollector = telemetryCollector;
    }

    public async Task<JsonRpcResponse> HandleRequestAsync(
        JsonRpcRequest request,
        string? headerActorId = null,
        string? headerActorRole = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Method))
        {
            return new JsonRpcResponse("2.0", null, new JsonRpcError(-32600, "Invalid Request: request or method is missing."), request?.Id);
        }

        var actorId = string.IsNullOrWhiteSpace(headerActorId) ? "SystemWorker" : headerActorId;
        var actorRole = string.IsNullOrWhiteSpace(headerActorRole) ? "SystemWorker" : headerActorRole;

        try
        {
            switch (request.Method)
            {
                case "initialize":
                    return new JsonRpcResponse("2.0", new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new
                        {
                            tools = new { }
                        },
                        serverInfo = new
                        {
                            name = "LoanAssistant-MCP-Server",
                            version = "1.0.0"
                        }
                    }, null, request.Id);

                case "notifications/initialized":
                    return new JsonRpcResponse("2.0", new { }, null, request.Id);

                case "ping":
                    return new JsonRpcResponse("2.0", new { }, null, request.Id);

                case "tools/list":
                    return new JsonRpcResponse("2.0", new { tools = GetRegisteredTools() }, null, request.Id);

                case "tools/call":
                    _telemetryCollector?.RecordToolCall();
                    var toolResult = await ExecuteToolCallAsync(request.Params, actorId, actorRole, cancellationToken);
                    return new JsonRpcResponse("2.0", toolResult, null, request.Id);

                default:
                    return new JsonRpcResponse("2.0", null, new JsonRpcError(-32601, $"MethodNotFound: Method '{request.Method}' is not supported."), request.Id);
            }
        }
        catch (Exception ex)
        {
            return new JsonRpcResponse("2.0", null, new JsonRpcError(-32603, $"InternalError: {ex.Message}"), request.Id);
        }
    }

    public static List<McpTool> GetRegisteredTools()
    {
        return new List<McpTool>
        {
            new(
                Name: "get_identity_status",
                Description: "Retrieve typed synthetic identity verification record for a loan application.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        applicationId = new { type = "string", description = "Target Loan Application ID" },
                        syntheticId = new { type = "string", description = "Target Synthetic Applicant ID" }
                    },
                    required = new[] { "applicationId", "syntheticId" }
                }),

            new(
                Name: "get_income_verification",
                Description: "Retrieve typed synthetic income verification record for a loan application.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        applicationId = new { type = "string", description = "Target Loan Application ID" },
                        syntheticId = new { type = "string", description = "Target Synthetic Applicant ID" }
                    },
                    required = new[] { "applicationId", "syntheticId" }
                }),

            new(
                Name: "get_credit_score",
                Description: "Retrieve typed synthetic credit score verification record for a loan application.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        applicationId = new { type = "string", description = "Target Loan Application ID" },
                        syntheticId = new { type = "string", description = "Target Synthetic Applicant ID" }
                    },
                    required = new[] { "applicationId", "syntheticId" }
                }),

            new(
                Name: "search_policy",
                Description: "Search underwriting policy guidelines using hybrid vector/keyword retrieval.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Underwriting query text" },
                        productId = new { type = "string", description = "Optional Loan Product ID filter" }
                    },
                    required = new[] { "query" }
                }),

            new(
                Name: "save_draft",
                Description: "Save a recommendation draft for loan officer review. Does NOT approve or reject applications.",
                InputSchema: new
                {
                    type = "object",
                    properties = new
                    {
                        applicationId = new { type = "string", description = "Target Loan Application ID" },
                        summaryReasoning = new { type = "string", description = "Summary of analysis and reasoning" },
                        routingState = new { type = "string", description = "Routing recommendation: PendingInformation, ManualReview, or ReadyForOfficerReview" },
                        unresolvedItems = new { type = "array", items = new { type = "string" }, description = "List of unverified or missing items" },
                        policyExceptions = new { type = "array", items = new { type = "string" }, description = "List of policy violations or exceptions" },
                        citations = new { type = "array", items = new { type = "object" }, description = "Policy citation references" }
                    },
                    required = new[] { "applicationId", "summaryReasoning", "routingState" }
                })
        };
    }

    private async Task<McpToolResult> ExecuteToolCallAsync(
        object? rawParams,
        string actorId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        if (rawParams == null)
        {
            return ErrorResult("InvalidArguments: Missing parameters for tools/call.");
        }

        using var doc = JsonSerializer.SerializeToDocument(rawParams);
        var root = doc.RootElement;

        if (!root.TryGetProperty("name", out var nameProp) || nameProp.ValueKind != JsonValueKind.String)
        {
            return ErrorResult("InvalidArguments: Parameter 'name' is required.");
        }

        var toolName = nameProp.GetString()!;
        var args = root.TryGetProperty("arguments", out var argsProp) ? argsProp : default;

        return toolName switch
        {
            "get_identity_status" => await HandleGetIdentityStatusAsync(args, cancellationToken),
            "get_income_verification" => await HandleGetIncomeVerificationAsync(args, cancellationToken),
            "get_credit_score" => await HandleGetCreditScoreAsync(args, cancellationToken),
            "search_policy" => await HandleSearchPolicyAsync(args, cancellationToken),
            "save_draft" => await HandleSaveDraftAsync(args, actorId, actorRole, cancellationToken),
            _ => ErrorResult($"ToolNotFound: Tool '{toolName}' is not registered.")
        };
    }

    private async Task<McpToolResult> HandleGetIdentityStatusAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var (appId, synId, err) = GetRequiredScopingArgs(args);
        if (err != null) return err;

        var (app, scopeErr) = await ValidateApplicationScopeAsync(appId!, synId!, cancellationToken);
        if (scopeErr != null) return scopeErr;

        var result = await _identityReader.VerifyIdentityAsync(synId!, cancellationToken);
        var json = JsonSerializer.Serialize(result);
        return SuccessResult(json);
    }

    private async Task<McpToolResult> HandleGetIncomeVerificationAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var (appId, synId, err) = GetRequiredScopingArgs(args);
        if (err != null) return err;

        var (app, scopeErr) = await ValidateApplicationScopeAsync(appId!, synId!, cancellationToken);
        if (scopeErr != null) return scopeErr;

        var result = await _incomeReader.VerifyIncomeAsync(synId!, cancellationToken);
        var json = JsonSerializer.Serialize(result);
        return SuccessResult(json);
    }

    private async Task<McpToolResult> HandleGetCreditScoreAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var (appId, synId, err) = GetRequiredScopingArgs(args);
        if (err != null) return err;

        var (app, scopeErr) = await ValidateApplicationScopeAsync(appId!, synId!, cancellationToken);
        if (scopeErr != null) return scopeErr;

        var result = await _creditReader.GetCreditScoreAsync(synId!, cancellationToken);
        var json = JsonSerializer.Serialize(result);
        return SuccessResult(json);
    }

    private async Task<McpToolResult> HandleSearchPolicyAsync(JsonElement args, CancellationToken cancellationToken)
    {
        if (args.ValueKind == JsonValueKind.Undefined || !args.TryGetProperty("query", out var queryProp) || string.IsNullOrWhiteSpace(queryProp.GetString()))
        {
            return ErrorResult("InvalidArguments: 'query' argument is required for search_policy.");
        }

        var query = queryProp.GetString()!;
        string? productId = args.TryGetProperty("productId", out var pProp) ? pProp.GetString() : null;

        var results = await _policyRetriever.SearchPolicyAsync(query, productId, effectiveVersion: null, topK: 3, cancellationToken);
        var json = JsonSerializer.Serialize(results);
        return SuccessResult(json);
    }

    private async Task<McpToolResult> HandleSaveDraftAsync(JsonElement args, string actorId, string actorRole, CancellationToken cancellationToken)
    {
        if (args.ValueKind == JsonValueKind.Undefined)
        {
            return ErrorResult("InvalidArguments: Missing arguments for save_draft.");
        }

        if (!args.TryGetProperty("applicationId", out var appProp) || string.IsNullOrWhiteSpace(appProp.GetString()))
        {
            return ErrorResult("InvalidArguments: 'applicationId' argument is required.");
        }
        if (!args.TryGetProperty("summaryReasoning", out var sumProp) || string.IsNullOrWhiteSpace(sumProp.GetString()))
        {
            return ErrorResult("InvalidArguments: 'summaryReasoning' argument is required.");
        }
        if (!args.TryGetProperty("routingState", out var routProp) || string.IsNullOrWhiteSpace(routProp.GetString()))
        {
            return ErrorResult("InvalidArguments: 'routingState' argument is required.");
        }

        var appId = appProp.GetString()!;
        var summary = sumProp.GetString()!;
        var routingState = routProp.GetString()!;

        // Safeguard check: save_draft MUST NOT submit Approve or Reject decisions
        if (routingState.Equals("Approve", StringComparison.OrdinalIgnoreCase) ||
            routingState.Equals("Reject", StringComparison.OrdinalIgnoreCase) ||
            routingState.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
            routingState.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorResult("InvalidArguments: Draft recommendations cannot submit final Approve or Reject decisions. RoutingState must be PendingInformation, ManualReview, or ReadyForOfficerReview.");
        }

        var unresolved = ParseStringList(args, "unresolvedItems");
        var exceptions = ParseStringList(args, "policyExceptions");
        var citations = ParseCitations(args);

        try
        {
            var command = new SaveRecommendationDraftCommand(
                ApplicationId: appId,
                SummaryReasoning: summary,
                RoutingState: routingState,
                UnresolvedItems: unresolved,
                PolicyExceptions: exceptions,
                Citations: citations,
                ActorId: actorId,
                ActorRole: actorRole);

            var recDto = await _saveDraftHandler.HandleAsync(command, cancellationToken);
            var json = JsonSerializer.Serialize(recDto);
            return SuccessResult(json);
        }
        catch (KeyNotFoundException ex)
        {
            return ErrorResult($"UnknownApplication: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErrorResult($"UnauthorizedAccess: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return ErrorResult($"InvalidArguments: {ex.Message}");
        }
    }

    private static (string? AppId, string? SynId, McpToolResult? Error) GetRequiredScopingArgs(JsonElement args)
    {
        if (args.ValueKind == JsonValueKind.Undefined)
        {
            return (null, null, ErrorResult("InvalidArguments: Missing arguments object."));
        }

        if (!args.TryGetProperty("applicationId", out var appProp) || string.IsNullOrWhiteSpace(appProp.GetString()))
        {
            return (null, null, ErrorResult("InvalidArguments: 'applicationId' is required."));
        }

        if (!args.TryGetProperty("syntheticId", out var synProp) || string.IsNullOrWhiteSpace(synProp.GetString()))
        {
            return (null, null, ErrorResult("InvalidArguments: 'syntheticId' is required."));
        }

        return (appProp.GetString(), synProp.GetString(), null);
    }

    private async Task<(Domain.Applications.LoanApplication? App, McpToolResult? Error)> ValidateApplicationScopeAsync(
        string applicationId,
        string syntheticId,
        CancellationToken cancellationToken)
    {
        var app = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
        if (app == null)
        {
            return (null, ErrorResult($"UnknownApplication: Loan application '{applicationId}' not found."));
        }

        if (app.Facts == null || string.IsNullOrWhiteSpace(app.Facts.SyntheticId))
        {
            return (null, ErrorResult($"InvalidApplicationState: Application '{applicationId}' has no synthetic ID bound."));
        }

        if (!app.Facts.SyntheticId.Equals(syntheticId, StringComparison.OrdinalIgnoreCase))
        {
            return (null, ErrorResult($"CrossApplicationMismatch: Synthetic identity '{syntheticId}' does not belong to application '{applicationId}' (bound: '{app.Facts.SyntheticId}'). Cross-application access denied."));
        }

        return (app, null);
    }

    private static List<string>? ParseStringList(JsonElement args, string propertyName)
    {
        if (args.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var elem in prop.EnumerateArray())
            {
                if (elem.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(elem.GetString()))
                {
                    list.Add(elem.GetString()!);
                }
            }
            return list;
        }
        return null;
    }

    private static List<CitationInput>? ParseCitations(JsonElement args)
    {
        if (args.TryGetProperty("citations", out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            var list = new List<CitationInput>();
            foreach (var elem in prop.EnumerateArray())
            {
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    var doc = elem.TryGetProperty("documentTitle", out var d) ? d.GetString() ?? "" : "";
                    var ver = elem.TryGetProperty("policyVersion", out var v) ? v.GetString() ?? "" : "";
                    var sec = elem.TryGetProperty("sectionOrPage", out var s) ? s.GetString() ?? "" : "";
                    var exc = elem.TryGetProperty("excerpt", out var e) ? e.GetString() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(doc) && !string.IsNullOrWhiteSpace(ver))
                    {
                        list.Add(new CitationInput(doc, ver, sec, exc));
                    }
                }
            }
            return list;
        }
        return null;
    }

    private static McpToolResult SuccessResult(string text)
    {
        return new McpToolResult(new List<McpToolContent> { new("text", text) }, IsError: false);
    }

    private static McpToolResult ErrorResult(string message)
    {
        return new McpToolResult(new List<McpToolContent> { new("text", message) }, IsError: true);
    }
}
