using Loan.Domain.Applications;

namespace Loan.Application.DTOs;

public record ExtractedFieldDto(
    string FieldName,
    string? RawValue,
    float ConfidenceScore,
    string SourceDocumentId,
    bool NeedsConfirmation,
    FieldConfirmationStatus ConfirmationStatus,
    string? ConfirmedBy);
