using Loan.Application.Recommendations;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Eligibility;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;
using Loan.Infrastructure.Persistence;
using Loan.Infrastructure.Verification;

namespace Loan.IntegrationTests;

[TestFixture]
public class EndToEndPipelineIntegrationTests
{
    [Test]
    public async Task FullPipeline_FromIntakeToOfficerApproval_ShouldSucceed()
    {
        // 1. Arrange Infrastructure Repositories and Synthetic Verification Services
        var appRepo = new InMemoryLoanApplicationRepository();
        var recRepo = new InMemoryRecommendationRepository();
        var identityService = new SyntheticIdentityService();
        var incomeService = new SyntheticIncomeService();
        var creditService = new SyntheticCreditService();

        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts(
            applicantId: "APP-INT-001",
            fullName: "Alice Cooper",
            syntheticId: "SYN-888777",
            monthlyGrossIncome: new Money(12000m),
            monthlyDebts: new Money(3000m),
            requestedLoanAmount: new Money(350000m),
            estimatedPropertyValue: new Money(500000m),
            creditScore: 750,
            employmentStatus: "Full-Time",
            loanPurpose: "Primary Residence");

        var app = new LoanApplication("APP-2026-INT", "APP-INT-001", rules, facts, DateTime.UtcNow);
        app.Submit(DateTime.UtcNow);
        await appRepo.AddAsync(app);

        // 2. Execute Verification & Eligibility Evaluation Handler
        var evalHandler = new EvaluateEligibilityCommandHandler(appRepo, identityService, incomeService, creditService);
        var evalResult = await evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-INT"));

        Assert.That(evalResult.Status, Is.EqualTo(EligibilityStatus.Eligible));

        // 3. Generate Recommendation Draft
        var draftHandler = new GenerateRecommendationDraftCommandHandler(appRepo, recRepo);
        var recDto = await draftHandler.HandleAsync(new GenerateRecommendationDraftCommand("APP-2026-INT"));

        Assert.That(recDto.DecisionRecommendation, Is.EqualTo(RecommendationType.Approve));
        Assert.That(recDto.Citations, Is.Not.Empty);

        // 4. Officer Decision Command
        var officerHandler = new OfficerDecisionCommandHandler(appRepo);
        var finalApp = await officerHandler.HandleAsync(new OfficerDecisionCommand(
            "APP-2026-INT",
            "OFFICER-007",
            RecommendationStatus.ApprovedByOfficer,
            "All financial verification documents confirmed. Approved."));

        Assert.That(finalApp.Status, Is.EqualTo(ApplicationStatus.Approved));
        Assert.That(finalApp.CurrentRecommendation!.ApprovedByOfficerId, Is.EqualTo("OFFICER-007"));
        Assert.That(finalApp.CurrentRecommendation.Status, Is.EqualTo(RecommendationStatus.ApprovedByOfficer));
    }
}
