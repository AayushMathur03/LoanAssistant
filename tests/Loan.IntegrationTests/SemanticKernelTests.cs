using Loan.Application.Abstractions;
using Loan.Domain.Applications;
using Loan.Domain.Recommendations;
using Loan.Infrastructure.MCP;
using Loan.Infrastructure.SemanticKernel;
using Loan.Infrastructure.SemanticKernel.Plugins;
using Loan.Infrastructure.Verification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Loan.IntegrationTests;

[TestFixture]
public class SemanticKernelTests
{
    private class DummyAppRepo : ILoanApplicationRepository
    {
        public Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IEnumerable<LoanApplication>> GetByApplicantIdAsync(string applicantId, CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<LoanApplication>());
        public Task<LoanApplication?> GetByIdAsync(string applicationId, CancellationToken cancellationToken = default)
        {
            var rules = Loan.Domain.Products.ProductRules.CreateStandardMortgage("v1.2");
            var facts = new ApplicantFacts(applicationId, "Alice Cooper", "SYN-888777", new(12000m), new(2500m), new(350000m), new(500000m), 750, "Employed", "Purchase");
            var app = new LoanApplication(applicationId, "APPLICANT-1", rules, facts, DateTime.UtcNow);
            return Task.FromResult<LoanApplication?>(app);
        }
        public Task<IEnumerable<LoanApplication>> GetPendingOfficerReviewAsync(CancellationToken cancellationToken = default) => Task.FromResult(Enumerable.Empty<LoanApplication>());
        public Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class DummyRecRepo : IRecommendationRepository
    {
        public Task<Recommendation?> GetByIdAsync(string recommendationId, CancellationToken cancellationToken = default) => Task.FromResult<Recommendation?>(null);
        public Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Test]
    public async Task SemanticKernel_WithPlugins_InitializesAndExecutesPluginsLocally()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureOpenAI:Endpoint"] = "",
            ["AzureOpenAI:ApiKey"] = ""
        }).Build();

        var identityPlugin = new IdentityPlugin(new SyntheticIdentityService());
        var creditPlugin = new CreditPlugin(new SyntheticCreditService());
        var policyPlugin = new PolicySearchPlugin(new Loan.Infrastructure.Search.SyntheticPolicyRetriever());
        var draftHandler = new Loan.Application.Recommendations.SaveRecommendationDraftCommandHandler(new DummyAppRepo(), new DummyRecRepo());
        var draftPlugin = new DraftSaverPlugin(draftHandler);

        var service = new SemanticKernelAgentService(
            config,
            identityPlugin,
            creditPlugin,
            policyPlugin,
            draftPlugin,
            NullLogger<SemanticKernelAgentService>.Instance);

        Assert.That(service.Kernel, Is.Not.Null);
        Assert.That(service.Kernel.Plugins.Count, Is.EqualTo(4));

        // Test calling Plugin through Kernel function
        var identityJson = await identityPlugin.GetIdentityStatusAsync("SYN-888777");
        Assert.That(identityJson, Does.Contain("Alice Cooper"));
        Assert.That(identityJson, Does.Contain("SYN-888777"));

        var creditJson = await creditPlugin.GetCreditStatusAsync("SYN-888777");
        Assert.That(creditJson, Does.Contain("750"));
    }
}
