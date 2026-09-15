using Loan.Domain.Applications;
using Loan.Domain.Recommendations;

namespace Loan.Web.Models;

public class ComplianceDashboardViewModel
{
    public int TotalAuditedApplications { get; set; }
    public int FlaggedExceptionsCount { get; set; }
    public int TridCompliantCount { get; set; }
    public int SecurityEventsCount { get; set; }

    public List<LoanApplication> ComplianceQueue { get; set; } = new();
    public List<AuditLogItem> RecentAuditEvents { get; set; } = new();
    public List<SecurityLogItem> SecurityEvents { get; set; } = new();
}

public class AuditLogItem
{
    public string ApplicationId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class SecurityLogItem
{
    public string EventType { get; set; } = string.Empty;
    public string SourceIp { get; set; } = string.Empty;
    public string QuerySnippet { get; set; } = string.Empty;
    public string InterceptReason { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}
