namespace Loan.Application.Common;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    public static string Current
    {
        get => _correlationId.Value ?? string.Empty;
        set => _correlationId.Value = value;
    }

    public static string EnsureCorrelationId()
    {
        if (string.IsNullOrWhiteSpace(_correlationId.Value))
        {
            _correlationId.Value = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        }
        return _correlationId.Value;
    }
}
