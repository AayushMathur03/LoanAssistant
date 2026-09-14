using Loan.Application.Abstractions;
using Loan.Application.Recommendations;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;

namespace Loan.Application.Tests;

[TestFixture]
public class OfficerDecisionHandlerTests
{
    private class InMemoryApplicationRepository : ILoanApplicationRepository
    {
        private readonly Dictionary<string, LoanApplication> _apps = new();

        public Task<LoanApplication?> GetByIdAsync(string applicationId, CancellationToken cancellationToken = default)
        {
            _apps.TryGetValue(applicationId, out var app);
            return Task.FromResult(app);
        }

        public Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default)
        {
            _apps[application.ApplicationId] = application;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default)
        {
            _apps[application.ApplicationId] = application;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<LoanApplication>> GetByApplicantIdAsync(string applicantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apps.Values.Where(a => a.ApplicantId == applicantId));
        }

        public Task<IEnumerable<LoanApplication>> GetPendingOfficerReviewAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apps.Values.Where(a => a.Status == ApplicationStatus.UnderOfficerReview));
        }
    }

    [Test]
    public async Task HandleAsync_OfficerApprove_ShouldApproveApplicationAndLogAudit()
    {
        var repo = new InMemoryApplicationRepository();
        var rules = ProductRules.CreateStandardMortgage("v1.0");
        var facts = new ApplicantFacts(
            "APP-100", "Alice Cooper", "SYN-888777",
            new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m),
            750, "Full-Time", "Primary Residence");

        var app = new LoanApplication("APP-2026-001", "APP-100", rules, facts, DateTime.UtcNow);
        app.EvaluateEligibility(DateTime.UtcNow);

        var rec = new Recommendation(
            "REC-001", RecommendationType.Approve, 0.10, "Clean record.",
            Array.Empty<RecommendationCitation>(), Array.Empty<string>(), Array.Empty<string>(), DateTime.UtcNow);
        app.SetRecommendation(rec, DateTime.UtcNow);

        await repo.AddAsync(app);

        var handler = new OfficerDecisionCommandHandler(repo);
        var command = new OfficerDecisionCommand("APP-2026-001", "OFFICER-77", RecommendationStatus.ApprovedByOfficer, "Confirmed income documentation.");

        var result = await handler.HandleAsync(command);

        Assert.That(result.Status, Is.EqualTo(ApplicationStatus.Approved));
        Assert.That(result.CurrentRecommendation!.ApprovedByOfficerId, Is.EqualTo("OFFICER-77"));
        Assert.That(result.CurrentRecommendation.Status, Is.EqualTo(RecommendationStatus.ApprovedByOfficer));
        Assert.That(result.CurrentRecommendation.AuditTrail, Has.Some.Matches<DTOs.AuditEntryDto>(a => a.Action == "OfficerApproved" && a.PerformedBy == "OFFICER-77"));
    }
}
