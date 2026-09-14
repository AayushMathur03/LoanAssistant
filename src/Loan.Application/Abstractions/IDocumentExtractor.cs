using Loan.Application.DTOs;

namespace Loan.Application.Abstractions;

public record DocumentExtractionResultDto(
    string DocumentId,
    string DocumentType,
    bool Success,
    string? ErrorMessage,
    IReadOnlyList<ExtractedFieldDto> ExtractedFields);

public interface IDocumentExtractor
{
    Task<DocumentExtractionResultDto> ExtractFieldsAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
