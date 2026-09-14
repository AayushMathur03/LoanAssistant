using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Loan.Application.Documents;

public record FileValidationResult(
    bool IsValid,
    string SanitizedFileName,
    string DetectedContentType,
    string? FailureReason);

public static class DocumentUploadValidator
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB limit

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png", ".txt", ".csv", ".json", ".md"
    };

    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".sh", ".ps1", ".vbs", ".js", ".html", ".htm", ".php", ".asp", ".aspx", ".svg", ".jar"
    };

    private static readonly Dictionary<string, string> ExtensionToMimeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".md"] = "text/markdown"
    };

    public static FileValidationResult ValidateUpload(string rawFileName, string contentType, long fileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            return new FileValidationResult(false, "unknown.bin", "application/octet-stream", "Filename cannot be empty.");
        }

        if (fileSizeBytes <= 0)
        {
            return new FileValidationResult(false, rawFileName, contentType, "Uploaded file is empty (0 bytes).");
        }

        if (fileSizeBytes > MaxFileSizeBytes)
        {
            return new FileValidationResult(false, rawFileName, contentType, $"File size ({fileSizeBytes / (1024 * 1024)}MB) exceeds maximum limit of 10MB.");
        }

        // Check for path traversal attempts
        if (rawFileName.Contains("..") || rawFileName.Contains('/') || rawFileName.Contains('\\') || rawFileName.Contains('\0'))
        {
            return new FileValidationResult(false, "suspicious.bin", contentType, "Filename contains suspicious path traversal or null characters.");
        }

        var extension = Path.GetExtension(rawFileName);
        if (string.IsNullOrWhiteSpace(extension) || BlockedExtensions.Contains(extension) || !AllowedExtensions.Contains(extension))
        {
            return new FileValidationResult(false, rawFileName, contentType, $"File extension '{extension}' is not permitted.");
        }

        // Sanitize filename: allow alphanumeric, space, hyphens, underscores, dots
        var nameWithoutExt = Path.GetFileNameWithoutExtension(rawFileName);
        var sanitizedBase = Regex.Replace(nameWithoutExt, @"[^a-zA-Z0-9_\-\s]", "_").Trim();
        if (string.IsNullOrWhiteSpace(sanitizedBase))
        {
            sanitizedBase = "document";
        }
        var sanitizedFileName = $"{sanitizedBase}{extension.ToLowerInvariant()}";

        // Determine expected MIME type
        var expectedMime = ExtensionToMimeMap.GetValueOrDefault(extension, "application/octet-stream");

        return new FileValidationResult(true, sanitizedFileName, expectedMime, null);
    }
}
