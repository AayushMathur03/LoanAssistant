using Loan.Application.Documents;
using NUnit.Framework;

namespace Loan.Application.Tests;

[TestFixture]
public class DocumentStorageTests
{
    [Test]
    public void DocumentUploadValidator_ValidPdf_ShouldPassValidation()
    {
        var result = DocumentUploadValidator.ValidateUpload("paystub.pdf", "application/pdf", 1024 * 50);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.SanitizedFileName, Is.EqualTo("paystub.pdf"));
        Assert.That(result.DetectedContentType, Is.EqualTo("application/pdf"));
        Assert.That(result.FailureReason, Is.Null);
    }

    [Test]
    public void DocumentUploadValidator_PathTraversalAttempt_ShouldBeRejected()
    {
        var result = DocumentUploadValidator.ValidateUpload("../../etc/passwd.txt", "text/plain", 500);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.FailureReason, Does.Contain("suspicious path traversal"));
    }

    [Test]
    public void DocumentUploadValidator_BlockedExecutableExtension_ShouldBeRejected()
    {
        var result = DocumentUploadValidator.ValidateUpload("malicious_script.exe", "application/x-msdownload", 1000);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.FailureReason, Does.Contain("not permitted"));
    }

    [Test]
    public void DocumentUploadValidator_OversizedFile_ShouldBeRejected()
    {
        long giantSize = 15 * 1024 * 1024; // 15 MB
        var result = DocumentUploadValidator.ValidateUpload("large_bank_statement.pdf", "application/pdf", giantSize);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.FailureReason, Does.Contain("exceeds maximum limit"));
    }
}
