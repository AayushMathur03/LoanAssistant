using Loan.Application.Abstractions;
using Loan.Domain.Applications;

namespace Loan.Application.Agents;

public class DocumentAnalysisAgent
{
    private readonly IChatModel _chatModel;

    public DocumentAnalysisAgent(IChatModel chatModel)
    {
        _chatModel = chatModel;
    }

    public async Task<DocumentAnalysisResult> AnalyzeAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        var docs = application.Documents.ToList();
        var unresolvedFields = new List<string>();
        var lowConfidenceFields = new List<string>();

        // Check product-specific required documents
        var missingDocs = new List<string>();
        bool hasIncomeDoc = docs.Any(d => d.DocumentType is Domain.Documents.DocumentType.Paystub or Domain.Documents.DocumentType.W2 or Domain.Documents.DocumentType.TaxReturn);
        bool hasIdDoc = docs.Any(d => d.DocumentType == Domain.Documents.DocumentType.DriverLicenseOrPassport);
        bool hasBankDoc = docs.Any(d => d.DocumentType == Domain.Documents.DocumentType.BankStatement);

        if (!application.Facts.IsIncomeVerified && !hasIncomeDoc) missingDocs.Add("Income Verification (Recent Paystub or W-2)");
        if (!application.Facts.IsIdentityVerified && !hasIdDoc) missingDocs.Add("Identity Verification (Driver License or Passport)");
        if (docs.Count > 0 && (application.ProductRules.ProductId == "MORTGAGE-STD" || application.ProductRules.RequiresPropertyValuation))
        {
            if (!hasBankDoc) missingDocs.Add("Asset Verification (60-Day Bank Statement)");
        }

        foreach (var doc in docs)
        {
            foreach (var field in doc.Fields)
            {
                if (field.NeedsConfirmation)
                {
                    unresolvedFields.Add($"{doc.DocumentType} field '{field.FieldName}' requires confirmation (Confidence: {field.ConfidenceScore:P0})");
                }
                if (field.ConfidenceScore < 0.85f)
                {
                    lowConfidenceFields.Add($"{doc.DocumentType} field '{field.FieldName}' (Confidence: {field.ConfidenceScore:P0})");
                }
            }
        }

        foreach (var m in missingDocs)
        {
            unresolvedFields.Add($"Missing required document: {m}");
        }

        bool hasAllDocs = missingDocs.Count == 0 && unresolvedFields.Count == 0;

        var contextPrompt = $"Application ID: {application.ApplicationId}\n" +
                            $"Total Documents Uploaded: {docs.Count}\n" +
                            $"Unresolved Fields: {(unresolvedFields.Count > 0 ? string.Join(", ", unresolvedFields) : "None")}\n" +
                            $"Low Confidence Fields: {(lowConfidenceFields.Count > 0 ? string.Join(", ", lowConfidenceFields) : "None")}";

        var messages = new List<ChatMessage>
        {
            new("system", AgentPrompts.DocumentAgentSystemPrompt),
            new("user", $"Analyze document health for this application:\n{contextPrompt}")
        };

        var notes = await _chatModel.GenerateCompletionAsync(messages, temperature: 0.1, cancellationToken: cancellationToken);

        return new DocumentAnalysisResult(
            ApplicationId: application.ApplicationId,
            HasAllRequiredDocuments: hasAllDocs,
            TotalDocumentsUploaded: docs.Count,
            UnresolvedFields: unresolvedFields,
            LowConfidenceFields: lowConfidenceFields,
            AnalysisNotes: notes);
    }
}
