namespace Loan.Application.Abstractions;

public record ChatMessage(string Role, string Content);

public interface IChatModel
{
    Task<string> GenerateCompletionAsync(
        IEnumerable<ChatMessage> messages,
        double temperature = 0.2,
        CancellationToken cancellationToken = default);

    Task<T> GenerateStructuredAsync<T>(
        IEnumerable<ChatMessage> messages,
        double temperature = 0.1,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamCompletionAsync(
        IEnumerable<ChatMessage> messages,
        double temperature = 0.2,
        CancellationToken cancellationToken = default);
}
