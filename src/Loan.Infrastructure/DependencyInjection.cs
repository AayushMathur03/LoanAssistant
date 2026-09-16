using Loan.Application.Abstractions;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
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

        // Register Structured Telemetry Collector
        services.AddSingleton<ITelemetryCollector, Telemetry.InMemoryTelemetryCollector>();

        // Register Azure OpenAI Chat Model
        services.AddSingleton<IChatModel, AzureOpenAI.AzureOpenAIChatModel>();

        // Register Document Storage & Extractor (Azure Blob Storage + GPT-4o LLM Extractor with fallback)
        services.AddSingleton<IDocumentStorageService, Documents.AzureBlobDocumentStorageService>();
        services.AddScoped<Documents.SyntheticDocumentExtractor>();
        services.AddScoped<IDocumentExtractor, Documents.AzureOpenAiDocumentExtractor>();

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

        // Register Semantic Kernel In-Process Plugins & Service
        services.AddScoped<SemanticKernel.Plugins.IdentityPlugin>();
        services.AddScoped<SemanticKernel.Plugins.CreditPlugin>();
        services.AddScoped<SemanticKernel.Plugins.PolicySearchPlugin>();
        services.AddScoped<SemanticKernel.Plugins.DraftSaverPlugin>();
        services.AddScoped<SemanticKernel.SemanticKernelAgentService>();

        // Register Slice 7 Multi-Agent Specialist Framework
        services.AddScoped<Application.Agents.DocumentAnalysisAgent>();
        services.AddScoped<Application.Agents.EligibilityAnalysisAgent>();
        services.AddScoped<Application.Agents.ComplianceReviewAgent>();
        services.AddScoped<Application.Agents.RecommendationOrchestratorAgent>();

        return services;
    }

    public static async Task ApplyInfrastructureMigrationsAsync(this IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LoanDbContext>();

    await dbContext.Database.MigrateAsync();
}

    public static async Task SeedIdentityUsersAndRolesAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();

        string[] roles = ["Applicant", "LoanOfficer", "ComplianceReviewer", "Administrator"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(role));
            }
        }

        var demoUsers = new (string Email, string Password, string FullName, string Role, string? LinkedAppId)[]
        {
            ("applicant@apex.local", "Applicant123!", "Alice Cooper", "Applicant", "APP-2026-001"),
            ("officer@apex.local", "Officer123!", "Marcus Vance (Loan Officer)", "LoanOfficer", null),
            ("compliance@apex.local", "Compliance123!", "Sarah Jenkins (Compliance)", "ComplianceReviewer", null),
            ("admin@apex.local", "Admin123!", "System Administrator", "Administrator", null)
        };

        foreach (var (email, password, fullName, role, linkedAppId) in demoUsers)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    LinkedApplicationId = linkedAppId,
                    EmailConfirmed = true
                };
                var createResult = await userManager.CreateAsync(user, password);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                }
            }
            else
            {
                if (!await userManager.IsInRoleAsync(user, role))
                {
                    await userManager.AddToRoleAsync(user, role);
                }
                if (linkedAppId != null && user.LinkedApplicationId != linkedAppId)
                {
                    user.LinkedApplicationId = linkedAppId;
                    await userManager.UpdateAsync(user);
                }
            }
        }
    }
}
