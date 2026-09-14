using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Application.ProductAdvice;

namespace Loan.Application.Tests;

[TestFixture]
public class ProductAdviceHandlerTests
{
    private class FakePolicyRetriever : IPolicyRetriever
    {
        public bool ReturnResults { get; set; } = true;

        public Task<IEnumerable<PolicySearchResultDto>> SearchPolicyAsync(
            string query,
            string? targetProductId = null,
            string? effectiveVersion = null,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            if (!ReturnResults)
            {
                return Task.FromResult<IEnumerable<PolicySearchResultDto>>(Array.Empty<PolicySearchResultDto>());
            }

            var citation = new CitationDto("Mortgage Guidelines", "v1.2", "Section 2.4", "Maximum LTV ratio is 80%.");
            var result = new PolicySearchResultDto(
                DocumentId: "DOC-001",
                Title: "Mortgage Guidelines",
                Version: "v1.2",
                Section: "Section 2.4",
                Content: "Maximum LTV ratio is 80% for standard residential mortgages.",
                SimilarityScore: 0.92,
                Citation: citation);

            return Task.FromResult<IEnumerable<PolicySearchResultDto>>(new[] { result });
        }
    }

    private class FakeChatModel : IChatModel
    {
        public Task<string> GenerateCompletionAsync(IEnumerable<ChatMessage> messages, double temperature = 0.2, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Per Mortgage Guidelines (v1.2, Section 2.4), the maximum LTV ratio allowed is 80%.");
        }

        public Task<T> GenerateStructuredAsync<T>(IEnumerable<ChatMessage> messages, double temperature = 0.1, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async IAsyncEnumerable<string> StreamCompletionAsync(
            IEnumerable<ChatMessage> messages,
            double temperature = 0.2,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return "Per Mortgage Guidelines (v1.2, Section 2.4), the maximum LTV ratio allowed is 80%.";
            await Task.CompletedTask;
        }
    }

    [Test]
    public async Task HandleAsync_WithMatchingPolicy_ShouldReturnCitedAnswer()
    {
        var retriever = new FakePolicyRetriever();
        var chatModel = new FakeChatModel();
        var handler = new AskProductQuestionQueryHandler(retriever, chatModel);

        var query = new AskProductQuestionQuery("What is the maximum LTV ratio?");
        var result = await handler.HandleAsync(query);

        Assert.That(result.HasSufficientEvidence, Is.True);
        Assert.That(result.Citations, Is.Not.Empty);
        Assert.That(result.Citations.First().PolicyVersion, Is.EqualTo("v1.2"));
        Assert.That(result.Answer, Does.Contain("maximum LTV ratio allowed is 80%"));
        Assert.That(result.NonApprovalDisclaimer, Is.Not.Empty);
    }

    [Test]
    public async Task HandleAsync_WithNoMatchingPolicy_ShouldReturnInsufficientEvidence()
    {
        var retriever = new FakePolicyRetriever { ReturnResults = false };
        var chatModel = new FakeChatModel();
        var handler = new AskProductQuestionQueryHandler(retriever, chatModel);

        var query = new AskProductQuestionQuery("What is the policy for commercial jet loans?");
        var result = await handler.HandleAsync(query);

        Assert.That(result.HasSufficientEvidence, Is.False);
        Assert.That(result.Citations, Is.Empty);
        Assert.That(result.Answer, Does.Contain("could not find sufficient matching policy evidence"));
    }
}
