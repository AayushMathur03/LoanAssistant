using System.Text;
using Loan.Infrastructure.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace Loan.IntegrationTests;

[TestFixture]
public class AzureBlobStorageLiveTests
{
    private IConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        string[] candidates = [
            Path.Combine(AppContext.BaseDirectory, "../../../../../src/Loan.Web"),
            Path.Combine(AppContext.BaseDirectory, "../../../../src/Loan.Web"),
            Path.Combine(AppContext.BaseDirectory, "../../../src/Loan.Web"),
            Path.Combine(Directory.GetCurrentDirectory(), "src/Loan.Web"),
            @"C:\Development\LoanAssistant\src\Loan.Web"
        ];

        var basePath = candidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "appsettings.json"))) 
            ?? Directory.GetCurrentDirectory();

        _configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets("LoanAssistant-Web-87612345-EF89")
            .AddEnvironmentVariables()
            .Build();
    }

    [Test]
    public async Task AzureBlobStorage_WithConfiguredConnectionString_UploadsToLiveAzureBlobSuccessfully()
    {
        var connStr = _configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("YOUR-", StringComparison.OrdinalIgnoreCase) || connStr.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore("Azure Storage connection string not configured.");
            return;
        }

        var service = new AzureBlobDocumentStorageService(
            _configuration,
            NullLogger<AzureBlobDocumentStorageService>.Instance,
            NullLogger<LocalFileDocumentStorageService>.Instance);

        var testContent = "Apex Loan Assistant - Live Azure Blob Storage Test Payload " + Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(testContent));

        var appId = "APP-LIVE-AZURE-TEST";
        var fileName = "LiveStorageVerificationTest.txt";

        var result = await service.SaveDocumentAsync(appId, fileName, "text/plain", stream);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.StorageReference, Does.StartWith("DOC-BLOB-"), "Expected real Azure Blob Storage reference starting with DOC-BLOB-");
        Assert.That(result.HashSha256, Is.Not.Null.And.Not.Empty);
        Assert.That(result.FileSizeBytes, Is.EqualTo(Encoding.UTF8.GetByteCount(testContent)));
    }
}
