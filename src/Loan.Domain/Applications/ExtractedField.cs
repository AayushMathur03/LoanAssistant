using Loan.Domain.Documents;

namespace Loan.Domain.Applications;

public class ExtractedField<T>
{
    public string FieldName { get; }
    public T? Value { get; private set; }
    public float ConfidenceScore { get; }
    public string SourceDocumentId { get; }
    public FieldConfirmationStatus Status { get; private set; }
    public DateTime ExtractedAtUtc { get; }
    public string? ConfirmedBy { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }

    public bool NeedsConfirmation => ConfidenceScore < 0.85f && Status == FieldConfirmationStatus.Unconfirmed;

    public ExtractedField(
        string fieldName,
        T? value,
        float confidenceScore,
        string sourceDocumentId,
        DateTime extractedAtUtc,
        FieldConfirmationStatus status = FieldConfirmationStatus.Unconfirmed)
    {
        FieldName = fieldName;
        Value = value;
        ConfidenceScore = Math.Clamp(confidenceScore, 0.0f, 1.0f);
        SourceDocumentId = sourceDocumentId;
        ExtractedAtUtc = extractedAtUtc;
        Status = status;
    }

    public void Confirm(string confirmedBy, T? overrideValue = default)
    {
        if (string.IsNullOrWhiteSpace(confirmedBy))
        {
            throw new ArgumentException("ConfirmedBy user identifier is required.", nameof(confirmedBy));
        }

        if (overrideValue != null && !EqualityComparer<T>.Default.Equals(overrideValue, default))
        {
            Value = overrideValue;
        }

        Status = ConfirmedBy == "OFFICER" ? FieldConfirmationStatus.ConfirmedByOfficer : FieldConfirmationStatus.ConfirmedByApplicant;
        ConfirmedBy = confirmedBy;
        ConfirmedAtUtc = DateTime.UtcNow;
    }
}
