using Loan.Application.ProductAdvice;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Loan.PromptTests;

[TestFixture]
public class PromptEvaluationTests
{
    private AskProductQuestionQueryHandler _handler = null!;
    private SyntheticPolicyRetriever _policyRetriever = null!;
    private SyntheticChatModel _chatModel = null!;
    private InMemoryTelemetryCollector _telemetryCollector = null!;
    private IConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        _policyRetriever = new SyntheticPolicyRetriever();
        _chatModel = new SyntheticChatModel();
        _telemetryCollector = new InMemoryTelemetryCollector();
        _handler = new AskProductQuestionQueryHandler(_policyRetriever, _chatModel);

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

    [TestCaseSource(typeof(PromptEvaluationDataset), nameof(PromptEvaluationDataset.GetTestCases))]
    public async Task EvaluatePromptTestCase(PromptTestCase testCase)
    {
        var query = new AskProductQuestionQuery(testCase.UserPrompt);
        var response = await _handler.HandleAsync(query);

        Assert.That(response.Answer, Does.Contain(testCase.ExpectedKeyword).IgnoreCase,
            $"Test Case {testCase.CaseId} ({testCase.Type} - {testCase.Category}): Expected answer to contain '{testCase.ExpectedKeyword}'.");

        if (testCase.ShouldHaveCitations)
        {
            Assert.That(response.Citations, Is.Not.Empty,
                $"Test Case {testCase.CaseId}: Expected response to contain policy citations.");
        }

        if (testCase.ShouldIncludeDisclaimer)
        {
            Assert.That(response.NonApprovalDisclaimer, Is.Not.Empty,
                $"Test Case {testCase.CaseId}: Expected non-approval disclaimer.");
        }
    }

    [Test]
    public async Task RunFullEvaluationSuite_AndGenerateAuditableReports()
    {
        // 1. Run Offline Deterministic Evaluation
        var offlineRunner = new EvaluationRunner(_policyRetriever, _chatModel, _telemetryCollector, "Offline Deterministic Test Suite", "SyntheticChatModel");
        var offlineSummary = await offlineRunner.RunEvaluationAsync();

        // 2. Check for Live Azure AI Configuration & Execute Live Suite if Credentials Present
        EvaluationSummary? liveSummary = null;

        var searchEndpoint = _configuration["AzureAISearch:Endpoint"];
        var searchApiKey = _configuration["AzureAISearch:ApiKey"];
        var openAiEndpoint = _configuration["AzureOpenAI:Endpoint"];
        var openAiApiKey = _configuration["AzureOpenAI:ApiKey"];

        var hasLiveSearch = !string.IsNullOrWhiteSpace(searchEndpoint) && !string.IsNullOrWhiteSpace(searchApiKey) && !searchEndpoint.Contains("YOUR-SEARCH-SERVICE", StringComparison.OrdinalIgnoreCase);
        var hasLiveOpenAi = !string.IsNullOrWhiteSpace(openAiEndpoint) && !string.IsNullOrWhiteSpace(openAiApiKey) && !openAiEndpoint.Contains("YOUR-RESOURCE-NAME", StringComparison.OrdinalIgnoreCase);

        if (hasLiveSearch && hasLiveOpenAi)
        {
            var indexer = new PolicyIndexer(_configuration, NullLogger<PolicyIndexer>.Instance);
            var seedDir = Path.Combine(GetSolutionDirectory(), "src", "Loan.Infrastructure", "Search", "SeedPolicies");
            await indexer.SynchronizeIndexAndSeedAsync(seedDir);
            await Task.Delay(3000);

            var liveTelemetry = new InMemoryTelemetryCollector();
            var liveRetriever = new AzureAiSearchPolicyRetriever(_configuration, NullLogger<AzureAiSearchPolicyRetriever>.Instance);
            var liveChatModel = new AzureOpenAIChatModel(_configuration, liveTelemetry);

            var liveRunner = new EvaluationRunner(
                liveRetriever,
                liveChatModel,
                liveTelemetry,
                "Live Azure AI Evaluation Suite",
                $"AzureOpenAIChatModel ({_configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o"})");

            liveSummary = await liveRunner.RunEvaluationAsync();
        }

        // 3. Save Reports in Solution Docs Directory
        var solutionDir = GetSolutionDirectory();
        var docsDir = Path.Combine(solutionDir, "docs");
        await EvaluationRunner.GenerateReportsAsync(offlineSummary, liveSummary, docsDir);

        // 4. Assert Evaluation Benchmarks
        Assert.That(offlineSummary.TotalPrompts, Is.EqualTo(20), "Expected exactly 20 evaluation prompts in suite.");
        Assert.That(offlineSummary.GoldenPrompts, Is.EqualTo(15), "Expected exactly 15 Golden prompts.");
        Assert.That(offlineSummary.AdversarialPrompts, Is.EqualTo(5), "Expected exactly 5 Adversarial prompts.");
        Assert.That(offlineSummary.TotalPassed, Is.EqualTo(20), "Expected all 20 prompts to pass offline evaluation benchmarks.");
        Assert.That(offlineSummary.OverallPassRatePercentage, Is.EqualTo(100.0), "Expected 100% offline pass rate across 20 prompts.");

        if (liveSummary != null)
        {
            Assert.That(liveSummary.TotalPrompts, Is.EqualTo(20), "Expected 20 prompts in live evaluation suite.");
            Assert.That(liveSummary.GoldenPassed, Is.EqualTo(15), "Expected all 15 Golden prompts to pass in live evaluation.");
            Assert.That(liveSummary.TotalPassed, Is.EqualTo(20), "Expected 100% pass rate in live evaluation suite.");
        }

        Assert.That(File.Exists(Path.Combine(docsDir, "evaluation_results.json")), Is.True, "JSON evaluation results file should exist.");
        Assert.That(File.Exists(Path.Combine(docsDir, "evaluation_report.md")), Is.True, "Markdown evaluation report file should exist.");
    }

    private static string GetSolutionDirectory()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && (!Directory.Exists(Path.Combine(dir.FullName, "src")) || !File.Exists(Path.Combine(dir.FullName, "LoanAssistant.slnx"))))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? Directory.GetCurrentDirectory();
    }
}
