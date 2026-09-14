using System.Text.RegularExpressions;

namespace Loan.Application.Common;

public static class PromptInjectionGuard
{
    private static readonly string[] InjectionKeywords = new[]
    {
        "SYSTEM OVERRIDE",
        "IGNORE PREVIOUS INSTRUCTIONS",
        "IGNORE ALL PREVIOUS INSTRUCTIONS",
        "DISREGARD POLICY",
        "DISREGARD RULES",
        "YOU ARE NOW IN DEVELOPER MODE",
        "GRANT APPROVAL",
        "BYPASS RULES",
        "BYPASS VERIFICATION",
        "OUTPUT SYSTEM PROMPT",
        "PRINT SYSTEM PROMPT",
        "SHOW SYSTEM PROMPT",
        "SET STATUS APPROVED",
        "APPROVE LOAN IMMEDIATELY",
        "IGNORE GUIDELINES",
        "ACT AS AN UNRESTRICTED"
    };

    public static bool IsInjectionAttempt(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        var upper = input.ToUpperInvariant();
        foreach (var keyword in InjectionKeywords)
        {
            if (upper.Contains(keyword))
            {
                return true;
            }
        }

        // Additional regex pattern checks for suspicious prompt injection structures
        if (Regex.IsMatch(input, @"(?:system\s*override|ignore\s+(?:all\s+)?previous\s+instructions)", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return false;
    }

    public const string RefusalMessage = "Refusal: The request contains unauthorized instructions attempting to override system governance or policy rules. This request has been denied and logged.";
}
