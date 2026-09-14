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

        foreach (var doc in docs)
        {
            foreach (var field in doc.Fields)
            {
                if (field.Status == Domain.Documents.FieldConfirmationStatus.Unconfirmed)
                {
                    unresolvedFields.Add($"{doc.DocumentType} field '{field.FieldName}' (Value: '{field.DisplayValue}')");
                }
                if (field.ConfidenceScore < 0.85f)
                {
                    lowConfidenceFields.Add($"{doc.DocumentType} field '{field.FieldName}' (Confidence: {field.ConfidenceScore:P0})");
                }
            }
        }

        bool hasAllDocs = docs.Count >= 2 && unresolvedFields.Count == 0;

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
