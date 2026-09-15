using System.Diagnostics;
using Loan.Application.Abstractions;
using Loan.Application.Common;

namespace Loan.Web.Middleware;

public class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITelemetryCollector telemetryCollector)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        }

        CorrelationContext.Current = correlationId;
        telemetryCollector.SetCorrelationId(correlationId);
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            telemetryCollector.RecordError($"UnhandledException:{ex.GetType().Name}");
            throw;
        }
        finally
        {
            sw.Stop();
            var telemetry = telemetryCollector.GetCurrentTelemetry();
            telemetry.TotalHttpLatencyMs = sw.Elapsed.TotalMilliseconds;
            CorrelationContext.Current = string.Empty;
        }
    }
}
