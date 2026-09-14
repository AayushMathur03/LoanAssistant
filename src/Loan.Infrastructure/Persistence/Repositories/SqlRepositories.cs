using Loan.Application.Abstractions;
using Loan.Domain.Applications;
using Loan.Domain.Recommendations;
using Loan.Infrastructure.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Loan.Infrastructure.Persistence.Repositories;

public class SqlLoanApplicationRepository : ILoanApplicationRepository
{
    private readonly LoanDbContext _context;

    public SqlLoanApplicationRepository(LoanDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<LoanApplication?> GetByIdAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId, cancellationToken);

        if (entity == null) return null;

        var recEntity = await _context.Recommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId, cancellationToken);

        var recommendation = recEntity?.ToDomain();
        return entity.ToDomain(recommendation);
    }

    public async Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        var entity = LoanApplicationEntity.FromDomain(application);
        await _context.Applications.AddAsync(entity, cancellationToken);

        if (application.CurrentRecommendation != null)
        {
            var recEntity = RecommendationEntity.FromDomain(application.CurrentRecommendation, application.ApplicationId);
            await _context.Recommendations.AddAsync(recEntity, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        var entity = LoanApplicationEntity.FromDomain(application);
        var existing = await _context.Applications.FindAsync(new object[] { application.ApplicationId }, cancellationToken);

        if (existing != null)
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }
        else
        {
            await _context.Applications.AddAsync(entity, cancellationToken);
        }

        if (application.CurrentRecommendation != null)
        {
            var existingRec = await _context.Recommendations.FindAsync(new object[] { application.CurrentRecommendation.RecommendationId }, cancellationToken);
            var recEntity = RecommendationEntity.FromDomain(application.CurrentRecommendation, application.ApplicationId);

            if (existingRec != null)
            {
                _context.Entry(existingRec).CurrentValues.SetValues(recEntity);
            }
            else
            {
                await _context.Recommendations.AddAsync(recEntity, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<LoanApplication>> GetByApplicantIdAsync(string applicantId, CancellationToken cancellationToken = default)
    {
        var entities = await _context.Applications
            .AsNoTracking()
            .Where(a => a.ApplicantId == applicantId)
            .ToListAsync(cancellationToken);

        var appIds = entities.Select(e => e.ApplicationId).ToList();
        var recs = await _context.Recommendations
            .AsNoTracking()
            .Where(r => appIds.Contains(r.ApplicationId))
            .ToDictionaryAsync(r => r.ApplicationId, cancellationToken);

        return entities.Select(e => e.ToDomain(recs.TryGetValue(e.ApplicationId, out var recEntity) ? recEntity.ToDomain() : null));
    }

    public async Task<IEnumerable<LoanApplication>> GetPendingOfficerReviewAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Applications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.UnderOfficerReview || a.Status == ApplicationStatus.Submitted)
            .ToListAsync(cancellationToken);

        var appIds = entities.Select(e => e.ApplicationId).ToList();
        var recs = await _context.Recommendations
            .AsNoTracking()
            .Where(r => appIds.Contains(r.ApplicationId))
            .ToDictionaryAsync(r => r.ApplicationId, cancellationToken);

        return entities.Select(e => e.ToDomain(recs.TryGetValue(e.ApplicationId, out var recEntity) ? recEntity.ToDomain() : null));
    }
}

public class SqlRecommendationRepository : IRecommendationRepository
{
    private readonly LoanDbContext _context;

    public SqlRecommendationRepository(LoanDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Recommendation?> GetByIdAsync(string recommendationId, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Recommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RecommendationId == recommendationId, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recommendation);

        var appEntity = await _context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ApplicationId.Replace("APP", "REC") == recommendation.RecommendationId || a.ApplicationId == recommendation.RecommendationId.Replace("REC", "APP"), cancellationToken);

        string appId = appEntity?.ApplicationId ?? recommendation.RecommendationId.Replace("REC", "APP");

        var existing = await _context.Recommendations.FindAsync(new object[] { recommendation.RecommendationId }, cancellationToken);
        var entity = RecommendationEntity.FromDomain(recommendation, appId);

        if (existing == null)
        {
            await _context.Recommendations.AddAsync(entity, cancellationToken);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
