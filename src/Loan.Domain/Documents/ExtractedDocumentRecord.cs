namespace Loan.Domain.Documents;

public enum DocumentType
{
    Paystub,
    W2,
    BankStatement,
    DriverLicenseOrPassport,
    TaxReturn,
    Generic
}

public enum ExtractionStatus
{
    Pending,
    Extracted,
    FailedValidation,
    RequiresHumanReview,
    Confirmed
}

public class ExtractedDocumentRecord
{
    public string DocumentId { get; }
    public string ApplicationId { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long FileSizeBytes { get; }
    public string StorageReference { get; }
    public string HashSha256 { get; }
    public DocumentType DocumentType { get; }
    public ExtractionStatus Status { get; private set; }
    public DateTime UploadedAtUtc { get; }

    public IReadOnlyList<ExtractedFieldRecord> Fields => _fields.AsReadOnly();
    private readonly List<ExtractedFieldRecord> _fields = new();

    public ExtractedDocumentRecord(
        string documentId,
        string applicationId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string storageReference,
        string hashSha256,
        DocumentType documentType,
        DateTime uploadedAtUtc)
    {
        DocumentId = documentId;
        ApplicationId = applicationId;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        StorageReference = storageReference;
        HashSha256 = hashSha256;
        DocumentType = documentType;
        Status = ExtractionStatus.Pending;
        UploadedAtUtc = uploadedAtUtc;
    }

    public void AddExtractedFields(IEnumerable<ExtractedFieldRecord> fields)
    {
        _fields.AddRange(fields);
        Status = _fields.Any(f => f.NeedsConfirmation) ? ExtractionStatus.RequiresHumanReview : ExtractionStatus.Extracted;
    }

    public void UpdateStatus(ExtractionStatus status)
    {
        Status = status;
    }
}
