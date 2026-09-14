using Loan.Domain.Common;
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
}
