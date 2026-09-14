using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Loan.IntegrationTests;

[TestFixture]
public class SqlPersistenceIntegrationTests
{
    private const string TestConnectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LoanAssistantTestDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    private DbContextOptions<LoanDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer(TestConnectionString, b => b.MigrationsAssembly("Loan.Infrastructure"))
            .Options;
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Ensure test database is migrated cleanly from scratch via EF Migrations
        using var context = new LoanDbContext(CreateOptions());
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        using var context = new LoanDbContext(CreateOptions());
        await context.Database.EnsureDeletedAsync();
    }

    [Test]
    public async Task AddAndGetLoanApplication_ShouldPersistAndReconstructDomainObject()
    {
        var testAppId = $"APP-UNIT-{Guid.NewGuid().ToString("N")[..6]}";

        using var context = new LoanDbContext(CreateOptions());
        var appRepo = new SqlLoanApplicationRepository(context);

        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts(
            applicantId: "APP-SQL-UNIT-1",
            fullName: "Unit Sql User",
            syntheticId: "SYN-111222",
            monthlyGrossIncome: new Money(15000m),
            monthlyDebts: new Money(4000m),
            requestedLoanAmount: new Money(450000m),
            estimatedPropertyValue: new Money(650000m),
            creditScore: 780,
            employmentStatus: "Full-Time",
            loanPurpose: "Purchase");

        var app = new LoanApplication(testAppId, "APP-SQL-UNIT-1", rules, facts, DateTime.UtcNow);
        app.Submit(DateTime.UtcNow);

        await appRepo.AddAsync(app);
        var retrieved = await appRepo.GetByIdAsync(testAppId);

        Assert.That(retrieved, Is.Not.Null);
        Assert.That(retrieved!.ApplicationId, Is.EqualTo(testAppId));
        Assert.That(retrieved.ApplicantId, Is.EqualTo("APP-SQL-UNIT-1"));
        Assert.That(retrieved.Facts.FullName, Is.EqualTo("Unit Sql User"));
        Assert.That(retrieved.Facts.MonthlyGrossIncome.Amount, Is.EqualTo(15000m));
        Assert.That(retrieved.Status, Is.EqualTo(ApplicationStatus.Submitted));
    }

    [Test]
    public async Task SaveAndGetRecommendation_ShouldPersistRecommendationAndLinkToApplication()
    {
        var testAppId = $"APP-UNITREC-{Guid.NewGuid().ToString("N")[..6]}";
        var testRecId = $"REC-UNITREC-{Guid.NewGuid().ToString("N")[..6]}";

        using var context = new LoanDbContext(CreateOptions());
        var appRepo = new SqlLoanApplicationRepository(context);
        var recRepo = new SqlRecommendationRepository(context);

        var rules = ProductRules.CreateStandardMortgage("v1.2");
        var facts = new ApplicantFacts(
            applicantId: "APP-SQL-UNIT-2",
            fullName: "Recommendation Sql User",
            syntheticId: "SYN-333444",
            monthlyGrossIncome: new Money(10000m),
            monthlyDebts: new Money(2000m),
            requestedLoanAmount: new Money(200000m),
            estimatedPropertyValue: new Money(300000m),
            creditScore: 720,
            employmentStatus: "Full-Time",
            loanPurpose: "Refinance");

        var app = new LoanApplication(testAppId, "APP-SQL-UNIT-2", rules, facts, DateTime.UtcNow);
        await appRepo.AddAsync(app);

        var citation = new RecommendationCitation("Title", "v1.0", "Sec 1", "Excerpt");
        var rec = new Recommendation(
            testRecId,
            RecommendationType.Approve,
            0.15,
            "Low risk application",
            new[] { citation },
            Array.Empty<string>(),
            Array.Empty<string>(),
            DateTime.UtcNow);

        await recRepo.SaveAsync(rec);
        app.SetRecommendation(rec, DateTime.UtcNow);
        await appRepo.UpdateAsync(app);

        var retrievedRec = await recRepo.GetByIdAsync(testRecId);
        var retrievedApp = await appRepo.GetByIdAsync(testAppId);

        Assert.That(retrievedRec, Is.Not.Null);
        Assert.That(retrievedRec!.RecommendationId, Is.EqualTo(testRecId));
        Assert.That(retrievedRec.DecisionRecommendation, Is.EqualTo(RecommendationType.Approve));
        Assert.That(retrievedApp, Is.Not.Null);
        Assert.That(retrievedApp!.CurrentRecommendation, Is.Not.Null);
        Assert.That(retrievedApp.CurrentRecommendation!.RecommendationId, Is.EqualTo(testRecId));
    }

    [Test]
    public async Task RealSqlServer_Persistence_ShouldSurviveDbContextRecreation()
    {
        var testAppId = $"APP-REALSQL-{Guid.NewGuid().ToString("N")[..6]}";
        var testRecId = testAppId.Replace("APP", "REC");

        // Step 1: Create application in DbContext #1
        using (var context1 = new LoanDbContext(CreateOptions()))
        {
            var appRepo = new SqlLoanApplicationRepository(context1);
            var rules = ProductRules.CreateStandardMortgage("v1.2");
            var facts = new ApplicantFacts(
                applicantId: "APP-SQL-USER-1",
                fullName: "Real Sql Server User",
                syntheticId: "SYN-777888",
                monthlyGrossIncome: new Money(14000m),
                monthlyDebts: new Money(3200m),
                requestedLoanAmount: new Money(300000m),
                estimatedPropertyValue: new Money(450000m),
                creditScore: 760,
                employmentStatus: "Full-Time Architect",
                loanPurpose: "Primary Residence Purchase");

            var app = new LoanApplication(testAppId, "APP-SQL-USER-1", rules, facts, DateTime.UtcNow);
            app.Submit(DateTime.UtcNow);

            await appRepo.AddAsync(app);
        }

        // Step 2: Read back from brand new DbContext #2 to prove SQL Server physical disk persistence
        using (var context2 = new LoanDbContext(CreateOptions()))
        {
            var appRepo2 = new SqlLoanApplicationRepository(context2);
            var retrievedApp = await appRepo2.GetByIdAsync(testAppId);

            Assert.That(retrievedApp, Is.Not.Null);
            Assert.That(retrievedApp!.ApplicationId, Is.EqualTo(testAppId));
            Assert.That(retrievedApp.Facts.FullName, Is.EqualTo("Real Sql Server User"));
            Assert.That(retrievedApp.Facts.MonthlyGrossIncome.Amount, Is.EqualTo(14000m));
            Assert.That(retrievedApp.Status, Is.EqualTo(ApplicationStatus.Submitted));

            // Attach recommendation in DbContext #2
            var recRepo2 = new SqlRecommendationRepository(context2);
            var citation = new RecommendationCitation("Mortgage Policy Guide", "v1.2", "Section 3.1", "Max DTI 43%");
            var rec = new Recommendation(
                testRecId,
                RecommendationType.Approve,
                0.12,
                "Strong credit history and low DTI.",
                new[] { citation },
                Array.Empty<string>(),
                Array.Empty<string>(),
                DateTime.UtcNow);

            await recRepo2.SaveAsync(rec);
            retrievedApp.SetRecommendation(rec, DateTime.UtcNow);
            await appRepo2.UpdateAsync(retrievedApp);
        }

        // Step 3: Read back application AND recommendation from DbContext #3
        using (var context3 = new LoanDbContext(CreateOptions()))
        {
            var appRepo3 = new SqlLoanApplicationRepository(context3);
            var recRepo3 = new SqlRecommendationRepository(context3);

            var finalApp = await appRepo3.GetByIdAsync(testAppId);
            var finalRec = await recRepo3.GetByIdAsync(testRecId);

            Assert.That(finalApp, Is.Not.Null);
            Assert.That(finalRec, Is.Not.Null);
            Assert.That(finalApp!.CurrentRecommendation, Is.Not.Null);
            Assert.That(finalApp.CurrentRecommendation!.RecommendationId, Is.EqualTo(testRecId));
            Assert.That(finalRec!.DecisionRecommendation, Is.EqualTo(RecommendationType.Approve));
            Assert.That(finalRec.Citations.Count, Is.EqualTo(1));
            Assert.That(finalRec.Citations[0].DocumentTitle, Is.EqualTo("Mortgage Policy Guide"));
        }
    }
}
