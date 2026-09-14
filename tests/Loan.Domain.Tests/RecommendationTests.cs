using Loan.Domain.Common;
using Loan.Domain.Recommendations;

namespace Loan.Domain.Tests;

[TestFixture]
public class RecommendationTests
{
    [Test]
    public void Recommendation_WithMissingEvidence_ShouldForcePendingInformationType()
    {
        var citations = new List<RecommendationCitation>
        {
            new("Mortgage Product Guide", "v1.0", "Section 3.2", "Income verification required")
        };

        var missingItems = new List<string> { "Verified Payslip Document" };

        var rec = new Recommendation(
            recommendationId: "REC-001",
            decisionRecommendation: RecommendationType.Approve, // System attempted Approve
            riskScore: 0.15,
            summaryReasoning: "Strong credit score and low DTI ratio.",
            citations: citations,
            policyViolations: Array.Empty<string>(),
            missingEvidenceItems: missingItems,
            createdAtUtc: DateTime.UtcNow
        );

        // BR-06 Enforcement
        Assert.That(rec.DecisionRecommendation, Is.EqualTo(RecommendationType.PendingInformation));
        Assert.That(rec.Status, Is.EqualTo(RecommendationStatus.DraftPreparedBySystem));
    }

    [Test]
    public void Recommendation_ApproveByOfficer_ShouldUpdateStatusAndRecordAuditTrail()
    {
        var rec = new Recommendation(
            recommendationId: "REC-002",
            decisionRecommendation: RecommendationType.Approve,
            riskScore: 0.10,
            summaryReasoning: "Meets all criteria.",
            citations: Array.Empty<RecommendationCitation>(),
            policyViolations: Array.Empty<string>(),
            missingEvidenceItems: Array.Empty<string>(),
            createdAtUtc: DateTime.UtcNow
        );

        var now = DateTime.UtcNow;
        rec.ApproveByOfficer("OFFICER-42", "Approved after manual review of tax documents.", now);

        // BR-07 Enforcement
        Assert.That(rec.Status, Is.EqualTo(RecommendationStatus.ApprovedByOfficer));
        Assert.That(rec.ApprovedByOfficerId, Is.EqualTo("OFFICER-42"));
        Assert.That(rec.OfficerDecisionNotes, Is.EqualTo("Approved after manual review of tax documents."));
        Assert.That(rec.AuditTrail.Count, Is.EqualTo(2)); // DraftPrepared + OfficerApproved
        Assert.That(rec.AuditTrail.Last().Action, Is.EqualTo("OfficerApproved"));
    }
}
