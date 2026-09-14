using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Loan.Infrastructure.Persistence.DbContext;

public class DesignTimeLoanDbContextFactory : IDesignTimeDbContextFactory<LoanDbContext>
{
    public LoanDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../Loan.Web");
        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LoanAssistantDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

        var builder = new DbContextOptionsBuilder<LoanDbContext>();
        builder.UseSqlServer(connectionString, b => b.MigrationsAssembly("Loan.Infrastructure"));

        return new LoanDbContext(builder.Options);
    }
}
