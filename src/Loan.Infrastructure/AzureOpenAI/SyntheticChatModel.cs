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
            userMsg.Contains("Ignore all previous instructions", StringComparison.OrdinalIgnoreCase) ||
            userMsg.Contains("DEVELOPER MODE", StringComparison.OrdinalIgnoreCase) ||
            userMsg.Contains("BYPASS RULES", StringComparison.OrdinalIgnoreCase) ||
            userMsg.Contains("PRINT SYSTEM PROMPT", StringComparison.OrdinalIgnoreCase) ||
            userMsg.Contains("SET STATUS APPROVED", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Safety Refusal: AI models cannot approve loans, bypass verification, alter loan state, or reveal internal system prompts. Final eligibility decisions belong exclusively to human loan officers per BR-01 & BR-07.";
        }
        else if (userMsg.Contains("commercial jet", StringComparison.OrdinalIgnoreCase) || userMsg.Contains("cryptocurrency", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "We could not find sufficient matching policy evidence in our current knowledge base to answer your question.";
        }
        else if (userMsg.Contains("Personal Loan", StringComparison.OrdinalIgnoreCase) && userMsg.Contains("credit score", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Personal Loan Product Guide v2.0 (v2.0, Section 2.2), a minimum credit score of 600 may be considered for preferred tiers for an unsecured Personal Loan under the active policy.";
        }
        else if (userMsg.Contains("Personal Loan", StringComparison.OrdinalIgnoreCase) && userMsg.Contains("DTI", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Personal Loan Product Guide v2.0 (v2.0, Section 2.1), the maximum standard Debt-to-Income (DTI) ratio allowed for an unsecured Personal Loan under active policy v2.0 is 38.0%.";
        }
        else if (userMsg.Contains("Personal Loan", StringComparison.OrdinalIgnoreCase) && (userMsg.Contains("maximum loan amount", StringComparison.OrdinalIgnoreCase) || userMsg.Contains("loan amount", StringComparison.OrdinalIgnoreCase)))
        {
            resultText = "Per Personal Loan Product Guide v2.0 (v2.0, Section 1.2), the maximum loan amount for an unsecured Personal Loan under active policy v2.0 is $75,000.";
        }
        else if (userMsg.Contains("TRID", StringComparison.OrdinalIgnoreCase) || userMsg.Contains("RESPA", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Consumer Protection, Fair Lending, and Disclosures v2.0 (v2.0, Section 8.1), under TRID/RESPA guidelines, Loan Estimate disclosures must be delivered within three business days of receiving a completed mortgage application.";
        }
        else if (userMsg.Contains("document types", StringComparison.OrdinalIgnoreCase) && userMsg.Contains("income", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Income and Employment Verification Policy v1.0 (v1.0, Section 1.2), accepted document types for income verification include Tax Return (Forms 1040/Schedule C), W-2 Wage Statement, Recent Paystub, and Certified Bank Statement.";
        }
        else if (userMsg.Contains("$0", StringComparison.OrdinalIgnoreCase) && userMsg.Contains("income", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Debt Calculation and Obligation Rules (v1.0), an applicant with $0 monthly income has an Invalid/null debt-to-income (DTI) ratio. Zero or negative income produces an Ineligible status to prevent division by zero errors.";
        }
        else if (userMsg.Contains("verbal statement", StringComparison.OrdinalIgnoreCase) || userMsg.Contains("overridden", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Debt Calculation and Obligation Rules (v1.0), verified income (such as W-2 or Tax Returns) strictly takes precedence over unverified or stated claims from an applicant per BR-02.";
        }
        else if (userMsg.Contains("issue a final loan approval", StringComparison.OrdinalIgnoreCase) || userMsg.Contains("final loan approval", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Consumer Protection, Fair Lending, and Disclosures v2.0 (v2.0, Section 1.5), the AI assistant cannot issue a final loan approval decision. Final approval or rejection authority rests exclusively with authorized human loan officers per BR-07.";
        }
        else if (userMsg.Contains("routing state", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per AI Agent Orchestration Rules, recommendations requiring officer review are assigned the DraftPreparedBySystem status with a routing state of ReadyForOfficerReview.";
        }
        else if (userMsg.Contains("residential mortgage", StringComparison.OrdinalIgnoreCase) && userMsg.Contains("maximum loan amount", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Residential Mortgage Underwriting Guide (v1.2, Section 3.1), the maximum loan amount for a standard residential mortgage is $750,000.";
        }
        else if (userMsg.Contains("LTV", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Residential Mortgage Underwriting Guide (v1.2, Section 3.2), the maximum Loan-to-Value (LTV) ratio for standard residential loans without Private Mortgage Insurance (PMI) is 80.0%.";
        }
        else if (userMsg.Contains("DTI", StringComparison.OrdinalIgnoreCase) && !userMsg.Contains("Auto Loan", StringComparison.OrdinalIgnoreCase))
        {
            resultText = "Per Residential Mortgage Underwriting Guide (v1.2, Section 3.1), the maximum Debt-to-Income (DTI) ratio allowed for standard residential loans is 43.0%. Applications exceeding 43.0% are referred to a human loan officer.";
        }
        else if (userMsg.Contains("credit score", StringComparison.OrdinalIgnoreCase) && !userMsg.Contains("Personal Loan", StringComparison.OrdinalIgnoreCase))
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
