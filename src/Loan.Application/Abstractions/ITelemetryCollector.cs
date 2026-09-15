namespace Loan.Application.Abstractions;

public record RequestTelemetry
{
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public long PromptTokens { get; set; }
    public long CompletionTokens { get; set; }
    public long TotalTokens => PromptTokens + CompletionTokens;
    public double LlmLatencyMs { get; set; }
    public double RagLatencyMs { get; set; }
    public double TotalHttpLatencyMs { get; set; }
    public Dictionary<string, double> AgentStageLatencyMs { get; set; } = new();
    public int ToolCallCount { get; set; }
    public int RetrievalHitCount { get; set; }
    public string ErrorCategory { get; set; } = "None";
    public string RoutingDistribution { get; set; } = "Standard";
    public string? OfficerDecision { get; set; }
}

public interface ITelemetryCollector
{
    string GetCurrentCorrelationId();
    void SetCorrelationId(string correlationId);
    RequestTelemetry GetCurrentTelemetry();
    void RecordTokens(long promptTokens, long completionTokens);
    void RecordLlmLatency(double durationMs);
    void RecordRagLatency(double durationMs, int hitCount);
    void RecordAgentStageLatency(string stageName, double durationMs);
    void RecordToolCall();
    void RecordError(string errorCategory);
    void RecordRoutingDistribution(string routing);
    void RecordOfficerDecision(string decision);
    IReadOnlyList<RequestTelemetry> GetRecentTelemetry(int count = 50);
}
