using Loan.Application.Abstractions;
using Loan.Application.DTOs;
using Loan.Domain.Eligibility;

namespace Loan.Application.Verification;

public record EvaluateEligibilityCommand(string ApplicationId);

public class EvaluateEligibilityCommandHandler
{
    private readonly ILoanApplicationRepository _repository;
    private readonly IIdentityReader _identityReader;
    private readonly IIncomeReader _incomeReader;
    private readonly ICreditReader _creditReader;

    public EvaluateEligibilityCommandHandler(
        ILoanApplicationRepository repository,
        IIdentityReader identityReader,
        IIncomeReader incomeReader,
        ICreditReader creditReader)
    {
        _repository = repository;
        _identityReader = identityReader;
        _incomeReader = incomeReader;
        _creditReader = creditReader;
    }

    public async Task<EligibilityIndicatorsDto> HandleAsync(EvaluateEligibilityCommand command, CancellationToken cancellationToken = default)
    {
        var application = await _repository.GetByIdAsync(command.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Loan application with ID '{command.ApplicationId}' was not found.");

        var facts = application.Facts;
        var now = DateTime.UtcNow;

        // Execute synthetic tool calls in parallel or sequence (FR-04)
        try
        {
            var identityResult = await _identityReader.VerifyIdentityAsync(facts.SyntheticId, cancellationToken);
            facts.SetIdentityVerified(identityResult.IsVerified, identityResult.VerifiedAtUtc);
        }
        catch (Exception)
        {
            facts.SetIdentityVerified(false, now);
        }

        try
        {
            var incomeResult = await _incomeReader.VerifyIncomeAsync(facts.SyntheticId, cancellationToken);
            facts.SetIncomeVerified(incomeResult.IsVerified, incomeResult.VerifiedMonthlyIncome, incomeResult.VerifiedAtUtc);
        }
        catch (Exception)
        {
            facts.SetIncomeVerified(false, facts.MonthlyGrossIncome, now);
        }

        try
        {
            var creditResult = await _creditReader.GetCreditScoreAsync(facts.SyntheticId, cancellationToken);
            facts.SetCreditVerified(creditResult.IsVerified, creditResult.CreditScore, creditResult.CheckedAtUtc);
        }
        catch (Exception)
        {
            facts.SetCreditVerified(false, facts.CreditScore, now);
        }

        // Domain calculation (BR-03 & FR-05)
        application.EvaluateEligibility(now);
        await _repository.UpdateAsync(application, cancellationToken);

        var ind = application.Indicators!;
        return new EligibilityIndicatorsDto(
            DebtToIncomeRatio: ind.DebtToIncomeRatio,
            LoanToValueRatio: ind.LoanToValueRatio,
            IsDtiEligible: ind.IsDtiEligible,
            IsLtvEligible: ind.IsLtvEligible,
            IsCreditScoreEligible: ind.IsCreditScoreEligible,
            IsIncomeThresholdEligible: ind.IsIncomeThresholdEligible,
            IsLoanAmountEligible: ind.IsLoanAmountEligible,
            Status: ind.Status,
            UnmetConditions: ind.UnmetConditions);
    }
}
