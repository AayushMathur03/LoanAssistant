using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Domain.Applications;
using Loan.Domain.Documents;
using Microsoft.Extensions.Logging;

namespace Loan.Infrastructure.Documents;

public class AzureOpenAiDocumentExtractor : IDocumentExtractor
{
    private readonly IChatModel _chatModel;
    private readonly SyntheticDocumentExtractor _fallbackExtractor;
    private readonly ILogger<AzureOpenAiDocumentExtractor> _logger;

    public AzureOpenAiDocumentExtractor(
        IChatModel chatModel,
        SyntheticDocumentExtractor fallbackExtractor,
        ILogger<AzureOpenAiDocumentExtractor> logger)
    {
        _chatModel = chatModel;
        _fallbackExtractor = fallbackExtractor;
        _logger = logger;
    }

    public async Task<DocumentExtractionResultDto> ExtractFieldsAsync(
        Stream documentStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentStream);

        // Read stream content as text
        long initialPosition = documentStream.CanSeek ? documentStream.Position : 0;
        string documentText;
        using (var reader = new StreamReader(documentStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            documentText = await reader.ReadToEndAsync(cancellationToken);
        }

        if (documentStream.CanSeek)
        {
            documentStream.Position = initialPosition;
        }

        // If text is empty, fall back immediately
        if (string.IsNullOrWhiteSpace(documentText))
        {
            _logger.LogInformation("Document text was empty; falling back to synthetic extractor for '{FileName}'.", fileName);
            return await _fallbackExtractor.ExtractFieldsAsync(documentStream, fileName, contentType, cancellationToken);
        }

        try
        {
            var systemPrompt = 
@"You are an authoritative AI document intelligence engine for a regulated financial lending institution.
Your job is to parse the uploaded document text and extract structured financial and identity fields.

Analyze the document text and extract all relevant fields matching the document type:
- Paystub / W2: MonthlyGrossIncome, FullName, EmployerName, PayPeriod, NetPay, YtdGross, EmployerEin
- BankStatement: StartingBalance, EndingBalance, AverageDailyBalance, TotalDeposits, TotalWithdrawals, AccountNumber
- DriverLicenseOrPassport: FullName, DocumentNumber, DateOfBirth, ExpirationDate
- TaxReturn: FullName, AdjustedGrossIncome, TaxableIncome, TaxYear, SelfEmployedIndicator

Rules for Confidence Scoring:
- Score 0.90 - 1.00: Clear, unambiguous, complete, and mathematically consistent data.
- Score 0.60 - 0.84: Smudged, blurry, hand-written, partial, ambiguous, or format-questionable data (will require human applicant confirmation).
- If the document states low confidence or smudged text, assign confidence < 0.80.

CRITICAL INSTRUCTION: Respond ONLY with a single JSON object matching this exact schema:
{
  ""documentType"": ""Paystub"" | ""BankStatement"" | ""DriverLicenseOrPassport"" | ""TaxReturn"" | ""Other"",
  ""fields"": [
    {
      ""fieldName"": ""string"",
      ""rawValue"": ""string"",
      ""confidenceScore"": 0.95,
      ""evidenceSnippet"": ""string snippet from document"",
      ""isSensitive"": false
    }
  ]
}";

            var userPrompt = $"File Name: {fileName}\nMIME Type: {contentType}\n\nDocument Text:\n{documentText}";

            var messages = new List<ChatMessage>
            {
                new("system", systemPrompt),
                new("user", userPrompt)
            };

            _logger.LogInformation("Invoking GPT-4o for document extraction on '{FileName}'...", fileName);
            var responseJson = await _chatModel.GenerateCompletionAsync(messages, temperature: 0.1, cancellationToken);

            if (string.IsNullOrWhiteSpace(responseJson) || responseJson.StartsWith("Degraded Service:"))
            {
                throw new InvalidOperationException("LLM document extraction returned empty or degraded service response.");
            }

            var cleanedJson = responseJson.Trim();
            if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                cleanedJson = cleanedJson.Substring(7);
            }
            else if (cleanedJson.StartsWith("```"))
            {
                cleanedJson = cleanedJson.Substring(3);
            }
            if (cleanedJson.EndsWith("```"))
            {
                cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
            }
            cleanedJson = cleanedJson.Trim();

            var parsed = JsonSerializer.Deserialize<LlmExtractionResponse>(cleanedJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed != null && parsed.Fields != null && parsed.Fields.Count > 0)
            {
                var docId = $"DOC-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
                var extractedFields = parsed.Fields.Select(f =>
                {
                    bool isLowConfidence = f.ConfidenceScore < 0.85f;
                    return new ExtractedFieldDto(
                        FieldName: f.FieldName,
                        RawValue: f.RawValue,
                        ConfidenceScore: f.ConfidenceScore,
                        SourceDocumentId: docId,
                        NeedsConfirmation: isLowConfidence,
                        ConfirmationStatus: isLowConfidence ? FieldConfirmationStatus.Unconfirmed : FieldConfirmationStatus.ConfirmedByApplicant,
                        ConfirmedBy: isLowConfidence ? null : "GPT4o-AutoVerified");
                }).ToList();

                _logger.LogInformation("GPT-4o successfully extracted {Count} fields from '{FileName}' ({DocType}).",
                    extractedFields.Count, fileName, parsed.DocumentType);

                return new DocumentExtractionResultDto(
                    DocumentId: docId,
                    DocumentType: parsed.DocumentType ?? "Other",
                    Success: true,
                    ErrorMessage: null,
                    ExtractedFields: extractedFields);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OpenAI document extraction on '{FileName}' failed or was unavailable. Falling back to SyntheticDocumentExtractor.", fileName);
        }

        // Resilient Fallback
        if (documentStream.CanSeek)
        {
            documentStream.Position = initialPosition;
        }
        return await _fallbackExtractor.ExtractFieldsAsync(documentStream, fileName, contentType, cancellationToken);
    }

    private class LlmExtractionResponse
    {
        [JsonPropertyName("documentType")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("fields")]
        public List<LlmExtractedField>? Fields { get; set; }
    }

    private class LlmExtractedField
    {
        [JsonPropertyName("fieldName")]
        public string FieldName { get; set; } = string.Empty;

        [JsonPropertyName("rawValue")]
        public string RawValue { get; set; } = string.Empty;

        [JsonPropertyName("confidenceScore")]
        public float ConfidenceScore { get; set; } = 1.0f;

        [JsonPropertyName("evidenceSnippet")]
        public string? EvidenceSnippet { get; set; }

        [JsonPropertyName("isSensitive")]
        public bool IsSensitive { get; set; }
    }
}
