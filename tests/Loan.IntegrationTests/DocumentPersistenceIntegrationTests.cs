using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Documents;
using Loan.Domain.Products;
using Loan.Infrastructure.Persistence.DbContext;
using Loan.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Loan.IntegrationTests;

[TestFixture]
public class DocumentPersistenceIntegrationTests
{
    private string _connectionString = null!;

    [SetUp]
    public void SetUp()
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../Loan.Web");
        if (!Directory.Exists(basePath))
        {
            basePath = Directory.GetCurrentDirectory();
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("LoanAssistant-Web-87612345-EF89")
            .AddEnvironmentVariables()
            .Build();

        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=LoanAssistantDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
    }

    private LoanDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LoanDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new LoanDbContext(options);
    }

    [Test]
    public async Task SqlServer_ExtractedDocumentAndAuditPersistence_ShouldPersistAndRehydrate()
    {
        using var context = CreateDbContext();
        await context.Database.MigrateAsync();

        var repo = new SqlLoanApplicationRepository(context);

        var appId = $"APP-DOC-TEST-{Guid.NewGuid():N}[..8]";
        var app = new LoanApplication(
            appId, "APP-100", ProductRules.CreateStandardMortgage("v1.0"),
            new ApplicantFacts("APP-100", "John Doe", "SYN-111", new Money(10000m), new Money(2500m), new Money(300000m), new Money(400000m), 750, "Full-Time", "Primary Residence"),
            DateTime.UtcNow);

        var doc = new ExtractedDocumentRecord(
            documentId: $"DOC-PERSIST-{Guid.NewGuid():N}[..8]",
            applicationId: appId,
            fileName: "paystub_2026.pdf",
            contentType: "application/pdf",
            fileSizeBytes: 2048,
            storageReference: "DOC-STORE-12345",
            hashSha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            documentType: DocumentType.Paystub,
            uploadedAtUtc: DateTime.UtcNow);

        doc.AddExtractedFields(new[]
        {
            new ExtractedFieldRecord("MonthlyGrossIncome", "10000.00", "10000.00", 0.95f, doc.DocumentId, "Line 1: Gross Pay", isSensitive: false, isValidFormat: true),
            new ExtractedFieldRecord("SSN", "***-**-6789", "***-**-6789", 0.98f, doc.DocumentId, "Line 2: SSN", isSensitive: true, isValidFormat: true)
        });

        app.AddDocumentRecord(doc, DateTime.UtcNow);
        app.ConfirmOrOverrideField(doc.DocumentId, "MonthlyGrossIncome", "10500.00", "OFF-101", "Officer", "Verified paystub raise", "CORR-999", DateTime.UtcNow);

        await repo.AddAsync(app);

        // Re-read from a fresh DbContext to prove persistence
        using var readContext = CreateDbContext();
        var readRepo = new SqlLoanApplicationRepository(readContext);

        var rehydrated = await readRepo.GetByIdAsync(appId);

        Assert.That(rehydrated, Is.Not.Null);
        Assert.That(rehydrated!.Documents.Count, Is.EqualTo(1));
        Assert.That(rehydrated.Documents[0].FileName, Is.EqualTo("paystub_2026.pdf"));
        Assert.That(rehydrated.Documents[0].Fields.Count, Is.EqualTo(2));
        Assert.That(rehydrated.DocumentAuditTrail.Count, Is.EqualTo(1));
        Assert.That(rehydrated.DocumentAuditTrail[0].ActorRole, Is.EqualTo("Officer"));
        Assert.That(rehydrated.DocumentAuditTrail[0].Action, Is.EqualTo("Override"));
    }
}
