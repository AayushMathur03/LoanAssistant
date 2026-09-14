using Loan.Application.Abstractions;
using Loan.Application.DTOs;

namespace Loan.Application.ProductAdvice;

public record AskProductQuestionQuery(
    string Question,
    string? TargetProductId = null,
    string? PolicyVersion = null);

public record ProductAdviceResponseDto(
    string Answer,
    IReadOnlyList<CitationDto> Citations,
    string EffectivePolicyVersion,
    bool HasSufficientEvidence,
    string NonApprovalDisclaimer);

public class AskProductQuestionQueryHandler
{
    private readonly IPolicyRetriever _policyRetriever;
    private readonly IChatModel _chatModel;

    public AskProductQuestionQueryHandler(IPolicyRetriever policyRetriever, IChatModel chatModel)
    {
        _policyRetriever = policyRetriever;
        _chatModel = chatModel;
    }

    public async Task<ProductAdviceResponseDto> HandleAsync(AskProductQuestionQuery query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
        {
            throw new ArgumentException("Question cannot be empty.", nameof(query));
        }

        // 1. Retrieve policy content from RAG retriever
        var searchResults = (await _policyRetriever.SearchPolicyAsync(
            query: query.Question,
            targetProductId: query.TargetProductId,
            effectiveVersion: query.PolicyVersion,
            topK: 3,
            cancellationToken: cancellationToken)).ToList();

        const string disclaimer = "Disclaimer: Product explanations are for informational purposes only and do NOT constitute a loan commitment, rate lock, or pre-approval decision.";

        if (!searchResults.Any())
        {
            return new ProductAdviceResponseDto(
                Answer: "We could not find sufficient matching policy evidence in our current knowledge base to answer your question.",
                Citations: Array.Empty<CitationDto>(),
                EffectivePolicyVersion: query.PolicyVersion ?? "v1.0",
                HasSufficientEvidence: false,
                NonApprovalDisclaimer: disclaimer);
        }

        // 2. Build system context with strict RAG grounding
        var citations = searchResults.Select(r => r.Citation).ToList();
        var contextText = string.Join("\n\n", searchResults.Select(r => $"[Document: {r.Title} (Ver: {r.Version}, Section: {r.Section})]\n{r.Content}"));

        var messages = new List<ChatMessage>
        {
            new("system", "You are a grounded loan assistant. Answer the user's question using ONLY the provided policy context below. " +
                          "Every fact or rule in your response MUST cite the document title and section. " +
                          "If the context does not contain enough information to answer, state clearly that you cannot verify.\n\n" +
                          $"Policy Context:\n{contextText}"),
            new("user", query.Question)
        };

        var answer = await _chatModel.GenerateCompletionAsync(messages, temperature: 0.1, cancellationToken: cancellationToken);

        return new ProductAdviceResponseDto(
            Answer: answer,
            Citations: citations,
            EffectivePolicyVersion: searchResults.First().Version,
            HasSufficientEvidence: true,
            NonApprovalDisclaimer: disclaimer);
    }
}
