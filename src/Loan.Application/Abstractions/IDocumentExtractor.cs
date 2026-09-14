using Loan.Application.DTOs;

namespace Loan.Application.Abstractions;

public interface IDocumentExtractor
{
    Task<IEnumerable<ExtractedFieldDto>> ExtractFieldsAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
