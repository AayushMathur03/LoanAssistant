using System.Text.Json;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Documents;
using Loan.Domain.Eligibility;
using Loan.Domain.Products;
using Loan.Domain.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace Loan.Infrastructure.Persistence.DbContext;

public class LoanDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public DbSet<LoanApplicationEntity> Applications => Set<LoanApplicationEntity>();
    public DbSet<RecommendationEntity> Recommendations => Set<RecommendationEntity>();

    public LoanDbContext(DbContextOptions<LoanDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // LoanApplicationEntity mapping
        modelBuilder.Entity<LoanApplicationEntity>(entity =>
        {
            entity.ToTable("LoanApplications");
            entity.HasKey(e => e.ApplicationId);

            entity.Property(e => e.ApplicationId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ApplicantId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ProductId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

            entity.Property(e => e.FactsJson).HasColumnName("Facts").IsRequired();
            entity.Property(e => e.ProductRulesJson).HasColumnName("ProductRules").IsRequired();
            entity.Property(e => e.IndicatorsJson).HasColumnName("Indicators");

            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();
        });

        // RecommendationEntity mapping
        modelBuilder.Entity<RecommendationEntity>(entity =>
        {
            entity.ToTable("Recommendations");
            entity.HasKey(e => e.RecommendationId);

            entity.Property(e => e.RecommendationId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ApplicationId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.DecisionRecommendation).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(e => e.SummaryReasoning).HasMaxLength(4000);

            entity.Property(e => e.CitationsJson).HasColumnName("Citations");
            entity.Property(e => e.PolicyViolationsJson).HasColumnName("PolicyViolations");
            entity.Property(e => e.MissingEvidenceItemsJson).HasColumnName("MissingEvidenceItems");
            entity.Property(e => e.AuditTrailJson).HasColumnName("AuditTrail");

            entity.Property(e => e.ApprovedByOfficerId).HasMaxLength(64);
            entity.Property(e => e.OfficerDecisionNotes).HasMaxLength(2000);
        });
    }
}

public class LoanApplicationEntity
{
    public string ApplicationId { get; set; } = string.Empty;
    public string ApplicantId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }

    public string FactsJson { get; set; } = "{}";
    public string ProductRulesJson { get; set; } = "{}";
    public string? IndicatorsJson { get; set; }
    public string DocumentsJson { get; set; } = "[]";
    public string DocumentAuditTrailJson { get; set; } = "[]";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public LoanApplication ToDomain(Recommendation? recommendation = null)
    {
        var facts = JsonSerializer.Deserialize<ApplicantFactsDto>(FactsJson)?.ToDomain() 
            ?? new ApplicantFacts(ApplicantId, "Unknown", "SYN-000000", new Money(0), new Money(0), new Money(0), new Money(0), 0, "Unknown", "Unknown");

        var rules = JsonSerializer.Deserialize<ProductRulesDto>(ProductRulesJson)?.ToDomain()
            ?? ProductRules.CreateStandardMortgage("v1.0");

        var app = new LoanApplication(ApplicationId, ApplicantId, rules, facts, CreatedAtUtc);

        if (!string.IsNullOrEmpty(IndicatorsJson))
        {
            app.EvaluateEligibility(UpdatedAtUtc);
        }

        if (recommendation != null)
        {
            app.SetRecommendation(recommendation, UpdatedAtUtc);
        }

        // Restore Document Records
        if (!string.IsNullOrWhiteSpace(DocumentsJson) && DocumentsJson != "[]")
        {
            var docDtos = JsonSerializer.Deserialize<List<ExtractedDocumentRecordDto>>(DocumentsJson);
            if (docDtos != null)
            {
                foreach (var docDto in docDtos)
                {
                    var doc = docDto.ToDomain();
                    app.AddDocumentRecord(doc, UpdatedAtUtc);
                }
            }
        }

        // Restore Document Audit Trail
        if (!string.IsNullOrWhiteSpace(DocumentAuditTrailJson) && DocumentAuditTrailJson != "[]")
        {
            var auditDtos = JsonSerializer.Deserialize<List<FieldOverrideAuditDto>>(DocumentAuditTrailJson);
            if (auditDtos != null)
            {
                var auditField = typeof(LoanApplication).GetField("_documentAuditTrail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (auditField?.GetValue(app) is List<FieldOverrideAuditEntry> auditList)
                {
                    auditList.AddRange(auditDtos.Select(a => a.ToDomain()));
                }
            }
        }

        typeof(LoanApplication).GetProperty(nameof(LoanApplication.Status))!
            .SetValue(app, Status);
        typeof(LoanApplication).GetProperty(nameof(LoanApplication.UpdatedAtUtc))!
            .SetValue(app, UpdatedAtUtc);

        return app;
    }

    public static LoanApplicationEntity FromDomain(LoanApplication app)
    {
        var docDtos = app.Documents.Select(ExtractedDocumentRecordDto.FromDomain).ToList();
        var auditDtos = app.DocumentAuditTrail.Select(FieldOverrideAuditDto.FromDomain).ToList();

        return new LoanApplicationEntity
        {
            ApplicationId = app.ApplicationId,
            ApplicantId = app.ApplicantId,
            ProductId = app.ProductId,
            Status = app.Status,
            FactsJson = JsonSerializer.Serialize(ApplicantFactsDto.FromDomain(app.Facts)),
            ProductRulesJson = JsonSerializer.Serialize(ProductRulesDto.FromDomain(app.ProductRules)),
            IndicatorsJson = app.Indicators != null ? JsonSerializer.Serialize(EligibilityIndicatorsDto.FromDomain(app.Indicators)) : null,
            DocumentsJson = JsonSerializer.Serialize(docDtos),
            DocumentAuditTrailJson = JsonSerializer.Serialize(auditDtos),
            CreatedAtUtc = app.CreatedAtUtc,
            UpdatedAtUtc = app.UpdatedAtUtc
        };
    }
}

public class RecommendationEntity
{
    public string RecommendationId { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public RecommendationType DecisionRecommendation { get; set; }
    public RecommendationStatus Status { get; set; }
    public double RiskScore { get; set; }
    public string SummaryReasoning { get; set; } = string.Empty;
    
    public string CitationsJson { get; set; } = "[]";
    public string PolicyViolationsJson { get; set; } = "[]";
    public string MissingEvidenceItemsJson { get; set; } = "[]";
    public string AuditTrailJson { get; set; } = "[]";

    public string? ApprovedByOfficerId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? OfficerDecisionNotes { get; set; }

    public Recommendation ToDomain()
    {
        var citations = JsonSerializer.Deserialize<List<CitationDto>>(CitationsJson)?.Select(c => c.ToDomain()) ?? Enumerable.Empty<RecommendationCitation>();
        var violations = JsonSerializer.Deserialize<List<string>>(PolicyViolationsJson) ?? new List<string>();
        var missing = JsonSerializer.Deserialize<List<string>>(MissingEvidenceItemsJson) ?? new List<string>();

        var rec = new Recommendation(
            RecommendationId,
            DecisionRecommendation,
            RiskScore,
            SummaryReasoning,
            citations,
            violations,
            missing,
            ApprovedAtUtc ?? DateTime.UtcNow);

        typeof(Recommendation).GetProperty(nameof(Recommendation.Status))!
            .SetValue(rec, Status);
        typeof(Recommendation).GetProperty(nameof(Recommendation.ApprovedByOfficerId))!
            .SetValue(rec, ApprovedByOfficerId);
        typeof(Recommendation).GetProperty(nameof(Recommendation.ApprovedAtUtc))!
            .SetValue(rec, ApprovedAtUtc);
        typeof(Recommendation).GetProperty(nameof(Recommendation.OfficerDecisionNotes))!
            .SetValue(rec, OfficerDecisionNotes);

        return rec;
    }

    public static RecommendationEntity FromDomain(Recommendation rec, string applicationId)
    {
        var citations = rec.Citations.Select(c => new CitationDto(c.DocumentTitle, c.PolicyVersion, c.SectionOrPage, c.Excerpt)).ToList();
        var audit = rec.AuditTrail.Select(a => new AuditEntryDto(a.Action, a.PerformedBy, a.TimestampUtc, a.Notes)).ToList();

        return new RecommendationEntity
        {
            RecommendationId = rec.RecommendationId,
            ApplicationId = applicationId,
            DecisionRecommendation = rec.DecisionRecommendation,
            Status = rec.Status,
            RiskScore = rec.RiskScore,
            SummaryReasoning = rec.SummaryReasoning,
            CitationsJson = JsonSerializer.Serialize(citations),
            PolicyViolationsJson = JsonSerializer.Serialize(rec.PolicyViolations),
            MissingEvidenceItemsJson = JsonSerializer.Serialize(rec.MissingEvidenceItems),
            AuditTrailJson = JsonSerializer.Serialize(audit),
            ApprovedByOfficerId = rec.ApprovedByOfficerId,
            ApprovedAtUtc = rec.ApprovedAtUtc,
            OfficerDecisionNotes = rec.OfficerDecisionNotes
        };
    }
}

internal record ApplicantFactsDto(
    string ApplicantId,
    string FullName,
    string SyntheticId,
    decimal MonthlyGrossIncome,
    decimal MonthlyDebts,
    decimal RequestedLoanAmount,
    decimal EstimatedPropertyValue,
    int CreditScore,
    string EmploymentStatus,
    string LoanPurpose)
{
    public ApplicantFacts ToDomain() => new(
        ApplicantId, FullName, SyntheticId, new Money(MonthlyGrossIncome), new Money(MonthlyDebts),
        new Money(RequestedLoanAmount), new Money(EstimatedPropertyValue), CreditScore, EmploymentStatus, LoanPurpose);

    public static ApplicantFactsDto FromDomain(ApplicantFacts f) => new(
        f.ApplicantId, f.FullName, f.SyntheticId, f.MonthlyGrossIncome.Amount, f.MonthlyDebts.Amount,
        f.RequestedLoanAmount.Amount, f.EstimatedPropertyValue.Amount, f.CreditScore, f.EmploymentStatus, f.LoanPurpose);
}

internal record ProductRulesDto(
    string ProductId,
    string ProductName,
    string EffectiveVersion,
    decimal MaxDtiRatio,
    decimal MaxLtvRatio,
    int MinCreditScore,
    decimal MinMonthlyIncome,
    decimal MaxLoanAmount,
    bool RequiresIncomeVerification,
    bool RequiresIdentityVerification)
{
    public ProductRules ToDomain() => new(
        ProductId, ProductName, EffectiveVersion, MaxDtiRatio, MaxLtvRatio, MinCreditScore, new Money(MinMonthlyIncome), new Money(MaxLoanAmount), RequiresIncomeVerification, RequiresIdentityVerification);

    public static ProductRulesDto FromDomain(ProductRules r) => new(
        r.ProductId, r.ProductName, r.EffectiveVersion, r.MaxDtiRatio, r.MaxLtvRatio, r.MinCreditScore, r.MinMonthlyIncome.Amount, r.MaxLoanAmount.Amount, r.RequiresIncomeVerification, r.RequiresIdentityVerification);
}

internal record EligibilityIndicatorsDto(
    decimal? DebtToIncomeRatio,
    decimal? LoanToValueRatio,
    bool IsDtiEligible,
    bool IsLtvEligible,
    bool IsCreditScoreEligible,
    bool IsIncomeThresholdEligible,
    bool IsLoanAmountEligible,
    bool IsIdentityVerified,
    bool IsIncomeVerified,
    bool IsCreditVerified,
    EligibilityStatus Status,
    List<string> UnmetConditions,
    DateTime EvaluatedAtUtc)
{
    public EligibilityIndicators ToDomain() => new(
        DebtToIncomeRatio, LoanToValueRatio, IsDtiEligible, IsLtvEligible, IsCreditScoreEligible, IsIncomeThresholdEligible, IsLoanAmountEligible, IsIdentityVerified, IsIncomeVerified, IsCreditVerified, Status, UnmetConditions, EvaluatedAtUtc);

    public static EligibilityIndicatorsDto FromDomain(EligibilityIndicators i) => new(
        i.DebtToIncomeRatio, i.LoanToValueRatio, i.IsDtiEligible, i.IsLtvEligible, i.IsCreditScoreEligible, i.IsIncomeThresholdEligible, i.IsLoanAmountEligible, i.IsIdentityVerified, i.IsIncomeVerified, i.IsCreditVerified, i.Status, i.UnmetConditions.ToList(), i.EvaluatedAtUtc);
}

internal record CitationDto(string DocumentTitle, string PolicyVersion, string SectionOrPage, string Excerpt)
{
    public RecommendationCitation ToDomain() => new(DocumentTitle, PolicyVersion, SectionOrPage, Excerpt);
}

internal record AuditEntryDto(string Action, string PerformedBy, DateTime TimestampUtc, string Notes);

internal record ExtractedFieldRecordDto(
    string FieldName,
    string DisplayValue,
    string RawValueMasked,
    float ConfidenceScore,
    string SourceDocumentId,
    string ProvenanceExcerpt,
    bool IsSensitive,
    bool IsValidFormat,
    FieldConfirmationStatus Status,
    string? ConfirmedValue,
    string? ConfirmedBy,
    string? ConfirmedRole,
    DateTime? ConfirmedAtUtc)
{
    public ExtractedFieldRecord ToDomain()
    {
        var rec = new ExtractedFieldRecord(FieldName, DisplayValue, RawValueMasked, ConfidenceScore, SourceDocumentId, ProvenanceExcerpt, IsSensitive, IsValidFormat, Status);
        if (!string.IsNullOrWhiteSpace(ConfirmedBy) && !string.IsNullOrWhiteSpace(ConfirmedValue))
        {
            rec.ConfirmOrOverride(ConfirmedValue, ConfirmedBy, ConfirmedRole ?? "User", ConfirmedAtUtc ?? DateTime.UtcNow);
        }
        return rec;
    }

    public static ExtractedFieldRecordDto FromDomain(ExtractedFieldRecord f) => new(
        f.FieldName, f.DisplayValue, f.RawValueMasked, f.ConfidenceScore, f.SourceDocumentId, f.ProvenanceExcerpt,
        f.IsSensitive, f.IsValidFormat, f.Status, f.ConfirmedValue, f.ConfirmedBy, f.ConfirmedRole, f.ConfirmedAtUtc);
}

internal record ExtractedDocumentRecordDto(
    string DocumentId,
    string ApplicationId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string StorageReference,
    string HashSha256,
    Loan.Domain.Documents.DocumentType DocumentType,
    Loan.Domain.Documents.ExtractionStatus Status,
    DateTime UploadedAtUtc,
    List<ExtractedFieldRecordDto> Fields)
{
    public ExtractedDocumentRecord ToDomain()
    {
        var doc = new ExtractedDocumentRecord(DocumentId, ApplicationId, FileName, ContentType, FileSizeBytes, StorageReference, HashSha256, DocumentType, UploadedAtUtc);
        doc.AddExtractedFields(Fields.Select(f => f.ToDomain()));
        doc.UpdateStatus(Status);
        return doc;
    }

    public static ExtractedDocumentRecordDto FromDomain(ExtractedDocumentRecord d) => new(
        d.DocumentId, d.ApplicationId, d.FileName, d.ContentType, d.FileSizeBytes, d.StorageReference, d.HashSha256,
        d.DocumentType, d.Status, d.UploadedAtUtc, d.Fields.Select(ExtractedFieldRecordDto.FromDomain).ToList());
}

internal record FieldOverrideAuditDto(
    string AuditId,
    string ApplicationId,
    string FieldName,
    string DocumentId,
    string ActorId,
    string ActorRole,
    DateTime TimestampUtc,
    string Action,
    string Reason,
    string CorrelationId)
{
    public FieldOverrideAuditEntry ToDomain() => new(AuditId, ApplicationId, FieldName, DocumentId, ActorId, ActorRole, TimestampUtc, Action, Reason, CorrelationId);

    public static FieldOverrideAuditDto FromDomain(FieldOverrideAuditEntry a) => new(
        a.AuditId, a.ApplicationId, a.FieldName, a.DocumentId, a.ActorId, a.ActorRole, a.TimestampUtc, a.Action, a.Reason, a.CorrelationId);
}
