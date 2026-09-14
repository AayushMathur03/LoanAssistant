using Loan.Application.Abstractions;
using Loan.Domain.Applications;

namespace Loan.Application.Documents;

public record FieldConfirmationItem(
    string DocumentId,
    string FieldName,
    string ConfirmedValue,
    string Reason);

public record ConfirmOrOverrideExtractedFieldsCommand(
    string ApplicationId,
    string ActorId,
    string ActorRole,
    IReadOnlyList<FieldConfirmationItem> FieldConfirmations,
    string? CorrelationId = null);

public class ConfirmOrOverrideExtractedFieldsCommandHandler
{
    private readonly ILoanApplicationRepository _repository;

    public ConfirmOrOverrideExtractedFieldsCommandHandler(ILoanApplicationRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(ConfirmOrOverrideExtractedFieldsCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ApplicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ActorRole);

        // 1. Role Authorization Check (Correction 6)
        var normalizedRole = command.ActorRole.Trim();
        if (normalizedRole.Equals("Compliance", StringComparison.OrdinalIgnoreCase) || normalizedRole.Equals("ComplianceReviewer", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Compliance Reviewers have read-only audit access and cannot confirm or override extracted source document facts.");
        }

        if (!normalizedRole.Equals("Applicant", StringComparison.OrdinalIgnoreCase) &&
            !normalizedRole.Equals("Officer", StringComparison.OrdinalIgnoreCase) &&
            !normalizedRole.Equals("LoanOfficer", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Role '{command.ActorRole}' is not authorized to confirm or override extracted document facts.");
        }

        // 2. Application Isolation Check (Correction 8)
        var application = await _repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application == null)
        {
            throw new KeyNotFoundException($"Loan application '{command.ApplicationId}' was not found.");
        }

        if (normalizedRole.Equals("Applicant", StringComparison.OrdinalIgnoreCase) && !string.Equals(application.ApplicantId, command.ActorId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Applicant '{command.ActorId}' is not authorized to modify application '{command.ApplicationId}'.");
        }

        var timestamp = DateTime.UtcNow;
        var correlationId = command.CorrelationId ?? Guid.NewGuid().ToString("N");

        // 3. Process each confirmed/overridden field
        foreach (var item in command.FieldConfirmations)
        {
            application.ConfirmOrOverrideField(
                documentId: item.DocumentId,
                fieldName: item.FieldName,
                confirmedValue: item.ConfirmedValue,
                actorId: command.ActorId,
                actorRole: normalizedRole,
                reason: item.Reason,
                correlationId: correlationId,
                timestampUtc: timestamp);
        }

        // 4. Save updated aggregate to EF Core repository
        await _repository.UpdateAsync(application, cancellationToken);
    }
}
