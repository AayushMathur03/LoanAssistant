using System.Text.Json.Serialization;

namespace Loan.Infrastructure.MCP;

public record JsonRpcRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] object? Params,
    [property: JsonPropertyName("id")] object? Id);

public record JsonRpcResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("result")] object? Result,
    [property: JsonPropertyName("error")] JsonRpcError? Error,
    [property: JsonPropertyName("id")] object? Id);

public record JsonRpcError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data")] object? Data = null);

public record McpTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] object InputSchema);

public record McpToolContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string Text);

public record McpToolResult(
    [property: JsonPropertyName("content")] List<McpToolContent> Content,
    [property: JsonPropertyName("isError")] bool IsError = false);
