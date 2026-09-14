using System.Security.Cryptography;
using Loan.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Loan.Infrastructure.Documents;

public class LocalFileDocumentStorageService : IDocumentStorageService
{
    private readonly string _baseStorageDirectory;
    private readonly ILogger<LocalFileDocumentStorageService> _logger;

    public LocalFileDocumentStorageService(ILogger<LocalFileDocumentStorageService> logger, string? baseDirectory = null)
    {
        _logger = logger;
        _baseStorageDirectory = baseDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Uploads");

        if (!Directory.Exists(_baseStorageDirectory))
        {
            Directory.CreateDirectory(_baseStorageDirectory);
        }
    }

    public async Task<StorageResult> SaveDocumentAsync(
        string applicationId,
        string fileName,
        string contentType,
        Stream contentStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(contentStream);

        var sanitizedAppDir = Path.Combine(_baseStorageDirectory, SanitizePathSegment(applicationId));
        if (!Directory.Exists(sanitizedAppDir))
        {
            Directory.CreateDirectory(sanitizedAppDir);
        }

        var storageRef = $"DOC-STORE-{Guid.NewGuid():N}";
        var targetFilePath = Path.Combine(sanitizedAppDir, $"{storageRef}.bin");

        using var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        using var sha256 = SHA256.Create();

        var buffer = new byte[8192];
        int bytesRead;
        long totalBytes = 0;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            totalBytes += bytesRead;
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hashBytes = sha256.Hash ?? Array.Empty<byte>();
        var hashHex = Convert.ToHexString(hashBytes).ToLowerInvariant();

        _logger.LogInformation("Document saved successfully. ApplicationId: {AppId}, Ref: {Ref}, Size: {Size} bytes, Hash: {Hash}",
            applicationId, storageRef, totalBytes, hashHex);

        return new StorageResult(storageRef, targetFilePath, totalBytes, contentType, hashHex);
    }

    public Task<Stream?> GetDocumentStreamAsync(
        string storageReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageReference);

        var sanitizedRef = SanitizePathSegment(storageReference);
        var matchingFiles = Directory.GetFiles(_baseStorageDirectory, $"{sanitizedRef}.bin", SearchOption.AllDirectories);

        if (matchingFiles.Length == 0)
        {
            _logger.LogWarning("Storage reference '{Ref}' not found in local document store.", storageReference);
            return Task.FromResult<Stream?>(null);
        }

        var stream = new FileStream(matchingFiles[0], FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteDocumentAsync(
        string storageReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageReference);

        var sanitizedRef = SanitizePathSegment(storageReference);
        var matchingFiles = Directory.GetFiles(_baseStorageDirectory, $"{sanitizedRef}.bin", SearchOption.AllDirectories);

        foreach (var file in matchingFiles)
        {
            try
            {
                File.Delete(file);
                _logger.LogInformation("Deleted local document file: {FilePath}", file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document file: {FilePath}", file);
            }
        }

        return Task.CompletedTask;
    }

    private static string SanitizePathSegment(string segment)
    {
        return string.Join("_", segment.Split(Path.GetInvalidFileNameChars()));
    }
}
