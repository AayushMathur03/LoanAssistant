using Loan.Application.Abstractions;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Eligibility;
using Loan.Domain.Products;
using Loan.Infrastructure.Verification;
using NUnit.Framework;

namespace Loan.Application.Tests;

[TestFixture]
public class SyntheticApplicationScenarioTests
{
    private class InMemoryAppRepo : ILoanApplicationRepository
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

    private InMemoryAppRepo _appRepo = null!;
    private EvaluateEligibilityCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _appRepo = new InMemoryAppRepo();
        var identityService = new SyntheticIdentityService();
        var incomeService = new SyntheticIncomeService();
        var creditService = new SyntheticCreditService();

        _handler = new EvaluateEligibilityCommandHandler(_appRepo, identityService, incomeService, creditService);
    }

    [TestCase("SYN-888777", "MORTGAGE", 12000, 2500, 350000, 500000, 750, EligibilityStatus.Eligible)]
    [TestCase("SYN-123456", "MORTGAGE", 10000, 2000, 250000, 400000, 720, EligibilityStatus.Eligible)]
    [TestCase("SYN-654321", "MORTGAGE", 10000, 2500, 300000, 450000, 610, EligibilityStatus.Ineligible)] // Credit 610 < 640
    [TestCase("SYN-100200", "MORTGAGE", 15000, 3000, 400000, 600000, 780, EligibilityStatus.Eligible)]
    [TestCase("SYN-200300", "PERSONAL", 9500, 1500, 25000, 0, 740, EligibilityStatus.Eligible)]
    [TestCase("SYN-300400", "MORTGAGE", 11000, 5200, 350000, 500000, 690, EligibilityStatus.ReferToHuman)] // DTI 47.3% > 43%
    [TestCase("SYN-400500", "MORTGAGE", 13500, 2800, 500000, 750000, 810, EligibilityStatus.Eligible)]
    [TestCase("SYN-500600", "PERSONAL", 8800, 3800, 40000, 0, 640, EligibilityStatus.ReferToHuman)] // DTI 43.2% > 38% for Personal Loan v2.0
    [TestCase("SYN-600700", "MORTGAGE", 14200, 4400, 350000, 500000, 760, EligibilityStatus.Eligible)]
    [TestCase("SYN-700800", "MORTGAGE", 9200, 2000, 450000, 500000, 700, EligibilityStatus.ReferToHuman)] // LTV 90% > 80%
    [TestCase("SYN-800900", "PERSONAL", 10500, 2200, 30000, 0, 730, EligibilityStatus.Eligible)]
    [TestCase("SYN-000000", "MORTGAGE", 0, 0, 0, 0, 520, EligibilityStatus.PendingInformation)] // Unverified Identity/Income
    public async Task EvaluateEligibility_All12CanonicalSyntheticScenarios_ReturnExpectedStatus(
        string syntheticId,
        string productType,
        decimal income,
        decimal debts,
        decimal loanAmount,
        decimal propValue,
        int creditScore,
        EligibilityStatus expectedStatus)
    {
        var rules = productType == "MORTGAGE" 
            ? ProductRules.CreateStandardMortgage("v1.2") 
            : ProductRules.CreatePersonalLoan("v2.0");

        var facts = new ApplicantFacts(
            "APP-100", "Synthetic Test User", syntheticId,
            new Money(income), new Money(debts),
            new Money(loanAmount), new Money(propValue),
            creditScore, "Employed", "Primary Residence");

        var appId = $"APP-{syntheticId}";
        var app = new LoanApplication(appId, "APP-100", rules, facts, DateTime.UtcNow);

        await _appRepo.AddAsync(app);

        var result = await _handler.HandleAsync(new EvaluateEligibilityCommand(appId));

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo(expectedStatus), $"Scenario '{syntheticId}' failed expected eligibility status.");
    }
}
