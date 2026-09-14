using System.Text.RegularExpressions;

namespace Loan.Application.Common;

public static class PiiMasker
{
    private static readonly Regex SsnRegex = new(@"\b\d{3}-\d{2}-(\d{4})\b", RegexOptions.Compiled);
    private static readonly Regex AccountNumberRegex = new(@"\b\d{4,13}(\d{4})\b", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"\b([A-Za-z0-9._%+-]{1,2})[A-Za-z0-9._%+-]*@([A-Za-z0-9.-]+\.[A-Za-z]{2,})\b", RegexOptions.Compiled);

    public static string MaskPii(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Mask SSN: 123-45-6789 -> ***-**-6789
        var result = SsnRegex.Replace(input, "***-**-$1");

        // Mask Account Numbers: 9876543210 -> ******3210
        result = AccountNumberRegex.Replace(result, m => new string('*', m.Length - 4) + m.Groups[1].Value);

        // Mask Email: john.doe@example.com -> jo***@example.com
        result = EmailRegex.Replace(result, "$1***@$2");

        return result;
    }

    public static string MaskSsn(string? ssn)
    {
        if (string.IsNullOrWhiteSpace(ssn)) return "***-**-XXXX";
        var match = SsnRegex.Match(ssn);
        if (match.Success && match.Groups.Count > 1)
        {
            return $"***-**-{match.Groups[1].Value}";
        }
        if (ssn.Length >= 4)
        {
            return $"***-**-{ssn[^4..]}";
        }
        return "***-**-XXXX";
    }
}
