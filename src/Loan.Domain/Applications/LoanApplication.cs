using Loan.Domain.Common;
using Loan.Domain.Documents;
using Loan.Domain.Eligibility;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;

namespace Loan.Domain.Applications;

public enum ApplicationStatus
{
    Draft,
    Submitted,
    UnderDocumentReview,
    UnderVerification,
    UnderOfficerReview,
    Approved,
    Rejected,
    InformationRequested
}

public class LoanApplication
{
    public string ApplicationId { get; }
    public string ApplicantId { get; }
    public string ProductId { get; }
    public ApplicationStatus Status { get; private set; }

    public ApplicantFacts Facts { get; private set; }
    public ProductRules ProductRules { get; private set; }
    public EligibilityIndicators? Indicators { get; private set; }
    public Recommendation? CurrentRecommendation { get; private set; }

    public IReadOnlyList<ExtractedDocumentRecord> Documents => _documents.AsReadOnly();
    private readonly List<ExtractedDocumentRecord> _documents = new();

    public IReadOnlyList<FieldOverrideAuditEntry> DocumentAuditTrail => _documentAuditTrail.AsReadOnly();
    private readonly List<FieldOverrideAuditEntry> _documentAuditTrail = new();

    public DateTime CreatedAtUtc { get; }
    public DateTime UpdatedAtUtc { get; private set; }

    public LoanApplication(
        string applicationId,
        string applicantId,
        ProductRules productRules,
        ApplicantFacts facts,
        DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(applicationId)) throw new ArgumentException("ApplicationId is required.", nameof(applicationId));
        if (string.IsNullOrWhiteSpace(applicantId)) throw new ArgumentException("ApplicantId is required.", nameof(applicantId));
        ArgumentNullException.ThrowIfNull(productRules);
        ArgumentNullException.ThrowIfNull(facts);

        ApplicationId = applicationId;
        ApplicantId = applicantId;
        ProductId = productRules.ProductId;
        ProductRules = productRules;
        Facts = facts;
        Status = ApplicationStatus.Draft;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public void UpdateFacts(ApplicantFacts updatedFacts, DateTime timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(updatedFacts);
        Facts = updatedFacts;
        UpdatedAtUtc = timestampUtc;
    }

    public void Submit(DateTime timestampUtc)
    {
        if (Status != ApplicationStatus.Draft && Status != ApplicationStatus.InformationRequested)
        {
            throw new InvalidApplicationStateException($"Cannot submit application in '{Status}' state.");
        }

        Status = ApplicationStatus.Submitted;
        UpdatedAtUtc = timestampUtc;
    }

    public void EvaluateEligibility(DateTime timestampUtc)
    {
        Indicators = EligibilityCalculator.Evaluate(Facts, ProductRules, timestampUtc);
        Status = ApplicationStatus.UnderVerification;
        UpdatedAtUtc = timestampUtc;
    }

    public void SetRecommendation(Recommendation recommendation, DateTime timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        CurrentRecommendation = recommendation;
        Status = ApplicationStatus.UnderOfficerReview;
        UpdatedAtUtc = timestampUtc;
    }

    public void OfficerApprove(string officerId, string notes, DateTime timestampUtc)
    {
        if (CurrentRecommendation == null)
        {
            throw new InvalidApplicationStateException("Cannot approve an application without a recommendation draft.");
        }

        CurrentRecommendation.ApproveByOfficer(officerId, notes, timestampUtc);
        Status = ApplicationStatus.Approved;
        UpdatedAtUtc = timestampUtc;
    }

    public void OfficerReject(string officerId, string notes, DateTime timestampUtc)
    {
        if (CurrentRecommendation == null)
        {
            throw new InvalidApplicationStateException("Cannot reject an application without a recommendation draft.");
        }

        CurrentRecommendation.RejectByOfficer(officerId, notes, timestampUtc);
        Status = ApplicationStatus.Rejected;
        UpdatedAtUtc = timestampUtc;
    }

    public void OfficerReturnForInfo(string officerId, string notes, DateTime timestampUtc)
    {
        if (CurrentRecommendation == null)
        {
            throw new InvalidApplicationStateException("Cannot return an application without a recommendation draft.");
        }

        CurrentRecommendation.ReturnForMoreInformation(officerId, notes, timestampUtc);
        Status = ApplicationStatus.InformationRequested;
        UpdatedAtUtc = timestampUtc;
    }

    public void AddDocumentRecord(ExtractedDocumentRecord documentRecord, DateTime timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(documentRecord);
        if (documentRecord.ApplicationId != ApplicationId)
        {
            throw new InvalidOperationException($"Cannot attach document belonging to ApplicationId '{documentRecord.ApplicationId}' to Application '{ApplicationId}'.");
        }

        _documents.Add(documentRecord);
        Status = ApplicationStatus.UnderDocumentReview;
        UpdatedAtUtc = timestampUtc;
    }

    public void ConfirmOrOverrideField(
        string documentId,
        string fieldName,
        string confirmedValue,
        string actorId,
        string actorRole,
        string reason,
        string correlationId,
        DateTime timestampUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        var doc = _documents.FirstOrDefault(d => d.DocumentId == documentId);
        if (doc == null)
        {
            throw new InvalidOperationException($"Document '{documentId}' was not found on Application '{ApplicationId}'.");
        }

        var field = doc.Fields.FirstOrDefault(f => f.FieldName == fieldName);
        if (field == null)
        {
            throw new InvalidOperationException($"Field '{fieldName}' was not found in Document '{documentId}'.");
        }

        string actionType = string.Equals(field.DisplayValue, confirmedValue, StringComparison.OrdinalIgnoreCase) ? "Confirm" : "Override";

        field.ConfirmOrOverride(confirmedValue, actorId, actorRole, timestampUtc);

        var audit = new FieldOverrideAuditEntry(
            auditId: $"AUD-{Guid.NewGuid():N}",
            applicationId: ApplicationId,
            fieldName: fieldName,
            documentId: documentId,
            actorId: actorId,
            actorRole: actorRole,
            timestampUtc: timestampUtc,
            action: actionType,
            reason: string.IsNullOrWhiteSpace(reason) ? "Manual field review" : reason,
            correlationId: string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId);

        _documentAuditTrail.Add(audit);

        // Update Facts dynamically if candidate field affects income/debt
        if (decimal.TryParse(confirmedValue, out var numValue))
        {
            if (fieldName.Equals("MonthlyGrossIncome", StringComparison.OrdinalIgnoreCase) || fieldName.Equals("StatedIncome", StringComparison.OrdinalIgnoreCase))
            {
                Facts = new ApplicantFacts(Facts.ApplicantId, Facts.FullName, Facts.SyntheticId, new Money(numValue), Facts.MonthlyDebts, Facts.RequestedLoanAmount, Facts.EstimatedPropertyValue, Facts.CreditScore, Facts.EmploymentStatus, Facts.LoanPurpose);
            }
            else if (fieldName.Equals("StatedDebts", StringComparison.OrdinalIgnoreCase) || fieldName.Equals("MonthlyDebts", StringComparison.OrdinalIgnoreCase))
            {
                Facts = new ApplicantFacts(Facts.ApplicantId, Facts.FullName, Facts.SyntheticId, Facts.MonthlyGrossIncome, new Money(numValue), Facts.RequestedLoanAmount, Facts.EstimatedPropertyValue, Facts.CreditScore, Facts.EmploymentStatus, Facts.LoanPurpose);
            }
        }

        UpdatedAtUtc = timestampUtc;
    }
}
