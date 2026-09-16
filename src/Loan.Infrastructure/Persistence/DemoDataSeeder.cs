using Loan.Application.Abstractions;
using Loan.Application.Recommendations;
using Loan.Application.Verification;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Documents;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Loan.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    public static async Task SeedDemoApplicationsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ILoanApplicationRepository>();
        var evalHandler = scope.ServiceProvider.GetRequiredService<EvaluateEligibilityCommandHandler>();
        var draftHandler = scope.ServiceProvider.GetRequiredService<GenerateRecommendationDraftCommandHandler>();
        var recRepo = scope.ServiceProvider.GetService<IRecommendationRepository>();
        var logger = scope.ServiceProvider.GetService<ILogger<LoanApplication>>();

        var now = DateTime.UtcNow;

        // =========================================================================
        // 1. APP-2026-001 (Alice Cooper) — Standard Mortgage v1.2 (Prime Eligible)
        // Stage: ReadyForReview / Draft Prepared — Happy Path for Officer Approval
        // =========================================================================
        var app1 = await repo.GetByIdAsync("APP-2026-001");
        if (app1 == null || app1.Documents.Count == 0)
        {
            var rules1 = ProductRules.CreateStandardMortgage("v1.2");
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

            app1 ??= new LoanApplication("APP-2026-001", "APP-100", rules1, facts1, now.AddHours(-24));

            if (app1.Documents.Count == 0)
            {
                // Paystub (Verified & Confirmed)
                var docPaystub = new ExtractedDocumentRecord(
                    documentId: "DOC-2026-PAYSTUB-01",
                    applicationId: "APP-2026-001",
                    fileName: "Alice_Cooper_Paystub_Verified.txt",
                    contentType: "text/plain",
                    fileSizeBytes: 2048,
                    storageReference: "SampleDocuments/Alice_Cooper_Paystub_Verified.txt",
                    hashSha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                    documentType: DocumentType.Paystub,
                    uploadedAtUtc: now.AddHours(-20));

                docPaystub.AddExtractedFields(new[]
                {
                    new ExtractedFieldRecord("FullName", "Alice Cooper", "Alice Cooper", 0.98f, "DOC-2026-PAYSTUB-01", "Header: Employee Name", status: FieldConfirmationStatus.ConfirmedByApplicant),
                    new ExtractedFieldRecord("MonthlyGrossIncome", "12000.00", "12000.00", 0.96f, "DOC-2026-PAYSTUB-01", "Line: Gross Pay: $12,000.00", status: FieldConfirmationStatus.ConfirmedByApplicant),
                    new ExtractedFieldRecord("EmployerName", "TechCorp Global LLC", "TechCorp Global LLC", 0.95f, "DOC-2026-PAYSTUB-01", "Header: Employer", status: FieldConfirmationStatus.ConfirmedByApplicant)
                });
                app1.AddDocumentRecord(docPaystub, now.AddHours(-20));

                // Bank Statement (Verified & Confirmed)
                var docBank = new ExtractedDocumentRecord(
                    documentId: "DOC-2026-BANK-01",
                    applicationId: "APP-2026-001",
                    fileName: "Alice_Cooper_BankStatement_60Day.txt",
                    contentType: "text/plain",
                    fileSizeBytes: 1850,
                    storageReference: "SampleDocuments/Alice_Cooper_BankStatement_60Day.txt",
                    hashSha256: "a1b2c3d4e5f67890123456789abcdef0123456789abcdef0123456789abcdef0",
                    documentType: DocumentType.BankStatement,
                    uploadedAtUtc: now.AddHours(-19));

                docBank.AddExtractedFields(new[]
                {
                    new ExtractedFieldRecord("AvailableBalance", "85000.00", "85000.00", 0.94f, "DOC-2026-BANK-01", "Summary: Available Balance: $85,000.00", status: FieldConfirmationStatus.ConfirmedByApplicant),
                    new ExtractedFieldRecord("Average60DayBalance", "82450.00", "82450.00", 0.93f, "DOC-2026-BANK-01", "Summary: Average 60-Day Balance: $82,450.00", status: FieldConfirmationStatus.ConfirmedByApplicant)
                });
                app1.AddDocumentRecord(docBank, now.AddHours(-19));

                // Driver License (Verified & Confirmed)
                var docId = new ExtractedDocumentRecord(
                    documentId: "DOC-2026-ID-01",
                    applicationId: "APP-2026-001",
                    fileName: "Alice_Cooper_DriverLicense_Masked.txt",
                    contentType: "text/plain",
                    fileSizeBytes: 1200,
                    storageReference: "SampleDocuments/Alice_Cooper_DriverLicense_Masked.txt",
                    hashSha256: "f0e1d2c3b4a59687786950413223140596877869504132231405968778695041",
                    documentType: DocumentType.DriverLicenseOrPassport,
                    uploadedAtUtc: now.AddHours(-18));

                docId.AddExtractedFields(new[]
                {
                    new ExtractedFieldRecord("FullName", "Alice Marie Cooper", "Alice Marie Cooper", 0.99f, "DOC-2026-ID-01", "Line 1: Full Name", status: FieldConfirmationStatus.ConfirmedByApplicant),
                    new ExtractedFieldRecord("SSN", "***-**-6789", "***-**-6789", 0.98f, "DOC-2026-ID-01", "Line 2: SSN Masked", isSensitive: true, status: FieldConfirmationStatus.ConfirmedByApplicant)
                });
                app1.AddDocumentRecord(docId, now.AddHours(-18));
            }

            app1.Submit(now.AddHours(-16));
            await repo.AddOrUpdateAsync(app1);
            await evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-001"));
            await draftHandler.HandleAsync(new GenerateRecommendationDraftCommand("APP-2026-001"));
        }

        // =========================================================================
        // 2. APP-2026-002 (Jane Smith) — Personal Loan v2.0 (Missing Bank Statement)
        // Stage: PendingInformation — Proves Missing Evidence Alert & Checklist
        // =========================================================================
        var app2 = await repo.GetByIdAsync("APP-2026-002");
        if (app2 == null)
        {
            var rules2 = ProductRules.CreatePersonalLoan("v2.0");
            var facts2 = new ApplicantFacts(
                applicantId: "APP-101",
                fullName: "Jane Smith",
                syntheticId: "SYN-654321",
                monthlyGrossIncome: new Money(6500m),
                monthlyDebts: new Money(1200m),
                requestedLoanAmount: new Money(25000m),
                estimatedPropertyValue: new Money(0m),
                creditScore: 680,
                employmentStatus: "Full-Time Marketing Lead",
                loanPurpose: "Debt Consolidation");

            app2 = new LoanApplication("APP-2026-002", "APP-101", rules2, facts2, now.AddHours(-14));

            // Paystub confirmed
            var docPaystub2 = new ExtractedDocumentRecord(
                documentId: "DOC-2026-PAYSTUB-02",
                applicationId: "APP-2026-002",
                fileName: "Jane_Smith_Paystub.txt",
                contentType: "text/plain",
                fileSizeBytes: 1900,
                storageReference: "SampleDocuments/Jane_Smith_Paystub.txt",
                hashSha256: "b2c3d4e5f6a7890123456789abcdef0123456789abcdef0123456789abcdef01",
                documentType: DocumentType.Paystub,
                uploadedAtUtc: now.AddHours(-12));

            docPaystub2.AddExtractedFields(new[]
            {
                new ExtractedFieldRecord("FullName", "Jane Smith", "Jane Smith", 0.97f, "DOC-2026-PAYSTUB-02", "Header: Employee Name", status: FieldConfirmationStatus.ConfirmedByApplicant),
                new ExtractedFieldRecord("MonthlyGrossIncome", "6500.00", "6500.00", 0.95f, "DOC-2026-PAYSTUB-02", "Line: Gross Pay: $6,500.00", status: FieldConfirmationStatus.ConfirmedByApplicant)
            });
            app2.AddDocumentRecord(docPaystub2, now.AddHours(-12));

            // Driver license confirmed
            var docId2 = new ExtractedDocumentRecord(
                documentId: "DOC-2026-ID-02",
                applicationId: "APP-2026-002",
                fileName: "Jane_Smith_DriverLicense.txt",
                contentType: "text/plain",
                fileSizeBytes: 1150,
                storageReference: "SampleDocuments/Jane_Smith_DriverLicense.txt",
                hashSha256: "c3d4e5f6a7b890123456789abcdef0123456789abcdef0123456789abcdef012",
                documentType: DocumentType.DriverLicenseOrPassport,
                uploadedAtUtc: now.AddHours(-11));

            docId2.AddExtractedFields(new[]
            {
                new ExtractedFieldRecord("FullName", "Jane Smith", "Jane Smith", 0.99f, "DOC-2026-ID-02", "Line 1: Full Name", status: FieldConfirmationStatus.ConfirmedByApplicant)
            });
            app2.AddDocumentRecord(docId2, now.AddHours(-11));

            // (NOTE: Intentionally missing Bank Statement to demonstrate Missing Evidence checklist!)
            app2.Submit(now.AddHours(-10));
            typeof(LoanApplication).GetProperty(nameof(LoanApplication.Status))!.SetValue(app2, ApplicationStatus.InformationRequested);
            await repo.AddOrUpdateAsync(app2);
            await evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-002"));
        }

        // =========================================================================
        // 3. APP-2026-003 (Bob Brown) — Standard Mortgage v1.2 (Low-Confidence Paystub)
        // Stage: PendingConfirmation — Proves Amber Warning Meter & Field Confirmation
        // =========================================================================
        var app3 = await repo.GetByIdAsync("APP-2026-003");
        if (app3 == null)
        {
            var rules3 = ProductRules.CreateStandardMortgage("v1.2");
            var facts3 = new ApplicantFacts(
                applicantId: "APP-102",
                fullName: "Bob Brown",
                syntheticId: "SYN-111222",
                monthlyGrossIncome: new Money(11500m),
                monthlyDebts: new Money(2800m),
                requestedLoanAmount: new Money(400000m),
                estimatedPropertyValue: new Money(550000m),
                creditScore: 720,
                employmentStatus: "Full-Time Operations Manager",
                loanPurpose: "Primary Residence Purchase");

            app3 = new LoanApplication("APP-2026-003", "APP-102", rules3, facts3, now.AddHours(-8));

            // Smudged low-confidence paystub (Unconfirmed, 72% confidence!)
            var docSmudged = new ExtractedDocumentRecord(
                documentId: "DOC-2026-SMUDGED-03",
                applicationId: "APP-2026-003",
                fileName: "LowConfidence_Smudged_Paystub.txt",
                contentType: "text/plain",
                fileSizeBytes: 1600,
                storageReference: "SampleDocuments/LowConfidence_Smudged_Paystub.txt",
                hashSha256: "d4e5f6a7b8c90123456789abcdef0123456789abcdef0123456789abcdef0123",
                documentType: DocumentType.Paystub,
                uploadedAtUtc: now.AddHours(-6));

            docSmudged.AddExtractedFields(new[]
            {
                new ExtractedFieldRecord("FullName", "Bob Brown", "Bob Brown", 0.82f, "DOC-2026-SMUDGED-03", "Header: Employee Name", status: FieldConfirmationStatus.ConfirmedByApplicant),
                new ExtractedFieldRecord("MonthlyGrossIncome", "11500.00", "11500.00", 0.72f, "DOC-2026-SMUDGED-03", "Line: Gross Pay (Smudged Scan): $11,500.00", isValidFormat: true, status: FieldConfirmationStatus.Unconfirmed),
                new ExtractedFieldRecord("PayPeriodEndingDate", "2026-08-XX", "2026-08-XX", 0.65f, "DOC-2026-SMUDGED-03", "Line 2: Date unreadable", isValidFormat: false, status: FieldConfirmationStatus.Unconfirmed)
            });
            app3.AddDocumentRecord(docSmudged, now.AddHours(-6));

            app3.Submit(now.AddHours(-5));
            typeof(LoanApplication).GetProperty(nameof(LoanApplication.Status))!.SetValue(app3, ApplicationStatus.InformationRequested);
            await repo.AddOrUpdateAsync(app3);
            await evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-003"));
        }

        // =========================================================================
        // 4. APP-2026-004 (Charlie Davis) — Auto Loan v1.1 (Exceeds Max DTI 45%)
        // Stage: Ineligible / ReferToHuman — Proves Deterministic Rule Referral
        // =========================================================================
        var app4 = await repo.GetByIdAsync("APP-2026-004");
        if (app4 == null)
        {
            var rules4 = ProductRules.CreateAutoLoan("v1.1");
            var facts4 = new ApplicantFacts(
                applicantId: "APP-103",
                fullName: "Charlie Davis",
                syntheticId: "SYN-333444",
                monthlyGrossIncome: new Money(3500m),
                monthlyDebts: new Money(1900m), // DTI = 1900 / 3500 = 54.3% > 45.0% Max DTI
                requestedLoanAmount: new Money(32000m),
                estimatedPropertyValue: new Money(35000m),
                creditScore: 660,
                employmentStatus: "Full-Time Logistics Coordinator",
                loanPurpose: "New Vehicle Purchase");

            app4 = new LoanApplication("APP-2026-004", "APP-103", rules4, facts4, now.AddHours(-4));

            var docPaystub4 = new ExtractedDocumentRecord(
                documentId: "DOC-2026-PAYSTUB-04",
                applicationId: "APP-2026-004",
                fileName: "Charlie_Davis_Paystub.txt",
                contentType: "text/plain",
                fileSizeBytes: 1800,
                storageReference: "SampleDocuments/Charlie_Davis_Paystub.txt",
                hashSha256: "e5f6a7b8c9d0123456789abcdef0123456789abcdef0123456789abcdef01234",
                documentType: DocumentType.Paystub,
                uploadedAtUtc: now.AddHours(-3));

            docPaystub4.AddExtractedFields(new[]
            {
                new ExtractedFieldRecord("FullName", "Charlie Davis", "Charlie Davis", 0.98f, "DOC-2026-PAYSTUB-04", "Header: Employee Name", status: FieldConfirmationStatus.ConfirmedByApplicant),
                new ExtractedFieldRecord("MonthlyGrossIncome", "3500.00", "3500.00", 0.96f, "DOC-2026-PAYSTUB-04", "Line: Gross Pay: $3,500.00", status: FieldConfirmationStatus.ConfirmedByApplicant)
            });
            app4.AddDocumentRecord(docPaystub4, now.AddHours(-3));

            app4.Submit(now.AddHours(-2));
            await repo.AddOrUpdateAsync(app4);
            await evalHandler.HandleAsync(new EvaluateEligibilityCommand("APP-2026-004"));
            await draftHandler.HandleAsync(new GenerateRecommendationDraftCommand("APP-2026-004"));
        }
    }

    private static async Task AddOrUpdateAsync(this ILoanApplicationRepository repo, LoanApplication app)
    {
        var existing = await repo.GetByIdAsync(app.ApplicationId);
        if (existing == null)
        {
            await repo.AddAsync(app);
        }
        else
        {
            await repo.UpdateAsync(app);
        }
    }
}
