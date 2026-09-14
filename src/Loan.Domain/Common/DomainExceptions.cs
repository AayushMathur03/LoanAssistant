namespace Loan.Domain.Common;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class InvalidApplicationStateException : DomainException
{
    public InvalidApplicationStateException(string message) : base(message) { }
}

public class DomainRuleViolationException : DomainException
{
    public string RuleCode { get; }

    public DomainRuleViolationException(string ruleCode, string message) : base(message)
    {
        RuleCode = ruleCode;
    }
}
