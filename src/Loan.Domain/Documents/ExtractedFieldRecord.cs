namespace Loan.Domain.Documents;

public enum FieldConfirmationStatus
{
    Unconfirmed,
    ConfirmedByApplicant,
    ConfirmedByOfficer,
    Rejected
}

public class ExtractedFieldRecord
{
    public string FieldName { get; }
    public string DisplayValue { get; private set; }
    public string RawValueMasked { get; }
    public float ConfidenceScore { get; }
    public string SourceDocumentId { get; }
    public string ProvenanceExcerpt { get; }
    public bool IsSensitive { get; }
    public bool IsValidFormat { get; }
    public bool NeedsConfirmation => ConfidenceScore < 0.85f || !IsValidFormat || Status == FieldConfirmationStatus.Unconfirmed;
    public FieldConfirmationStatus Status { get; private set; }
    public string? ConfirmedValue { get; private set; }
    public string? ConfirmedBy { get; private set; }
    public string? ConfirmedRole { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }

    public ExtractedFieldRecord(
        string fieldName,
        string displayValue,
        string rawValueMasked,
        float confidenceScore,
        string sourceDocumentId,
        string provenanceExcerpt,
        bool isSensitive = false,
        bool isValidFormat = true,
        FieldConfirmationStatus status = FieldConfirmationStatus.Unconfirmed)
    {
        FieldName = fieldName;
        DisplayValue = displayValue;
        RawValueMasked = rawValueMasked;
        ConfidenceScore = Math.Clamp(confidenceScore, 0.0f, 1.0f);
        SourceDocumentId = sourceDocumentId;
        ProvenanceExcerpt = provenanceExcerpt;
        IsSensitive = isSensitive;
        IsValidFormat = isValidFormat;
        Status = status;
    }

    public void ConfirmOrOverride(string confirmedValue, string confirmedBy, string confirmedRole, DateTime timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(confirmedBy))
        {
            throw new ArgumentException("ConfirmedBy user identifier is required.", nameof(confirmedBy));
        }

        ConfirmedValue = confirmedValue;
        DisplayValue = IsSensitive ? MaskSensitiveValue(confirmedValue) : confirmedValue;
        ConfirmedBy = confirmedBy;
        ConfirmedRole = confirmedRole;
        ConfirmedAtUtc = timestampUtc;

        Status = confirmedRole.Equals("Officer", StringComparison.OrdinalIgnoreCase) || confirmedRole.Equals("LoanOfficer", StringComparison.OrdinalIgnoreCase)
            ? FieldConfirmationStatus.ConfirmedByOfficer
            : FieldConfirmationStatus.ConfirmedByApplicant;
    }

    public static string MaskSensitiveValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "****";
        if (value.Length <= 4) return new string('*', value.Length);
        if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{3}-\d{2}-\d{4}$"))
        {
            return $"***-**-{value[^4..]}";
        }
        return $"{new string('*', value.Length - 4)}{value[^4..]}";
    }
}
