using Loan.Application.DTOs;

namespace Loan.Application.Abstractions;

public interface IPolicyRetriever
{
    Task<IEnumerable<PolicySearchResultDto>> SearchPolicyAsync(
        string query,
        string? targetProductId = null,
        string? effectiveVersion = null,
        int topK = 5,
        CancellationToken cancellationToken = default);
}
