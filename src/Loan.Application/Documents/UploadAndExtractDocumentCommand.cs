using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Domain.Applications;
using Loan.Domain.Documents;

namespace Loan.Application.Documents;

public record UploadAndExtractDocumentCommand(
    string ApplicationId,
    string FileName,
    string ContentType,
    Stream ContentStream);

public class UploadAndExtractDocumentCommandHandler
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IDocumentStorageService _storageService;
    private readonly IDocumentExtractor _documentExtractor;

    public UploadAndExtractDocumentCommandHandler(
        ILoanApplicationRepository repository,
        IDocumentStorageService storageService,
        IDocumentExtractor documentExtractor)
    {
        _repository = repository;
        _storageService = storageService;
        _documentExtractor = documentExtractor;
    }

    public async Task<ExtractedDocumentRecord> HandleAsync(UploadAndExtractDocumentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ApplicationId);

        // 1. Isolation check: Verify ApplicationId exists
        var application = await _repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application == null)
        {
            throw new KeyNotFoundException($"Loan application '{command.ApplicationId}' was not found.");
        }

        // 2. Validate file security & MIME
        var validation = DocumentUploadValidator.ValidateUpload(command.FileName, command.ContentType, command.ContentStream.Length);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Document upload rejected: {validation.FailureReason}");
        }

        // 3. Save raw file content into Infrastructure storage (NOT inside EF aggregate)
        var storageResult = await _storageService.SaveDocumentAsync(
            command.ApplicationId,
            validation.SanitizedFileName,
            validation.DetectedContentType,
            command.ContentStream,
            cancellationToken);

        // 4. Reset stream position for extractor
        if (command.ContentStream.CanSeek)
        {
            command.ContentStream.Position = 0;
        }

        // 5. Extract fields via IDocumentExtractor
        var extraction = await _documentExtractor.ExtractFieldsAsync(
            command.ContentStream,
            validation.SanitizedFileName,
            validation.DetectedContentType,
            cancellationToken);

        Enum.TryParse<DocumentType>(extraction.DocumentType, true, out var docType);

        var timestamp = DateTime.UtcNow;
        var docRecord = new ExtractedDocumentRecord(
            documentId: extraction.DocumentId,
            applicationId: command.ApplicationId,
            fileName: validation.SanitizedFileName,
            contentType: validation.DetectedContentType,
            fileSizeBytes: storageResult.FileSizeBytes,
            storageReference: storageResult.StorageReference,
            hashSha256: storageResult.HashSha256,
            documentType: docType,
            uploadedAtUtc: timestamp);

        var fieldRecords = extraction.ExtractedFields.Select(f => new ExtractedFieldRecord(
            fieldName: f.FieldName,
            displayValue: f.RawValue ?? string.Empty,
            rawValueMasked: f.RawValue ?? string.Empty,
            confidenceScore: f.ConfidenceScore,
            sourceDocumentId: f.SourceDocumentId,
            provenanceExcerpt: $"Extracted from {validation.SanitizedFileName} ({f.FieldName})",
            isSensitive: f.FieldName.Equals("SSN", StringComparison.OrdinalIgnoreCase) || f.FieldName.Equals("AccountNumber", StringComparison.OrdinalIgnoreCase) || f.FieldName.Equals("EmployerEin", StringComparison.OrdinalIgnoreCase),
            isValidFormat: !f.NeedsConfirmation || f.ConfidenceScore >= 0.85f,
            status: FieldConfirmationStatus.Unconfirmed)).ToList();

        docRecord.AddExtractedFields(fieldRecords);

        // 6. Update LoanApplication aggregate and save persistence
        application.AddDocumentRecord(docRecord, timestamp);
        await _repository.UpdateAsync(application, cancellationToken);

        return docRecord;
    }
}
