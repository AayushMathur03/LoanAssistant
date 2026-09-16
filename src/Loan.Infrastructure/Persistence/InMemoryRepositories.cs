using System.Collections.Concurrent;
using Loan.Application.Abstractions;
using Loan.Domain.Applications;
using Loan.Domain.Recommendations;

namespace Loan.Infrastructure.Persistence;

public class InMemoryLoanApplicationRepository : ILoanApplicationRepository
{
    private readonly ConcurrentDictionary<string, LoanApplication> _store = new();

    public Task<LoanApplication?> GetByIdAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(applicationId, out var application);
        return Task.FromResult(application);
    }

    public Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        _store[application.ApplicationId] = application;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        _store[application.ApplicationId] = application;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<LoanApplication>> GetByApplicantIdAsync(string applicantId, CancellationToken cancellationToken = default)
    {
        var results = _store.Values.Where(a => a.ApplicantId.Equals(applicantId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IEnumerable<LoanApplication>>(results.ToList());
    }

    public Task<IEnumerable<LoanApplication>> GetPendingOfficerReviewAsync(CancellationToken cancellationToken = default)
    {
        var results = _store.Values.Where(a => a.Status == ApplicationStatus.UnderOfficerReview || a.Status == ApplicationStatus.Submitted);
        return Task.FromResult<IEnumerable<LoanApplication>>(results.ToList());
    }

    public Task<IEnumerable<LoanApplication>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<LoanApplication>>(_store.Values.ToList());
    }
}

public class InMemoryRecommendationRepository : IRecommendationRepository
{
    private readonly ConcurrentDictionary<string, Recommendation> _store = new();

    public Task<Recommendation?> GetByIdAsync(string recommendationId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(recommendationId, out var recommendation);
        return Task.FromResult(recommendation);
    }

    public Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        _store[recommendation.RecommendationId] = recommendation;
        return Task.CompletedTask;
    }
}
