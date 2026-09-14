using Loan.Application.Abstractions;
using Loan.Application.DTOs;

namespace Loan.Infrastructure.Search;

public class SyntheticPolicyRetriever : IPolicyRetriever
{
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
            Content: "For standard residential mortgages, the maximum Debt-to-Income (DTI) ratio is capped at 43.0%. Applications exceeding 43.0% DTI must be referred to a human loan officer for exception review. DTI is calculated deterministically as total monthly debt obligations divided by gross monthly income.",
            Excerpt: "Maximum DTI ratio is 43.0%. DTI > 43% requires human loan officer referral."
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
            Title: "Unsecured Personal Loan Policy",
            Version: "v1.0",
            Section: "Section 2.0 - Personal Loan Eligibility",
            Content: "Unsecured personal loans support up to $50,000 with a maximum DTI of 36.0% and minimum credit score of 620. Minimum verified monthly income requirement is $2,000.",
            Excerpt: "Personal loan cap $50,000, max DTI 36.0%, min credit score 620."
        ),
        new(
            DocumentId: "DOC-COMPLIANCE-001",
            ProductId: "ALL",
            Title: "Consumer Lending Regulatory Disclosure",
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
        var filtered = _documents.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(targetProductId))
        {
            filtered = filtered.Where(d => d.ProductId.Equals(targetProductId, StringComparison.OrdinalIgnoreCase) || d.ProductId == "ALL");
        }

        if (!string.IsNullOrWhiteSpace(effectiveVersion))
        {
            filtered = filtered.Where(d => d.Version.Equals(effectiveVersion, StringComparison.OrdinalIgnoreCase));
        }

        // Simple text relevance scoring for synthetic search (filtering generic stop words)
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "what", "is", "the", "underwriting", "policy", "for", "financing", "a", "an", "in", "of", "to", "and", "or", "about", "rules", "guidelines"
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

        return Task.FromResult<IEnumerable<PolicySearchResultDto>>(results);
    }
}
