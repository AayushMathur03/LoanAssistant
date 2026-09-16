using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Domain.Recommendations;

namespace Loan.Application.Recommendations;

public record GenerateRecommendationDraftCommand(string ApplicationId);

public class GenerateRecommendationDraftCommandHandler
{
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly IRecommendationRepository _recommendationRepository;

    public GenerateRecommendationDraftCommandHandler(
        ILoanApplicationRepository applicationRepository,
        IRecommendationRepository recommendationRepository)
    {
        _applicationRepository = applicationRepository;
        _recommendationRepository = recommendationRepository;
    }

    public async Task<RecommendationDto> HandleAsync(GenerateRecommendationDraftCommand command, CancellationToken cancellationToken = default)
    {
        var app = await _applicationRepository.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan application '{command.ApplicationId}' not found.");

        if (app.Indicators == null)
        {
            throw new InvalidOperationException("Eligibility must be evaluated before generating a recommendation draft.");
        }

        var missingEvidence = app.Indicators.UnmetConditions
            .Where(c => c.Contains("verification", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Document Gating: Check for unconfirmed low-confidence fields across documents
        foreach (var doc in app.Documents)
        {
            foreach (var f in doc.Fields)
            {
                if (f.NeedsConfirmation)
                {
                    missingEvidence.Add($"Unconfirmed low-confidence field '{f.FieldName}' in {doc.DocumentType} ({f.ConfidenceScore:P0} confidence). Confirmation required.");
                }
            }
        }

        // Check for missing mandatory document types per product
        bool hasIncomeDoc = app.Documents.Any(d => d.DocumentType is Domain.Documents.DocumentType.Paystub or Domain.Documents.DocumentType.W2 or Domain.Documents.DocumentType.TaxReturn);
        bool hasIdDoc = app.Documents.Any(d => d.DocumentType == Domain.Documents.DocumentType.DriverLicenseOrPassport);
        bool hasBankDoc = app.Documents.Any(d => d.DocumentType == Domain.Documents.DocumentType.BankStatement);

        if (!app.Facts.IsIncomeVerified && !hasIncomeDoc && !missingEvidence.Any(e => e.Contains("Income", StringComparison.OrdinalIgnoreCase)))
        {
            missingEvidence.Add("Missing mandatory evidence: Recent 30-Day Paystub or W-2");
        }
        if (!app.Facts.IsIdentityVerified && !hasIdDoc && !missingEvidence.Any(e => e.Contains("Identity", StringComparison.OrdinalIgnoreCase)))
        {
            missingEvidence.Add("Missing mandatory evidence: Government Photo ID (Driver License or Passport)");
        }
        if (app.Documents.Count > 0 && (app.ProductRules.ProductId == "MORTGAGE-STD" || app.ProductRules.RequiresPropertyValuation) && !hasBankDoc)
        {
            missingEvidence.Add("Missing mandatory evidence: 60-Day Bank Statement");
        }

        var policyViolations = app.Indicators.UnmetConditions
            .Where(c => !c.Contains("verification", StringComparison.OrdinalIgnoreCase))
            .ToList();

        RecommendationType systemType;
        if (missingEvidence.Any())
        {
            systemType = RecommendationType.PendingInformation;
        }
        else if (policyViolations.Any())
        {
            systemType = RecommendationType.ReferToHuman;
        }
        else
        {
            systemType = RecommendationType.Approve;
        }

        var citations = new List<RecommendationCitation>
        {
            new("Mortgage Eligibility Standard", app.ProductRules.EffectiveVersion, "Section 4.1", "Maximum DTI ratio 43% and LTV threshold 80%.")
        };

        var now = DateTime.UtcNow;
        var recId = $"REC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        
        var summary = systemType switch
        {
            RecommendationType.Approve => "Applicant satisfies all deterministic policy guidelines and verification checks.",
            RecommendationType.PendingInformation => "Application is missing mandatory identity or income verification evidence.",
            RecommendationType.ReferToHuman => "Application exhibits policy exception(s) requiring manual loan officer review.",
            _ => "Review required."
        };

        var rec = new Recommendation(
            recommendationId: recId,
            decisionRecommendation: systemType,
            riskScore: policyViolations.Any() ? 0.65 : 0.15,
            summaryReasoning: summary,
            citations: citations,
            policyViolations: policyViolations,
            missingEvidenceItems: missingEvidence,
            createdAtUtc: now);

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

public record OfficerDecisionCommand(
    string ApplicationId,
    string OfficerId,
    RecommendationStatus Action, // ApprovedByOfficer, RejectedByOfficer, ReturnedForInfo
    string DecisionNotes);

public class OfficerDecisionCommandHandler
{
    private readonly ILoanApplicationRepository _repository;

    public OfficerDecisionCommandHandler(ILoanApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task<LoanApplicationDto> HandleAsync(OfficerDecisionCommand command, CancellationToken cancellationToken = default)
    {
        var app = await _repository.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan application '{command.ApplicationId}' not found.");

        var now = DateTime.UtcNow;

        switch (command.Action)
        {
            case RecommendationStatus.ApprovedByOfficer:
                app.OfficerApprove(command.OfficerId, command.DecisionNotes, now);
                break;

            case RecommendationStatus.RejectedByOfficer:
                app.OfficerReject(command.OfficerId, command.DecisionNotes, now);
                break;

            case RecommendationStatus.ReturnedForInfo:
                app.OfficerReturnForInfo(command.OfficerId, command.DecisionNotes, now);
                break;

            default:
                throw new ArgumentException($"Invalid officer decision action '{command.Action}'.", nameof(command));
        }

        await _repository.UpdateAsync(app, cancellationToken);

        var factsDto = new ApplicantFactsDto(
            app.Facts.ApplicantId,
            app.Facts.FullName,
            app.Facts.SyntheticId,
            app.Facts.MonthlyGrossIncome.Amount,
            app.Facts.MonthlyDebts.Amount,
            app.Facts.RequestedLoanAmount.Amount,
            app.Facts.EstimatedPropertyValue.Amount,
            app.Facts.CreditScore,
            app.Facts.EmploymentStatus,
            app.Facts.LoanPurpose,
            app.Facts.IsIdentityVerified,
            app.Facts.IsIncomeVerified,
            app.Facts.IsCreditVerified);

        var indDto = app.Indicators != null ? new EligibilityIndicatorsDto(
            app.Indicators.DebtToIncomeRatio,
            app.Indicators.LoanToValueRatio,
            app.Indicators.IsDtiEligible,
            app.Indicators.IsLtvEligible,
            app.Indicators.IsCreditScoreEligible,
            app.Indicators.IsIncomeThresholdEligible,
            app.Indicators.IsLoanAmountEligible,
            app.Indicators.Status,
            app.Indicators.UnmetConditions) : null;

        var recDto = app.CurrentRecommendation != null ? new RecommendationDto(
            app.CurrentRecommendation.RecommendationId,
            app.CurrentRecommendation.DecisionRecommendation,
            app.CurrentRecommendation.Status,
            app.CurrentRecommendation.RiskScore,
            app.CurrentRecommendation.SummaryReasoning,
            app.CurrentRecommendation.Citations.Select(c => new CitationDto(c.DocumentTitle, c.PolicyVersion, c.SectionOrPage, c.Excerpt)).ToList(),
            app.CurrentRecommendation.PolicyViolations,
            app.CurrentRecommendation.MissingEvidenceItems,
            app.CurrentRecommendation.AuditTrail.Select(a => new AuditEntryDto(a.Action, a.PerformedBy, a.TimestampUtc, a.Notes)).ToList(),
            app.CurrentRecommendation.ApprovedByOfficerId,
            app.CurrentRecommendation.ApprovedAtUtc,
            app.CurrentRecommendation.OfficerDecisionNotes) : null;

        return new LoanApplicationDto(
            app.ApplicationId,
            app.ApplicantId,
            app.ProductId,
            app.Status,
            factsDto,
            indDto,
            recDto,
            app.CreatedAtUtc,
            app.UpdatedAtUtc);
    }
}
