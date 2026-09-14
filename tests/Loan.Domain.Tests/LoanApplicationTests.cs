using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;

namespace Loan.Domain.Tests;

[TestFixture]
public class LoanApplicationTests
{
    private ProductRules _rules = null!;
    private ApplicantFacts _facts = null!;

    [SetUp]
    public void SetUp()
    {
        _rules = ProductRules.CreateStandardMortgage("v1.0");
        _facts = new ApplicantFacts(
            applicantId: "APP-100",
            fullName: "Alice Cooper",
            syntheticId: "SYN-888777",
            monthlyGrossIncome: new Money(12000m),
            monthlyDebts: new Money(3000m),
            requestedLoanAmount: new Money(350000m),
            estimatedPropertyValue: new Money(500000m),
            creditScore: 750,
            employmentStatus: "Full-Time",
            loanPurpose: "Primary Residence"
        );
    }

    [Test]
    public void Application_Lifecycle_SubmitAndEvaluate_ShouldProgressState()
    {
        var now = DateTime.UtcNow;
        var app = new LoanApplication("APP-2026-001", "APP-100", _rules, _facts, now);

        Assert.That(app.Status, Is.EqualTo(ApplicationStatus.Draft));

        app.Submit(now);
        Assert.That(app.Status, Is.EqualTo(ApplicationStatus.Submitted));

        app.EvaluateEligibility(now);
        Assert.That(app.Status, Is.EqualTo(ApplicationStatus.UnderVerification));
        Assert.That(app.Indicators, Is.Not.Null);

        var recommendation = new Recommendation(
            "REC-100",
            RecommendationType.Approve,
            0.12,
            "Clean credit profile.",
            Array.Empty<RecommendationCitation>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            now
        );

        app.SetRecommendation(recommendation, now);
        Assert.That(app.Status, Is.EqualTo(ApplicationStatus.UnderOfficerReview));

        app.OfficerApprove("OFFICER-99", "All clear", now);
        Assert.That(app.Status, Is.EqualTo(ApplicationStatus.Approved));
    }

    [Test]
    public void Application_OfficerApprove_WithoutRecommendation_ShouldThrowInvalidApplicationStateException()
    {
        var now = DateTime.UtcNow;
        var app = new LoanApplication("APP-2026-002", "APP-100", _rules, _facts, now);

        Assert.Throws<InvalidApplicationStateException>(() => app.OfficerApprove("OFFICER-99", "Notes", now));
    }
}
