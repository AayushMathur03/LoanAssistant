using Loan.Application.Abstractions;
using Loan.Application.ProductAdvice;
using Loan.Application.Recommendations;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Infrastructure;
using Loan.Infrastructure.AzureOpenAI;
using Loan.Infrastructure.Documents;
using Loan.Infrastructure.MCP;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services to the container.
builder.Services.AddControllersWithViews();

// Register Infrastructure Persistence Repositories & Tools via Composition Extension
builder.Services.AddInfrastructurePersistence(builder.Configuration);

// Register Application CQRS Handlers
builder.Services.AddTransient<AskProductQuestionQueryHandler>();
builder.Services.AddTransient<EvaluateEligibilityCommandHandler>();
builder.Services.AddTransient<GenerateRecommendationDraftCommandHandler>();
builder.Services.AddTransient<OfficerDecisionCommandHandler>();

var app = builder.Build();

// Development-only automatic database migration and starter data seeding
if (app.Environment.IsDevelopment())
{
    await EnsureDatabaseAndSeedAsync(app);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<Loan.Web.Middleware.CorrelationIdMiddleware>();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Apply EF Core migrations and seed sample applications for local development
static async Task EnsureDatabaseAndSeedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    
    // Check if automatic startup migration is enabled via configuration (default true in Dev)
    var autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup", true);
    if (autoMigrate)
    {
        await scope.ServiceProvider.ApplyInfrastructureMigrationsAsync();
    }

    var repo = scope.ServiceProvider.GetRequiredService<ILoanApplicationRepository>();

    var existing1 = await repo.GetByIdAsync("APP-2026-001");
    if (existing1 != null) return; // Already seeded in SQL Server

    var evalHandler = scope.ServiceProvider.GetRequiredService<EvaluateEligibilityCommandHandler>();
    var draftHandler = scope.ServiceProvider.GetRequiredService<GenerateRecommendationDraftCommandHandler>();

    var rules = ProductRules.CreateStandardMortgage("v1.2");
    
    // Sample Application 1: Eligible mortgage application
    var facts1 = new ApplicantFacts(
        applicantId: "APP-100",
        fullName: "Alice Cooper",
        syntheticId: "SYN-888777",
        monthlyGrossIncome: new Money(12000m),
        monthlyDebts: new Money(3000m),
        requestedLoanAmount: new Money(350000m),
        estimatedPropertyValue: new Money(500000m),
        creditScore: 750,
        employmentStatus: "Full-Time Senior Engineer",
        loanPurpose: "Primary Residence Purchase");

    var app1 = new LoanApplication("APP-2026-001", "APP-100", rules, facts1, DateTime.UtcNow.AddHours(-12));
    app1.Submit(DateTime.UtcNow.AddHours(-10));
    await repo.AddAsync(app1);
    await evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-001"));
    await draftHandler.HandleAsync(new GenerateRecommendationDraftCommand("APP-2026-001"));

    // Sample Application 2: High DTI application requiring human officer review
    var facts2 = new ApplicantFacts(
        applicantId: "APP-101",
        fullName: "Jane Smith",
        syntheticId: "SYN-654321",
        monthlyGrossIncome: new Money(10000m),
        monthlyDebts: new Money(5200m), // High DTI (52% > 43% max)
        requestedLoanAmount: new Money(400000m),
        estimatedPropertyValue: new Money(600000m),
        creditScore: 610,
        employmentStatus: "Full-Time Marketing Lead",
        loanPurpose: "Refinance");

    var app2 = new LoanApplication("APP-2026-002", "APP-101", rules, facts2, DateTime.UtcNow.AddHours(-6));
    app2.Submit(DateTime.UtcNow.AddHours(-5));
    await repo.AddAsync(app2);
    await evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-002"));
    await draftHandler.HandleAsync(new GenerateRecommendationDraftCommand("APP-2026-002"));
}

public partial class Program { }
