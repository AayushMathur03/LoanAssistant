namespace Loan.Application.Abstractions;

public record StorageResult(
    string StorageReference,
    string FilePath,
    long FileSizeBytes,
    string ContentType,
    string HashSha256);

public interface IDocumentStorageService
{
    Task<StorageResult> SaveDocumentAsync(
        string applicationId,
        string fileName,
        string contentType,
        Stream contentStream,
        CancellationToken cancellationToken = default);

    Task<Stream?> GetDocumentStreamAsync(
        string storageReference,
        CancellationToken cancellationToken = default);

    Task DeleteDocumentAsync(
        string storageReference,
        CancellationToken cancellationToken = default);
}
