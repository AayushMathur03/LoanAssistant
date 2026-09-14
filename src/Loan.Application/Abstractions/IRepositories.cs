using Loan.Domain.Applications;
using Loan.Domain.Recommendations;

namespace Loan.Application.Abstractions;

public interface ILoanApplicationRepository
{
    Task<LoanApplication?> GetByIdAsync(string applicationId, CancellationToken cancellationToken = default);
    Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default);
    Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanApplication>> GetByApplicantIdAsync(string applicantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanApplication>> GetPendingOfficerReviewAsync(CancellationToken cancellationToken = default);
}

public interface IRecommendationRepository
{
    Task<Recommendation?> GetByIdAsync(string recommendationId, CancellationToken cancellationToken = default);
    Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default);
}
