using Loan.Application.Abstractions;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Loan.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructurePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LoanAssistantDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

        services.AddDbContext<LoanDbContext>(options =>
        {
            options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(LoanDbContext).Assembly.FullName));
        });

        services.AddScoped<ILoanApplicationRepository, SqlLoanApplicationRepository>();
        services.AddScoped<IRecommendationRepository, SqlRecommendationRepository>();

        // Register Azure OpenAI Chat Model
        services.AddSingleton<IChatModel, AzureOpenAI.AzureOpenAIChatModel>();

        // Register Document Storage & Extractor
        services.AddSingleton<IDocumentStorageService, Documents.LocalFileDocumentStorageService>();
        services.AddScoped<IDocumentExtractor, Documents.SyntheticDocumentExtractor>();

        // Register Slice 4 CQRS Command Handlers
        services.AddScoped<Application.Documents.UploadAndExtractDocumentCommandHandler>();
        services.AddScoped<Application.Documents.ConfirmOrOverrideExtractedFieldsCommandHandler>();

        // Register Policy Retriever (Hybrid Azure AI Search)
        services.AddSingleton<Search.SyntheticPolicyRetriever>();
        services.AddScoped<IPolicyRetriever, Search.AzureAiSearchPolicyRetriever>();
        services.AddScoped<Search.PolicyIndexer>();

        // Register Slice 5 Synthetic Verification Services
        services.AddSingleton<IIdentityReader, Verification.SyntheticIdentityService>();
        services.AddSingleton<IIncomeReader, Verification.SyntheticIncomeService>();
        services.AddSingleton<ICreditReader, Verification.SyntheticCreditService>();

        // Register Slice 5 Command Handlers & MCP Server
        services.AddScoped<Application.Recommendations.SaveRecommendationDraftCommandHandler>();
        services.AddScoped<MCP.McpToolServer>();

        return services;
    }

    public static async Task ApplyInfrastructureMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoanDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
