using Loan.Application.Abstractions;
using Loan.Infrastructure.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Loan.IntegrationTests;

[TestFixture]
public class AzureAiSearchIntegrationTests
{
    private IConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../Loan.Web");
        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        _configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("LoanAssistant-Web-87612345-EF89")
            .AddEnvironmentVariables()
            .Build();
    }

    private static string GetSeedPoliciesDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && (!Directory.Exists(Path.Combine(dir.FullName, "src")) || !File.Exists(Path.Combine(dir.FullName, "LoanAssistant.slnx"))))
        {
            dir = dir.Parent;
        }

        if (dir != null)
        {
            var seedPath = Path.Combine(dir.FullName, "src", "Loan.Infrastructure", "Search", "SeedPolicies");
            if (Directory.Exists(seedPath))
                return seedPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "Search", "SeedPolicies");
    }

    [Test]
    public async Task LiveAzureAISearch_IndexingAndEffectiveVersionSearch_ShouldSelectV2OverV1()
    {
        var searchEndpoint = _configuration["AzureAISearch:Endpoint"];
        var searchApiKey = _configuration["AzureAISearch:ApiKey"];
        var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
        var openAiApiKey = _configuration["AzureOpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(searchEndpoint) || string.IsNullOrWhiteSpace(searchApiKey) ||
            string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiApiKey) ||
            searchEndpoint.Contains("YOUR-SEARCH-SERVICE", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("Azure AI Search or Azure OpenAI credentials not configured in User Secrets. Skipping live Azure AI Search integration test.");
            return;
        }

        // 1. Run Indexer
        var indexerLogger = NullLogger<PolicyIndexer>.Instance;
        var indexer = new PolicyIndexer(_configuration, indexerLogger);

        var seedDir = GetSeedPoliciesDirectory();
        Assert.That(Directory.Exists(seedDir), Is.True, $"SeedPolicies directory should exist at '{seedDir}'.");

        await indexer.SynchronizeIndexAndSeedAsync(seedDir);

        // Allow Azure AI Search 3 seconds for index partition creation and replica synchronization
        await Task.Delay(3000);

        // 2. Perform Active/Effective Hybrid Search
        var retrieverLogger = NullLogger<AzureAiSearchPolicyRetriever>.Instance;
        var retriever = new AzureAiSearchPolicyRetriever(_configuration, retrieverLogger);

        var results = (await retriever.SearchPolicyAsync("Personal loan maximum principal and DTI ratio", targetProductId: "LOAN-PERSONAL")).ToList();

        // 3. Verify Active Version Selection (v2.0 active, v1.0 expired)
        Assert.That(results, Is.Not.Null.And.Not.Empty, "Expected Azure AI Search to return policy matches for Personal Loan.");
        Assert.That(results[0].Version, Is.EqualTo("v2.0"));
        Assert.That(results[0].Citation, Is.Not.Null);
        Assert.That(results[0].Citation.PolicyVersion, Is.EqualTo("v2.0"));
    }

    [Test]
    public void AzureAiSearchPolicyRetriever_MissingCredentials_ShouldThrowExplicitException()
    {
        var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureAISearch:Endpoint"] = "",
            ["AzureAISearch:ApiKey"] = ""
        }).Build();

        var logger = NullLogger<AzureAiSearchPolicyRetriever>.Instance;
        var retriever = new AzureAiSearchPolicyRetriever(emptyConfig, logger);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await retriever.SearchPolicyAsync("Maximum DTI ratio for residential mortgage"));

        Assert.That(ex!.Message, Does.Contain("Azure AI Search or Azure OpenAI configuration is incomplete"));
    }

    [Test]
    public async Task LiveAzureAISearch_InsufficientEvidence_ShouldReturnNoIrrelevantMatches()
    {
        var searchEndpoint = _configuration["AzureAISearch:Endpoint"];
        var searchApiKey = _configuration["AzureAISearch:ApiKey"];
        var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
        var openAiApiKey = _configuration["AzureOpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(searchEndpoint) || string.IsNullOrWhiteSpace(searchApiKey) ||
            string.IsNullOrWhiteSpace(openAiEndpoint) || string.IsNullOrWhiteSpace(openAiApiKey))
        {
            Assert.Ignore("Azure credentials missing.");
            return;
        }

        var retriever = new AzureAiSearchPolicyRetriever(_configuration, NullLogger<AzureAiSearchPolicyRetriever>.Instance);

        // Query for a non-existent policy topic (e.g., Spacecraft Lunar Financing Policy)
        var results = (await retriever.SearchPolicyAsync("Quantum Spacecraft Orbital Landing Insurance Policy", targetProductId: "LOAN-PERSONAL")).ToList();

        // High similarity filters or relevant matches should return empty or low irrelevant scores
        Assert.That(results.Count, Is.LessThanOrEqualTo(1));
    }
}
