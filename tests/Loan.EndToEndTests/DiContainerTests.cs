using Loan.Application.Abstractions;
using Loan.Application.ProductAdvice;
using Loan.Application.Recommendations;
using Loan.Application.Verification;
using Loan.Infrastructure;
using Loan.Infrastructure.MCP;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Loan.EndToEndTests;

[TestFixture]
public class DiContainerTests
{
    [Test]
    public void ServiceContainer_BuildsSuccessfullyWithValidateScopesAndValidateOnBuild()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LoanAssistantDb_Test;Integrated Security=True;" }
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        // Composition extension under test
        services.AddInfrastructurePersistence(config);

        services.AddTransient<AskProductQuestionQueryHandler>();
        services.AddTransient<EvaluateEligibilityCommandHandler>();
        services.AddTransient<GenerateRecommendationDraftCommandHandler>();
        services.AddTransient<OfficerDecisionCommandHandler>();

        var providerOptions = new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        };

        // Must build service provider without throwing scope validation exceptions
        IServiceProvider provider = null!;
        Assert.DoesNotThrow(() =>
        {
            provider = services.BuildServiceProvider(providerOptions);
        });

        // Verify McpToolServer can be resolved inside a request scope along with scoped repository
        using var scope = provider.CreateScope();
        var mcpServer = scope.ServiceProvider.GetService<McpToolServer>();
        var repo = scope.ServiceProvider.GetService<ILoanApplicationRepository>();

        Assert.That(mcpServer, Is.Not.Null);
        Assert.That(repo, Is.Not.Null);
    }

    [Test]
    public async Task McpToolServer_ResolvedFromServiceScope_ExecutesToolWithScopedRepository()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddInfrastructurePersistence(config);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using var scope = provider.CreateScope();
        var mcpServer = scope.ServiceProvider.GetRequiredService<McpToolServer>();
        var repo = scope.ServiceProvider.GetRequiredService<ILoanApplicationRepository>();

        // Seed a sample application into the scoped repository
        var appId = $"APP-DI-{Guid.NewGuid():N}"[..12];
        var rules = Domain.Products.ProductRules.CreateStandardMortgage("v1.2");
        var facts = new Domain.Applications.ApplicantFacts("APP-100", "Alice Cooper", "SYN-888777", new Domain.Common.Money(12000m), new Domain.Common.Money(3000m), new Domain.Common.Money(350000m), new Domain.Common.Money(500000m), 750, "Employed", "Purchase");
        var app = new Domain.Applications.LoanApplication(appId, "APP-100", rules, facts, DateTime.UtcNow);
        await repo.AddAsync(app);

        var request = new JsonRpcRequest("2.0", "tools/call", new
        {
            name = "get_identity_status",
            arguments = new
            {
                applicationId = appId,
                syntheticId = "SYN-888777"
            }
        }, 1);

        var response = await mcpServer.HandleRequestAsync(request, "Actor-1", "SystemWorker");

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Error, Is.Null);
        var result = response.Result as McpToolResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsError, Is.False);
        Assert.That(result.Content[0].Text, Does.Contain("Alice Cooper"));
    }
}
