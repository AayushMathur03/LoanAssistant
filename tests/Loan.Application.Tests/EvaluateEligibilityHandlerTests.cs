using Loan.Application.Abstractions;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Eligibility;
using Loan.Domain.Products;

namespace Loan.Application.Tests;

[TestFixture]
public class EvaluateEligibilityHandlerTests
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

    private class FakeIdentityReader : IIdentityReader
    {
        public Task<IdentityVerificationResult> VerifyIdentityAsync(string syntheticId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IdentityVerificationResult(syntheticId, true, "Alice Cooper", "Verified via Synthetic DB", DateTime.UtcNow));
        }
    }

    private class FakeIncomeReader : IIncomeReader
    {
        public Task<IncomeVerificationResult> VerifyIncomeAsync(string syntheticId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IncomeVerificationResult(syntheticId, true, new Money(12000m), "Acme Corp", DateTime.UtcNow));
        }
    }

    private class FakeCreditReader : ICreditReader
    {
        public Task<CreditVerificationResult> GetCreditScoreAsync(string syntheticId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CreditVerificationResult(syntheticId, true, 750, "Experian Synthetic", DateTime.UtcNow));
        }
    }

    [Test]
    public async Task HandleAsync_ValidApplication_ShouldEvaluateAndPersistIndicators()
    {
        var repo = new InMemoryApplicationRepository();
        var rules = ProductRules.CreateStandardMortgage("v1.0");
        var facts = new ApplicantFacts(
            "APP-100", "Alice Cooper", "SYN-888777",
            new Money(12000m), new Money(3000m), new Money(350000m), new Money(500000m),
            750, "Full-Time", "Primary Residence");

        var app = new LoanApplication("APP-2026-001", "APP-100", rules, facts, DateTime.UtcNow);
        await repo.AddAsync(app);

        var handler = new EvaluateEligibilityCommandHandler(repo, new FakeIdentityReader(), new FakeIncomeReader(), new FakeCreditReader());
        var result = await handler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-001"));

        Assert.That(result.Status, Is.EqualTo(EligibilityStatus.Eligible));
        Assert.That(result.IsDtiEligible, Is.True);
        Assert.That(result.IsLtvEligible, Is.True);
        Assert.That(result.IsCreditScoreEligible, Is.True);

        var updatedApp = await repo.GetByIdAsync("APP-2026-001");
        Assert.That(updatedApp!.Status, Is.EqualTo(ApplicationStatus.UnderVerification));
        Assert.That(updatedApp.Facts.IsIdentityVerified, Is.True);
        Assert.That(updatedApp.Facts.IsIncomeVerified, Is.True);
        Assert.That(updatedApp.Facts.IsCreditVerified, Is.True);
    }
}
