using Loan.Application.Recommendations;

namespace Loan.Application.Agents;

public record DocumentAnalysisResult(
    string ApplicationId,
    bool HasAllRequiredDocuments,
    int TotalDocumentsUploaded,
    List<string> UnresolvedFields,
    List<string> LowConfidenceFields,
    string AnalysisNotes);

public record EligibilityAnalysisResult(
    string ApplicationId,
    decimal? DebtToIncomeRatio,
    decimal? LoanToValueRatio,
    int EffectiveCreditScore,
    decimal EffectiveMonthlyIncome,
    bool IsDtiEligible,
    bool IsLtvEligible,
    bool IsCreditScoreEligible,
    string DeterministicStatus,
    List<string> FinancialRiskFactors,
    string SummaryNotes);

public record ComplianceReviewResult(
    string ApplicationId,
    string EffectivePolicyVersion,
    List<string> PolicyViolations,
    List<CitationInput> Citations,
    bool HasSufficientEvidence,
    string ComplianceNotes);

public record RecommendationDraft(
    string ApplicationId,
    string RoutingState,            // DETERMINISTIC: "PendingInformation" | "ManualReview" | "ReadyForOfficerReview"
    double RiskScore,               // DETERMINISTIC: 0.15 (low) to 0.85 (high)
    string SummaryReasoning,        // LLM-GENERATED & VALIDATED
    List<string> UnresolvedItems,   // LLM-EXPLAINED & VALIDATED
    List<string> PolicyExceptions,  // LLM-EXPLAINED & VALIDATED
    List<CitationInput> Citations,  // RAG GROUNDED & VALIDATED
    string NonApprovalDisclaimer    // DETERMINISTIC CONSTANT
);
