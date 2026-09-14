using Loan.Domain.Recommendations;

namespace Loan.Application.DTOs;

public record AuditEntryDto(
    string Action,
    string PerformedBy,
    DateTime TimestampUtc,
    string Notes);

public record RecommendationDto(
    string RecommendationId,
    RecommendationType DecisionRecommendation,
    RecommendationStatus Status,
    double RiskScore,
    string SummaryReasoning,
    IReadOnlyList<CitationDto> Citations,
    IReadOnlyList<string> PolicyViolations,
    IReadOnlyList<string> MissingEvidenceItems,
    IReadOnlyList<AuditEntryDto> AuditTrail,
    string? ApprovedByOfficerId,
    DateTime? ApprovedAtUtc,
    string? OfficerDecisionNotes);
