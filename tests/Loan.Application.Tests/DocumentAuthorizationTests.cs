using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Loan.Application.Abstractions;
using Loan.Application.Documents;
using Loan.Domain.Applications;
using Loan.Domain.Common;
using Loan.Domain.Products;

namespace Loan.Application.Tests;

[TestFixture]
public class DocumentAuthorizationTests
{
    private class InMemoryApplicationRepository : ILoanApplicationRepository
    {
        private readonly Dictionary<string, LoanApplication> _apps = new();

        public Task<LoanApplication?> GetByIdAsync(string applicationId, CancellationToken cancellationToken = default)
        {
            _apps.TryGetValue(applicationId, out var app);
            return Task.FromResult(app);
        }

        public Task AddAsync(LoanApplication application, CancellationToken cancellationToken = default)
        {
            _apps[application.ApplicationId] = application;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LoanApplication application, CancellationToken cancellationToken = default)
        {
            _apps[application.ApplicationId] = application;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<LoanApplication>> GetByApplicantIdAsync(string applicantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apps.Values.Where(a => a.ApplicantId == applicantId));
        }

        public Task<IEnumerable<LoanApplication>> GetPendingOfficerReviewAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_apps.Values.Where(a => a.Status == ApplicationStatus.UnderOfficerReview));
        }
    }

    private InMemoryApplicationRepository _repository = null!;
    private ConfirmOrOverrideExtractedFieldsCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryApplicationRepository();
        _handler = new ConfirmOrOverrideExtractedFieldsCommandHandler(_repository);
    }

    [Test]
    public void ComplianceReviewer_AttemptingOverride_ShouldThrowUnauthorizedAccessException()
    {
        var command = new ConfirmOrOverrideExtractedFieldsCommand(
            ApplicationId: "APP-2026-001",
            ActorId: "COMP-999",
            ActorRole: "Compliance",
            FieldConfirmations: new[] { new FieldConfirmationItem("DOC-1", "MonthlyGrossIncome", "12000.00", "Review") });

        var ex = Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await _handler.HandleAsync(command));

        Assert.That(ex!.Message, Does.Contain("Compliance Reviewers have read-only audit access"));
    }

    [Test]
    public void NonExistentApplication_ShouldThrowKeyNotFoundException()
    {
        var command = new ConfirmOrOverrideExtractedFieldsCommand(
            ApplicationId: "APP-UNKNOWN",
            ActorId: "OFF-101",
            ActorRole: "Officer",
            FieldConfirmations: new[] { new FieldConfirmationItem("DOC-1", "MonthlyGrossIncome", "12000.00", "Review") });

        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _handler.HandleAsync(command));
    }
}
