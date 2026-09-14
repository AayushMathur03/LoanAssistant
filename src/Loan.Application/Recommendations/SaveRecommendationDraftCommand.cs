using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Domain.Recommendations;

namespace Loan.Application.Recommendations;

public record CitationInput(string DocumentTitle, string PolicyVersion, string SectionOrPage, string Excerpt);

public record SaveRecommendationDraftCommand(
    string ApplicationId,
    string SummaryReasoning,
    string RoutingState,
    List<string>? UnresolvedItems = null,
    List<string>? PolicyExceptions = null,
    List<CitationInput>? Citations = null,
    double? RiskScore = null,
    string ActorId = "SystemWorker",
    string ActorRole = "SystemWorker");

public class SaveRecommendationDraftCommandHandler
{
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly IRecommendationRepository _recommendationRepository;

    public SaveRecommendationDraftCommandHandler(
        ILoanApplicationRepository applicationRepository,
        IRecommendationRepository recommendationRepository)
    {
        _applicationRepository = applicationRepository;
        _recommendationRepository = recommendationRepository;
    }

    public async Task<RecommendationDto> HandleAsync(SaveRecommendationDraftCommand command, CancellationToken cancellationToken = default)
    {
        var app = await _applicationRepository.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan application '{command.ApplicationId}' not found.");

        if (string.IsNullOrWhiteSpace(command.ActorRole) || 
            (!command.ActorRole.Equals("SystemWorker", StringComparison.OrdinalIgnoreCase) && 
             !command.ActorRole.Equals("Officer", StringComparison.OrdinalIgnoreCase) && 
             !command.ActorRole.Equals("System", StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException($"Actor role '{command.ActorRole}' is not authorized to save recommendation draft.");
        }

        var normalizedRouting = command.RoutingState?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRouting) ||
            normalizedRouting.Equals("Approve", StringComparison.OrdinalIgnoreCase) ||
            normalizedRouting.Equals("Reject", StringComparison.OrdinalIgnoreCase) ||
            normalizedRouting.Equals("Approved", StringComparison.OrdinalIgnoreCase) ||
            normalizedRouting.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Draft recommendations cannot submit final Approve or Reject decisions. RoutingState must be PendingInformation, ManualReview, or ReadyForOfficerReview.");
        }

        RecommendationType recType = normalizedRouting switch
        {
            "PendingInformation" => RecommendationType.PendingInformation,
            "ManualReview" => RecommendationType.ReferToHuman,
            "ReadyForOfficerReview" => (command.PolicyExceptions != null && command.PolicyExceptions.Count > 0) ? RecommendationType.ReferToHuman : RecommendationType.Approve,
            _ => throw new ArgumentException($"Invalid routingState '{command.RoutingState}'. Allowed values: PendingInformation, ManualReview, ReadyForOfficerReview.")
        };

        var unresolved = command.UnresolvedItems ?? new List<string>();
        var exceptions = command.PolicyExceptions ?? new List<string>();

        if (unresolved.Count > 0)
        {
            recType = RecommendationType.PendingInformation;
        }

        var citations = (command.Citations ?? new List<CitationInput>())
            .Select(c => new RecommendationCitation(c.DocumentTitle, c.PolicyVersion, c.SectionOrPage, c.Excerpt))
            .ToList();

        var calculatedRisk = command.RiskScore ?? (exceptions.Count > 0 ? 0.65 : (unresolved.Count > 0 ? 0.50 : 0.15));

        var recId = $"REC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var now = DateTime.UtcNow;

        var rec = new Recommendation(
            recommendationId: recId,
            decisionRecommendation: recType,
            riskScore: calculatedRisk,
            summaryReasoning: command.SummaryReasoning,
            citations: citations,
            policyViolations: exceptions,
            missingEvidenceItems: unresolved,
            createdAtUtc: now,
            createdBySystemAgent: command.ActorId);

        await _recommendationRepository.SaveAsync(rec, cancellationToken);
        app.SetRecommendation(rec, now);
        await _applicationRepository.UpdateAsync(app, cancellationToken);

        return MapToDto(rec);
    }

    private static RecommendationDto MapToDto(Recommendation rec)
    {
        return new RecommendationDto(
            RecommendationId: rec.RecommendationId,
            DecisionRecommendation: rec.DecisionRecommendation,
            Status: rec.Status,
            RiskScore: rec.RiskScore,
            SummaryReasoning: rec.SummaryReasoning,
            Citations: rec.Citations.Select(c => new CitationDto(c.DocumentTitle, c.PolicyVersion, c.SectionOrPage, c.Excerpt)).ToList(),
            PolicyViolations: rec.PolicyViolations,
            MissingEvidenceItems: rec.MissingEvidenceItems,
            AuditTrail: rec.AuditTrail.Select(a => new AuditEntryDto(a.Action, a.PerformedBy, a.TimestampUtc, a.Notes)).ToList(),
            ApprovedByOfficerId: rec.ApprovedByOfficerId,
            ApprovedAtUtc: rec.ApprovedAtUtc,
            OfficerDecisionNotes: rec.OfficerDecisionNotes);
    }
}
