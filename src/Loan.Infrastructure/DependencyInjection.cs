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

        return services;
    }

    public static async Task ApplyInfrastructureMigrationsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoanDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
