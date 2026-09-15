using System.Text.RegularExpressions;
using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Domain.Documents;

namespace Loan.Infrastructure.Documents;

public class SyntheticDocumentExtractor : IDocumentExtractor
{
    public async Task<DocumentExtractionResultDto> ExtractFieldsAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var docId = $"DOC-EXT-{Guid.NewGuid():N}[..8]".ToUpperInvariant();
        var lowerName = fileName.ToLowerInvariant();
        var isLowConfidence = lowerName.Contains("lowconf") || lowerName.Contains("blurry") || lowerName.Contains("draft") || lowerName.Contains("scan");

        string contentText = string.Empty;
        if (documentStream != null && documentStream.CanRead)
        {
            try
            {
                using var reader = new StreamReader(documentStream, System.Text.Encoding.UTF8, leaveOpen: true);
                contentText = await reader.ReadToEndAsync(cancellationToken);
                if (documentStream.CanSeek)
                {
                    documentStream.Position = 0;
                }
            }
            catch { }
        }

        DocumentType docType;
        if (lowerName.Contains("paystub") || lowerName.Contains("payslip") || lowerName.Contains("salary") || contentText.Contains("Gross Pay", StringComparison.OrdinalIgnoreCase))
        {
            docType = DocumentType.Paystub;
        }
        else if (lowerName.Contains("w2") || lowerName.Contains("w-2") || lowerName.Contains("taxform") || contentText.Contains("W-2", StringComparison.OrdinalIgnoreCase))
        {
            docType = DocumentType.W2;
        }
        else if (lowerName.Contains("bank") || lowerName.Contains("statement") || contentText.Contains("Account Balance", StringComparison.OrdinalIgnoreCase))
        {
            docType = DocumentType.BankStatement;
        }
        else if (lowerName.Contains("license") || lowerName.Contains("passport") || lowerName.Contains("id") || contentText.Contains("Driver License", StringComparison.OrdinalIgnoreCase))
        {
            docType = DocumentType.DriverLicenseOrPassport;
        }
        else if (lowerName.Contains("tax") || lowerName.Contains("1040") || lowerName.Contains("return") || contentText.Contains("Tax Return", StringComparison.OrdinalIgnoreCase))
        {
            docType = DocumentType.TaxReturn;
        }
        else
        {
            docType = DocumentType.Generic;
        }

        var fields = new List<ExtractedFieldDto>();

        switch (docType)
        {
            case DocumentType.Paystub:
                var incomeValue = ExtractRegexMatch(contentText, @"(?:Gross Pay|Monthly Income|Gross Income)[:\s]+\$?([0-9\.,]+)", "12000.00");
                var employerValue = ExtractRegexMatch(contentText, @"Employer[:\s]+([A-Za-z0-9\s]+)", "TechCorp Global LLC");
                var employeeName = ExtractRegexMatch(contentText, @"Employee[:\s]+([A-Za-z\s]+)", "Alice Cooper");
                
                fields.Add(CreateField("FullName", employeeName, employeeName, isLowConfidence ? 0.80f : 0.98f, docId, $"Header: Employee Name: {employeeName}", false));
                fields.Add(CreateField("MonthlyGrossIncome", incomeValue, incomeValue, isLowConfidence ? 0.72f : 0.96f, docId, $"Line: Gross Pay: ${incomeValue}", false));
                fields.Add(CreateField("EmployerName", employerValue, employerValue, isLowConfidence ? 0.78f : 0.94f, docId, $"Header: Employer: {employerValue}", false));
                fields.Add(CreateField("PayPeriodEndingDate", isLowConfidence ? "2026-08-XX" : "2026-08-31", isLowConfidence ? "2026-08-XX" : "2026-08-31", isLowConfidence ? 0.65f : 0.91f, docId, "Line 2: Pay Period Ending: 2026-08-31", false, isValidFormat: !isLowConfidence));
                fields.Add(CreateField("SSN", "123-45-6789", "***-**-6789", isLowConfidence ? 0.75f : 0.98f, docId, "Line 1: SSN: XXX-XX-6789", isSensitive: true));
                break;

            case DocumentType.W2:
                var wagesValue = ExtractRegexMatch(contentText, @"Wages[:\s]+\$?([0-9\.,]+)", "144000.00");
                var w2Employee = ExtractRegexMatch(contentText, @"Employee[:\s]+([A-Za-z\s]+)", "Alice Cooper");
                var w2Employer = ExtractRegexMatch(contentText, @"Employer[:\s]+([A-Za-z0-9\s]+)", "TechCorp Global LLC");
                fields.Add(CreateField("FullName", w2Employee, w2Employee, isLowConfidence ? 0.82f : 0.99f, docId, $"Box e: Employee Name: {w2Employee}", false));
                fields.Add(CreateField("EmployerName", w2Employer, w2Employer, isLowConfidence ? 0.81f : 0.97f, docId, $"Box c: Employer Name: {w2Employer}", false));
                fields.Add(CreateField("AnnualWages", wagesValue, wagesValue, isLowConfidence ? 0.74f : 0.97f, docId, $"Box 1: Wages, tips, other comp.: ${wagesValue}", false));
                fields.Add(CreateField("EmployerEin", "12-3456789", "XX-XXX6789", isLowConfidence ? 0.80f : 0.95f, docId, "Box b: Employer Identification Number: 12-3456789", isSensitive: true));
                fields.Add(CreateField("TaxYear", "2025", "2025", isLowConfidence ? 0.70f : 0.99f, docId, "Header: Form W-2 Wage Statement 2025", false));
                break;

            case DocumentType.BankStatement:
                var balanceValue = ExtractRegexMatch(contentText, @"Avg Balance[:\s]+\$?([0-9\.,]+)", "45000.00");
                var bankCustomer = ExtractRegexMatch(contentText, @"Account Holder[:\s]+([A-Za-z\s]+)", "Alice Cooper");
                fields.Add(CreateField("FullName", bankCustomer, bankCustomer, isLowConfidence ? 0.82f : 0.97f, docId, $"Statement Header: {bankCustomer}", false));
                fields.Add(CreateField("AverageMonthlyBalance", balanceValue, balanceValue, isLowConfidence ? 0.76f : 0.93f, docId, $"Summary: 90-Day Avg Balance: ${balanceValue}", false));
                fields.Add(CreateField("AccountNumber", "9876543210", "******3210", isLowConfidence ? 0.79f : 0.96f, docId, "Account Number: *******3210", isSensitive: true));
                fields.Add(CreateField("StatedDebts", "3000.00", "3000.00", isLowConfidence ? 0.68f : 0.89f, docId, "Monthly Recurring Outflow: $3,000.00", false));
                break;

            case DocumentType.DriverLicenseOrPassport:
                var nameValue = ExtractRegexMatch(contentText, @"Name[:\s]+([A-Za-z\s,]+)", "Alice Cooper");
                fields.Add(CreateField("FullName", nameValue, nameValue, isLowConfidence ? 0.70f : 0.98f, docId, $"Field 1: Name: {nameValue}", false));
                fields.Add(CreateField("DocumentNumber", "DL-987654321", "*****4321", isLowConfidence ? 0.71f : 0.95f, docId, "Field 4d: DL No: DL-987654321", isSensitive: true));
                fields.Add(CreateField("DateOfBirth", "1988-05-14", "1988-05-14", isLowConfidence ? 0.64f : 0.92f, docId, "Field 3: DOB: 05/14/1988", false));
                fields.Add(CreateField("ExpirationDate", "2029-05-14", "2029-05-14", isLowConfidence ? 0.69f : 0.94f, docId, "Field 4b: EXP: 05/14/2029", false));
                break;

            case DocumentType.TaxReturn:
                var agiValue = ExtractRegexMatch(contentText, @"AGI[:\s]+\$?([0-9\.,]+)", "142500.00");
                var taxName = ExtractRegexMatch(contentText, @"Taxpayer[:\s]+([A-Za-z\s]+)", "Alice Cooper");
                fields.Add(CreateField("FullName", taxName, taxName, isLowConfidence ? 0.80f : 0.98f, docId, $"Form 1040 Header: {taxName}", false));
                fields.Add(CreateField("AdjustedGrossIncome", agiValue, agiValue, isLowConfidence ? 0.73f : 0.95f, docId, $"Line 11: Adjusted Gross Income: ${agiValue}", false));
                fields.Add(CreateField("TaxableIncome", "118000.00", "118000.00", isLowConfidence ? 0.70f : 0.94f, docId, "Line 15: Taxable Income: $118,000.00", false));
                fields.Add(CreateField("TaxYear", "2025", "2025", isLowConfidence ? 0.70f : 0.99f, docId, "Form 1040 Year: 2025", false));
                fields.Add(CreateField("SelfEmployedIndicator", "No", "No", isLowConfidence ? 0.65f : 0.90f, docId, "Schedule C Attached: None", false));
                break;

            default:
                fields.Add(CreateField("StatedIncome", "10000.00", "10000.00", isLowConfidence ? 0.60f : 0.88f, docId, "Extracted text snippet: Stated Income $10,000", false));
                fields.Add(CreateField("StatedDebts", "2500.00", "2500.00", isLowConfidence ? 0.60f : 0.87f, docId, "Extracted text snippet: Monthly Debts $2,500", false));
                break;
        }

        var result = new DocumentExtractionResultDto(
            DocumentId: docId,
            DocumentType: docType.ToString(),
            Success: true,
            ErrorMessage: null,
            ExtractedFields: fields);

        return result;
    }

    private static string ExtractRegexMatch(string text, string pattern, string fallbackValue)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallbackValue;
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success && match.Groups.Count > 1)
        {
            var val = match.Groups[1].Value.Trim();
            return string.IsNullOrWhiteSpace(val) ? fallbackValue : val;
        }
        return fallbackValue;
    }

    private static ExtractedFieldDto CreateField(
        string fieldName,
        string rawValue,
        string displayValue,
        float confidenceScore,
        string docId,
        string provenance,
        bool isSensitive,
        bool isValidFormat = true)
    {
        bool needsConfirmation = confidenceScore < 0.85f || !isValidFormat;
        var maskedValue = isSensitive ? ExtractedFieldRecord.MaskSensitiveValue(rawValue) : displayValue;

        return new ExtractedFieldDto(
            FieldName: fieldName,
            RawValue: maskedValue,
            ConfidenceScore: confidenceScore,
            SourceDocumentId: docId,
            NeedsConfirmation: needsConfirmation,
            ConfirmationStatus: FieldConfirmationStatus.Unconfirmed,
            ConfirmedBy: null);
    }
}
