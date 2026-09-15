using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Infrastructure.Search;
using Loan.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Loan.EndToEndTests;

[TestFixture]
public class HealthControllerTests
{
    private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LoanAssistantTestDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    private LoanDbContext _dbContext = null!;
    private IConfiguration _configuration = null!;
    private HealthController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer(TestConnectionString, b => b.MigrationsAssembly("Loan.Infrastructure"))
            .Options;

        _dbContext = new LoanDbContext(options);
        _dbContext.Database.EnsureCreated();

        var inMemorySettings = new Dictionary<string, string?>
        {
            { "AzureOpenAI:Endpoint", "https://test-openai.openai.azure.com/" },
            { "AzureOpenAI:ApiKey", "fake-key-123" },
            { "AzureAISearch:Endpoint", "https://test-search.search.windows.net" },
            { "AzureAISearch:ApiKey", "fake-search-key" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var chatModel = new SyntheticChatModel();
        var policyRetriever = new SyntheticPolicyRetriever();

        _controller = new HealthController(_dbContext, chatModel, policyRetriever, _configuration);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    [Test]
    public void GetLiveness_ShouldReturn200OK_WithLivenessStatus()
    {
        var result = _controller.GetLiveness() as OkObjectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));

        var response = result.Value as HealthStatusResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo("Healthy"));
        Assert.That(response.Type, Is.EqualTo("Liveness"));
    }

    [Test]
    public async Task GetReadiness_WhenDatabaseIsHealthy_ShouldReturn200OK()
    {
        var result = await _controller.GetReadiness(CancellationToken.None) as OkObjectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));

        var response = result.Value as HealthStatusResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo("Healthy"));
        Assert.That(response.Type, Is.EqualTo("Readiness"));
        Assert.That(response.Database, Is.EqualTo("Connected"));
    }

    [Test]
    public async Task GetReadiness_WhenDatabaseFails_ShouldReturn503Unhealthy()
    {
        var invalidOptions = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer("Data Source=invalid_server_xyz_12345;Initial Catalog=NonExistent;Connect Timeout=1;")
            .Options;
        using var invalidDb = new LoanDbContext(invalidOptions);
        var chatModel = new SyntheticChatModel();
        var policyRetriever = new SyntheticPolicyRetriever();
        var failingController = new HealthController(invalidDb, chatModel, policyRetriever, _configuration);

        var result = await failingController.GetReadiness(CancellationToken.None) as ObjectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(503));

        var response = result.Value as HealthStatusResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo("Unhealthy"));
        Assert.That(response.Type, Is.EqualTo("Readiness"));
    }

    [Test]
    public async Task GetReadiness_WhenCancelled_ShouldReturn503Unhealthy()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _controller.GetReadiness(cts.Token) as ObjectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(503));

        var response = result.Value as HealthStatusResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo("Unhealthy"));
        Assert.That(response.Type, Is.EqualTo("Readiness"));
    }

    [Test]
    public async Task GetDetails_WhenAllDependenciesConfigured_ShouldReturn200Healthy()
    {
        var result = await _controller.GetDetails(CancellationToken.None) as ObjectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200));

        var response = result.Value as HealthStatusResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo("Healthy"));
        Assert.That(response.Type, Is.EqualTo("DetailedDependencies"));
        Assert.That(response.Dependencies, Is.Not.Null);
        Assert.That(response.Dependencies!.ContainsKey("database"), Is.True);
        Assert.That(response.Dependencies!["database"].Status, Is.EqualTo("Healthy"));
        Assert.That(response.Dependencies!["azureOpenAi"].Status, Is.EqualTo("Healthy"));
        Assert.That(response.Dependencies!["azureAiSearch"].Status, Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task GetDetails_WhenAiServicesUnconfigured_ShouldReturn200Degraded()
    {
        var emptyConfig = new ConfigurationBuilder().Build();
        var chatModel = new SyntheticChatModel();
        var policyRetriever = new SyntheticPolicyRetriever();
        var degradedController = new HealthController(_dbContext, chatModel, policyRetriever, emptyConfig);

        var result = await degradedController.GetDetails(CancellationToken.None) as ObjectResult;

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.StatusCode, Is.EqualTo(200)); // Degraded still returns 200 for process availability

        var response = result.Value as HealthStatusResponse;
        Assert.That(response, Is.Not.Null);
        Assert.That(response!.Status, Is.EqualTo("Degraded"));
        Assert.That(response.Type, Is.EqualTo("DetailedDependencies"));
        Assert.That(response.Dependencies!["azureOpenAi"].Status, Is.EqualTo("Degraded"));
        Assert.That(response.Dependencies!["azureAiSearch"].Status, Is.EqualTo("Degraded"));
    }
}
