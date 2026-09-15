using System.Collections.Concurrent;
using Loan.Application.Abstractions;
using Loan.Application.Common;

namespace Loan.Infrastructure.Telemetry;

public class InMemoryTelemetryCollector : ITelemetryCollector
{
    private readonly ConcurrentDictionary<string, RequestTelemetry> _activeTelemetry = new();
    private readonly ConcurrentQueue<RequestTelemetry> _recentTelemetry = new();
    private const int MaxRecentCount = 100;

    public string GetCurrentCorrelationId()
    {
        return CorrelationContext.EnsureCorrelationId();
    }

    public void SetCorrelationId(string correlationId)
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            CorrelationContext.Current = correlationId;
        }
    }

    public RequestTelemetry GetCurrentTelemetry()
    {
        var cid = GetCurrentCorrelationId();
        return _activeTelemetry.GetOrAdd(cid, id =>
        {
            var telemetry = new RequestTelemetry { CorrelationId = id };
            _recentTelemetry.Enqueue(telemetry);
            while (_recentTelemetry.Count > MaxRecentCount)
            {
                _recentTelemetry.TryDequeue(out _);
            }
            return telemetry;
        });
    }

    public void RecordTokens(long promptTokens, long completionTokens)
    {
        var telemetry = GetCurrentTelemetry();
        lock (telemetry)
        {
            telemetry.PromptTokens += promptTokens;
            telemetry.CompletionTokens += completionTokens;
        }
    }

    public void RecordLlmLatency(double durationMs)
    {
        var telemetry = GetCurrentTelemetry();
        lock (telemetry)
        {
            telemetry.LlmLatencyMs += durationMs;
        }
    }

    public void RecordRagLatency(double durationMs, int hitCount)
    {
        var telemetry = GetCurrentTelemetry();
        lock (telemetry)
        {
            telemetry.RagLatencyMs += durationMs;
            telemetry.RetrievalHitCount += hitCount;
        }
    }

    public void RecordAgentStageLatency(string stageName, double durationMs)
    {
        var telemetry = GetCurrentTelemetry();
        lock (telemetry)
        {
            telemetry.AgentStageLatencyMs[stageName] = durationMs;
        }
    }

    public void RecordToolCall()
    {
        var telemetry = GetCurrentTelemetry();
        lock (telemetry)
        {
            telemetry.ToolCallCount++;
        }
    }

    public void RecordError(string errorCategory)
    {
        var telemetry = GetCurrentTelemetry();
        lock (telemetry)
        {
            telemetry.ErrorCategory = errorCategory;
        }
    }

    public void RecordRoutingDistribution(string routing)
    {
        var telemetry = GetCurrentTelemetry();
        lock (telemetry)
        {
            telemetry.RoutingDistribution = routing;
        }
    }

    public void RecordOfficerDecision(string decision)
    {
        var telemetry = GetCurrentTelemetry();
        lock (telemetry)
        {
            telemetry.OfficerDecision = decision;
        }
    }

    public IReadOnlyList<RequestTelemetry> GetRecentTelemetry(int count = 50)
    {
        return _recentTelemetry
            .Reverse()
            .Take(count)
            .ToList();
    }
}
