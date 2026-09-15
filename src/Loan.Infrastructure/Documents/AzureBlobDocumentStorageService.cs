using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Loan.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Loan.Infrastructure.Documents;

public class AzureBlobDocumentStorageService : IDocumentStorageService
{
    private readonly BlobContainerClient? _containerClient;
    private readonly LocalFileDocumentStorageService _fallbackService;
    private readonly ILogger<AzureBlobDocumentStorageService> _logger;
    private readonly bool _useAzureBlob;

    public AzureBlobDocumentStorageService(
        IConfiguration configuration,
        ILogger<AzureBlobDocumentStorageService> logger,
        ILogger<LocalFileDocumentStorageService> fallbackLogger)
    {
        _logger = logger;
        _fallbackService = new LocalFileDocumentStorageService(fallbackLogger);

        var connectionString = configuration["AzureStorage:ConnectionString"] 
            ?? configuration.GetConnectionString("AzureStorage")
            ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

        var containerName = configuration["AzureStorage:ContainerName"] ?? "loan-documents";

        if (!string.IsNullOrWhiteSpace(connectionString) && !connectionString.Contains("YOUR_"))
        {
            try
            {
                var blobServiceClient = new BlobServiceClient(connectionString);
                _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                _containerClient.CreateIfNotExists(PublicAccessType.None);
                _useAzureBlob = true;
                _logger.LogInformation("Azure Blob Storage initialized with container '{ContainerName}' (Private)", containerName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Azure Blob Storage. Falling back to secure local file storage.");
                _useAzureBlob = false;
            }
        }
        else
        {
            _logger.LogInformation("Azure Storage connection string not configured. Using local file document storage fallback.");
            _useAzureBlob = false;
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

        if (!_useAzureBlob || _containerClient == null)
        {
            return await _fallbackService.SaveDocumentAsync(applicationId, fileName, contentType, contentStream, cancellationToken);
        }

        var storageRef = $"DOC-BLOB-{Guid.NewGuid():N}";
        var blobName = $"{SanitizeSegment(applicationId)}/{storageRef}_{SanitizeSegment(fileName)}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        using var memoryStream = new MemoryStream();
        using var sha256 = SHA256.Create();

        var buffer = new byte[8192];
        int bytesRead;
        long totalBytes = 0;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
            totalBytes += bytesRead;
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        var hashHex = Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant();

        memoryStream.Position = 0;

        var blobUploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            },
            Metadata = new Dictionary<string, string>
            {
                { "ApplicationId", applicationId },
                { "StorageRef", storageRef },
                { "Sha256", hashHex }
            }
        };

        await blobClient.UploadAsync(memoryStream, blobUploadOptions, cancellationToken);

        _logger.LogInformation("Document uploaded to Azure Blob. AppId: {AppId}, Blob: {Blob}, Size: {Size} bytes, Hash: {Hash}",
            applicationId, blobName, totalBytes, hashHex);

        return new StorageResult(storageRef, blobClient.Uri.ToString(), totalBytes, contentType, hashHex);
    }

    public async Task<Stream?> GetDocumentStreamAsync(
        string storageReference,
        CancellationToken cancellationToken = default)
    {
        if (!_useAzureBlob || _containerClient == null)
        {
            return await _fallbackService.GetDocumentStreamAsync(storageReference, cancellationToken);
        }

        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: null, cancellationToken: cancellationToken))
        {
            if (blobItem.Name.Contains(storageReference))
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                var downloadResponse = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
                return downloadResponse.Value.Content;
            }
        }

        return await _fallbackService.GetDocumentStreamAsync(storageReference, cancellationToken);
    }

    public async Task DeleteDocumentAsync(
        string storageReference,
        CancellationToken cancellationToken = default)
    {
        if (!_useAzureBlob || _containerClient == null)
        {
            await _fallbackService.DeleteDocumentAsync(storageReference, cancellationToken);
            return;
        }

        await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: null, cancellationToken: cancellationToken))
        {
            if (blobItem.Name.Contains(storageReference))
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                _logger.LogInformation("Deleted Azure Blob: {BlobName}", blobItem.Name);
            }
        }

        await _fallbackService.DeleteDocumentAsync(storageReference, cancellationToken);
    }

    private static string SanitizeSegment(string segment)
    {
        return string.Join("_", segment.Split(Path.GetInvalidFileNameChars()));
    }
}
