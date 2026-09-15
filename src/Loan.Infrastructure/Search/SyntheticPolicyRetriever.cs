using Loan.Application.Abstractions;
using Loan.Application.DTOs;

namespace Loan.Infrastructure.Search;

public class SyntheticPolicyRetriever : IPolicyRetriever
{
    private readonly ITelemetryCollector? _telemetryCollector;

    public SyntheticPolicyRetriever(ITelemetryCollector? telemetryCollector = null)
    {
        _telemetryCollector = telemetryCollector;
    }

    private record DocumentRecord(
        string DocumentId,
        string ProductId,
        string Title,
        string Version,
        string Section,
        string Content,
        string Excerpt);

    private readonly List<DocumentRecord> _documents = new()
    {
        new(
            DocumentId: "DOC-MORTGAGE-001",
            ProductId: "MORTGAGE-STD",
            Title: "Residential Mortgage Underwriting Guide",
            Version: "v1.2",
            Section: "Section 3.1 - Debt to Income Ratio",
            Content: "For standard residential mortgages under active v1.2 policy, the maximum Debt-to-Income (DTI) ratio is capped at 43.0%. Applications exceeding 43.0% DTI must be referred to a human loan officer for exception review. Maximum loan amount is $750,000.",
            Excerpt: "Maximum DTI ratio is 43.0%. Maximum loan amount is $750,000."
        ),
        new(
            DocumentId: "DOC-MORTGAGE-002",
            ProductId: "MORTGAGE-STD",
            Title: "Residential Mortgage Underwriting Guide",
            Version: "v1.2",
            Section: "Section 3.2 - Loan to Value & Down Payment",
            Content: "The maximum Loan-to-Value (LTV) ratio for standard residential loans without Private Mortgage Insurance (PMI) is 80.0%. LTV is calculated as requested loan amount divided by property appraisal value. A minimum 20% down payment is required to waive PMI.",
            Excerpt: "Maximum LTV ratio is 80.0% without PMI."
        ),
        new(
            DocumentId: "DOC-MORTGAGE-003",
            ProductId: "MORTGAGE-STD",
            Title: "Residential Mortgage Underwriting Guide",
            Version: "v1.2",
            Section: "Section 4.0 - Credit Score Thresholds",
            Content: "A minimum credit score of 640 is required for standard residential mortgage eligibility. Scores between 620 and 639 require compliance manual review. Scores below 620 are ineligible for standard products.",
            Excerpt: "Minimum credit score 640 required for standard mortgage."
        ),
        new(
            DocumentId: "DOC-PERSONAL-001",
            ProductId: "LOAN-PERSONAL",
            Title: "Personal Loan Product Guide v2.0",
            Version: "v2.0",
            Section: "Section 2.0 - Personal Loan Eligibility & Principal Limits",
            Content: "Under effective Personal Loan Policy v2.0, unsecured personal loans support a maximum loan amount of $75,000 with a maximum standard DTI ratio of 38.0% and a minimum credit score of 600. Minimum verified monthly income requirement is $2,500.",
            Excerpt: "Personal loan maximum loan amount $75,000, max DTI 38.0%, min credit score 600."
        ),
        new(
            DocumentId: "DOC-INCOME-001",
            ProductId: "ALL",
            Title: "Income and Employment Verification Policy v1.0",
            Version: "v1.0",
            Section: "Section 1.2 - Eligible Document Types",
            Content: "Accepted document types for income verification include Tax Return (Forms 1040/Schedule C), W-2 Wage Statement, Recent Paystub, and Certified Bank Statement.",
            Excerpt: "Accepted income verification document types include Tax Return, W-2, Paystub, Bank Statement."
        ),
        new(
            DocumentId: "DOC-COMPLIANCE-001",
            ProductId: "ALL",
            Title: "Consumer Protection, Fair Lending, and Disclosures v2.0",
            Version: "v2.0",
            Section: "Section 8.1 - TRID and RESPA Compliance",
            Content: "Under TRID/RESPA guidelines, Loan Estimate disclosures must be delivered within three business days of receiving a completed mortgage application. Unearned fees and kickbacks under Section 8 are strictly prohibited.",
            Excerpt: "Loan Estimate disclosures must be delivered within three business days under TRID/RESPA guidelines."
        ),
        new(
            DocumentId: "DOC-COMPLIANCE-002",
            ProductId: "ALL",
            Title: "Consumer Protection, Fair Lending, and Disclosures v2.0",
            Version: "v2.0",
            Section: "Section 1.5 - Fair Lending & Non-Approval Disclaimer",
            Content: "All AI assistant responses are for information, preliminary evidence validation, and draft recommendation assembly only. Final approval or rejection rests exclusively with authorized human loan officers.",
            Excerpt: "Final approval decisions belong exclusively to human loan officers."
        )
    };

    public Task<IEnumerable<PolicySearchResultDto>> SearchPolicyAsync(
        string query,
        string? targetProductId = null,
        string? effectiveVersion = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var filtered = _documents.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(targetProductId))
        {
            filtered = filtered.Where(d => d.ProductId.Equals(targetProductId, StringComparison.OrdinalIgnoreCase) || d.ProductId == "ALL");
        }

        if (!string.IsNullOrWhiteSpace(effectiveVersion))
        {
            filtered = filtered.Where(d => d.Version.Equals(effectiveVersion, StringComparison.OrdinalIgnoreCase));
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "what", "is", "the", "underwriting", "policy", "for", "financing", "a", "an", "in", "of", "to", "and", "or", "about", "rules", "guidelines", "under", "active", "loan", "loans", "interest", "rate", "cap"
        };

        var queryTerms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => !stopWords.Contains(term))
            .ToList();

        var results = filtered
            .Select(d =>
            {
                double score = 0.0;
                foreach (var term in queryTerms)
                {
                    if (d.Content.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        d.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                        d.Section.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 0.35;
                    }
                }
                return new { Document = d, Score = Math.Min(score, 0.99) };
            })
            .Where(r => r.Score >= 0.3)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .Select(r => new PolicySearchResultDto(
                DocumentId: r.Document.DocumentId,
                Title: r.Document.Title,
                Version: r.Document.Version,
                Section: r.Document.Section,
                Content: r.Document.Content,
                SimilarityScore: r.Score,
                Citation: new CitationDto(
                    DocumentTitle: r.Document.Title,
                    PolicyVersion: r.Document.Version,
                    SectionOrPage: r.Document.Section,
                    Excerpt: r.Document.Excerpt)))
            .ToList();

        sw.Stop();
        _telemetryCollector?.RecordRagLatency(sw.Elapsed.TotalMilliseconds, results.Count);

        return Task.FromResult<IEnumerable<PolicySearchResultDto>>(results);
    }
}
