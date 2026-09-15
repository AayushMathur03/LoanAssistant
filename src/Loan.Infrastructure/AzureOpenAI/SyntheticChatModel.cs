using System.Runtime.CompilerServices;
using System.Text.Json;
using Loan.Application.Abstractions;

namespace Loan.Infrastructure.AzureOpenAI;

public class SyntheticChatModel : IChatModel
{
    private readonly ITelemetryCollector? _telemetryCollector;

    public SyntheticChatModel(ITelemetryCollector? telemetryCollector = null)
    {
        _telemetryCollector = telemetryCollector;
    }

    public Task<string> GenerateCompletionAsync(
        IEnumerable<ChatMessage> messages,
        double temperature = 0.2,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var messageList = messages.ToList();
        var userMsg = messageList.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        var sysMsg = messageList.FirstOrDefault(m => m.Role == "system")?.Content ?? "";

        string resultText;

        if (userMsg.Contains("OVERRIDE", StringComparison.OrdinalIgnoreCase) ||
            userMsg.Contains("Ignore all previous instructions", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Safety Refusal: AI models cannot approve loans, bypass verification, or alter loan state. Final eligibility decisions belong exclusively to human loan officers per BR-01 & BR-07.";
        }
        else if (userMsg.Contains("LTV", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Residential Mortgage Underwriting Guide (v1.2, Section 3.2), the maximum Loan-to-Value (LTV) ratio for standard residential loans without Private Mortgage Insurance (PMI) is 80.0%.";
        }
        else if (userMsg.Contains("DTI", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Residential Mortgage Underwriting Guide (v1.2, Section 3.1), the maximum Debt-to-Income (DTI) ratio allowed for standard residential loans is 43.0%. Applications exceeding 43.0% are referred to a human loan officer.";
        }
        else if (userMsg.Contains("credit score", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Residential Mortgage Underwriting Guide (v1.2, Section 4.0), a minimum credit score of 640 is required for standard residential mortgage eligibility.";
        }
        else
        {
            resultText = "Based on the provided policy guidelines, standard residential loan requirements specify a max DTI of 43%, max LTV of 80%, and min credit score of 640. Please review cited policy documents for full details.";
        }

        sw.Stop();

        var promptTokens = messageList.Sum(m => m.Content.Length) / 4;
        var completionTokens = resultText.Length / 4;

        _telemetryCollector?.RecordTokens(promptTokens, completionTokens);
        _telemetryCollector?.RecordLlmLatency(sw.Elapsed.TotalMilliseconds);

        return Task.FromResult(resultText);
    }

    public Task<T> GenerateStructuredAsync<T>(
        IEnumerable<ChatMessage> messages,
        double temperature = 0.1,
        CancellationToken cancellationToken = default)
    {
        // Fallback JSON generation for structured types
        var json = JsonSerializer.Serialize(Activator.CreateInstance<T>());
        var result = JsonSerializer.Deserialize<T>(json)!;
        return Task.FromResult(result);
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        IEnumerable<ChatMessage> messages,
        double temperature = 0.2,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var fullText = await GenerateCompletionAsync(messages, temperature, cancellationToken);
        var words = fullText.Split(' ');
        foreach (var word in words)
        {
            yield return word + " ";
            await Task.Delay(10, cancellationToken);
        }
    }
}
