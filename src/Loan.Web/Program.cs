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
using Loan.Infrastructure.Persistence;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Infrastructure.Search;
using Loan.Infrastructure.Verification;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services to the container.
builder.Services.AddControllersWithViews();

// Register Infrastructure Persistence Repositories & Tools via Composition Extension
builder.Services.AddInfrastructurePersistence(builder.Configuration);

// Register ASP.NET Core Identity with Entity Framework Core
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<LoanDbContext>()
.AddDefaultTokenProviders();

// Register Application CQRS Handlers
builder.Services.AddTransient<AskProductQuestionQueryHandler>();
builder.Services.AddTransient<EvaluateEligibilityCommandHandler>();
builder.Services.AddTransient<GenerateRecommendationDraftCommandHandler>();
builder.Services.AddTransient<OfficerDecisionCommandHandler>();

// Configure ASP.NET Core Identity Application Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ApexLending.Auth";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
});

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
app.UseAuthentication();
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

    // Seed Synthetic Demo Identity Users and Roles (Applicant, LoanOfficer, ComplianceReviewer, Administrator)
    await scope.ServiceProvider.SeedIdentityUsersAndRolesAsync();

    // Seed Comprehensive Multi-Stage Demo Applications (APP-2026-001 through APP-2026-004)
    await DemoDataSeeder.SeedDemoApplicationsAsync(scope.ServiceProvider);
}

public partial class Program { }
