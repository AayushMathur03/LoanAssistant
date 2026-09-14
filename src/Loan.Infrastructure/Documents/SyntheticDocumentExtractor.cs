using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Domain.Applications;

namespace Loan.Infrastructure.Documents;

public class SyntheticDocumentExtractor : IDocumentExtractor
{
    public Task<IEnumerable<ExtractedFieldDto>> ExtractFieldsAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var docId = $"DOC-EXT-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var isPaystub = fileName.Contains("paystub", StringComparison.OrdinalIgnoreCase) || fileName.Contains("income", StringComparison.OrdinalIgnoreCase);
        var isIdDoc = fileName.Contains("id", StringComparison.OrdinalIgnoreCase) || fileName.Contains("passport", StringComparison.OrdinalIgnoreCase) || fileName.Contains("license", StringComparison.OrdinalIgnoreCase);

        var fields = new List<ExtractedFieldDto>();

        if (isPaystub)
        {
            fields.Add(new ExtractedFieldDto("MonthlyGrossIncome", "10000.00", 0.95f, docId, false, FieldConfirmationStatus.Unconfirmed, null));
            fields.Add(new ExtractedFieldDto("EmployerName", "TechCorp LLC", 0.92f, docId, false, FieldConfirmationStatus.Unconfirmed, null));
            fields.Add(new ExtractedFieldDto("PayPeriodEndingDate", "2026-08-31", 0.78f, docId, true, FieldConfirmationStatus.Unconfirmed, null)); // Low confidence (<0.85) triggers confirmation
        }
        else if (isIdDoc)
        {
            fields.Add(new ExtractedFieldDto("FullName", "John Doe", 0.98f, docId, false, FieldConfirmationStatus.Unconfirmed, null));
            fields.Add(new ExtractedFieldDto("SyntheticId", "SYN-123456", 0.96f, docId, false, FieldConfirmationStatus.Unconfirmed, null));
            fields.Add(new ExtractedFieldDto("DateOfBirth", "1988-05-14", 0.81f, docId, true, FieldConfirmationStatus.Unconfirmed, null)); // Low confidence (<0.85)
        }
        else
        {
            // Generic extracted fields for demonstration
            fields.Add(new ExtractedFieldDto("StatedIncome", "12000.00", 0.90f, docId, false, FieldConfirmationStatus.Unconfirmed, null));
            fields.Add(new ExtractedFieldDto("StatedDebts", "3000.00", 0.88f, docId, false, FieldConfirmationStatus.Unconfirmed, null));
            fields.Add(new ExtractedFieldDto("PropertyAddress", "123 Main St, Springfield", 0.72f, docId, true, FieldConfirmationStatus.Unconfirmed, null)); // Low confidence
        }

        return Task.FromResult<IEnumerable<ExtractedFieldDto>>(fields);
    }
}
