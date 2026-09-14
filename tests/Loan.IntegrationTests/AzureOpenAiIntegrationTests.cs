using Loan.Application.Abstractions;
using Loan.Infrastructure.AzureOpenAI;
using Microsoft.Extensions.Configuration;

namespace Loan.IntegrationTests;

[TestFixture]
public class AzureOpenAiIntegrationTests
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

    [Test]
    public async Task LiveAzureOpenAI_GenerateCompletion_ShouldReturnRealResponse()
    {
        var endpoint = _configuration["AzureOpenAI:Endpoint"];
        var apiKey = _configuration["AzureOpenAI:ApiKey"];

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) ||
            endpoint.Contains("YOUR-RESOURCE-NAME", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("Azure OpenAI credentials not configured in User Secrets. Skipping live Azure OpenAI integration test.");
            return;
        }

        // Arrange
        var chatModel = new AzureOpenAIChatModel(_configuration);
        var messages = new[]
        {
            new ChatMessage("system", "You are a professional banking compliance assistant. Provide concise, clear answers."),
            new ChatMessage("user", "Explain what a Debt-to-Income (DTI) ratio is in 2 sentences.")
        };

        // Act
        var responseText = await chatModel.GenerateCompletionAsync(messages, temperature: 0.1);

        // Assert
        Assert.That(responseText, Is.Not.Null.And.Not.Empty);
        Assert.That(responseText.ToLowerInvariant(), Does.Contain("debt").Or.Contain("income").Or.Contain("ratio"));
    }

    [Test]
    public void AzureOpenAIChatModel_MissingCredentials_ShouldThrowExplicitException()
    {
        // Arrange
        var emptyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureOpenAI:Endpoint"] = "https://YOUR-RESOURCE-NAME.openai.azure.com/",
            ["AzureOpenAI:ApiKey"] = ""
        }).Build();

        var model = new AzureOpenAIChatModel(emptyConfig);

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await model.GenerateCompletionAsync(new[] { new ChatMessage("user", "Hello") }));

        Assert.That(ex!.Message, Does.Contain("Azure OpenAI configuration is incomplete"));
    }
}
