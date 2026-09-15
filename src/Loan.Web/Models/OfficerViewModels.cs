using Loan.Domain.Applications;

namespace Loan.Web.Models;

public class OfficerDashboardViewModel
{
    public int TotalApplications { get; set; }
    public int ReadyForReviewCount { get; set; }
    public int PendingInfoCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }

    public List<LoanApplication> ReviewQueue { get; set; } = new();
    public List<LoanApplication> RecentActivity { get; set; } = new();
}
