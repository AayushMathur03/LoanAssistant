using System.Collections;
using System.Collections.Generic;
using Loan.Domain.Applications;
using Loan.Domain.Documents;

namespace Loan.Web.Models;

public class ApplicantDashboardViewModel : IReadOnlyList<LoanApplication>
{
    public string CurrentUserName { get; set; } = string.Empty;
    public string CurrentUserEmail { get; set; } = string.Empty;
    public string ApplicantId { get; set; } = string.Empty;
    
    public LoanApplication? ActiveApplication { get; set; }
    public List<LoanApplication> AllApplications { get; set; } = new();
    public string ActiveTab { get; set; } = "catalogue"; // "catalogue" or "applications"

    public IEnumerator<LoanApplication> GetEnumerator() => AllApplications.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => AllApplications.GetEnumerator();
    public int Count => AllApplications.Count;
    public LoanApplication this[int index] => AllApplications[index];

    public int JourneyStage => ActiveApplication switch
    {
        null => 0,
        { Status: ApplicationStatus.Approved or ApplicationStatus.Rejected } => 5,
        { CurrentRecommendation: not null } => 4,
        { Indicators: not null } => 3,
        { Documents.Count: > 0 } => 2,
        _ => 1
    };

    public string JourneyStageTitle => JourneyStage switch
    {
        0 => "No Application Submitted",
        1 => "Application Submitted",
        2 => "Documents Uploaded & Verified",
        3 => "Eligibility Evaluated",
        4 => "AI Recommendation Drafted",
        5 => ActiveApplication?.Status == ApplicationStatus.Approved ? "Loan Approved" : "Loan Decision Finalized",
        _ => "In Progress"
    };

    public List<string> MissingEvidenceItems
    {
        get
        {
            var items = new List<string>();
            if (ActiveApplication == null) return items;

            bool hasId = ActiveApplication.Documents.Any(d => d.DocumentType == DocumentType.DriverLicenseOrPassport);
            bool hasIncome = ActiveApplication.Documents.Any(d => d.DocumentType is DocumentType.Paystub or DocumentType.W2 or DocumentType.TaxReturn);
            bool hasBank = ActiveApplication.Documents.Any(d => d.DocumentType == DocumentType.BankStatement);

            if (!hasId) items.Add("Government Photo ID (Driver License or Passport)");
            if (!hasIncome) items.Add("Income Verification (Recent Paystub or W-2)");
            if (!hasBank) items.Add("Asset Verification (Bank Statement - 60 to 90 Days)");

            if (ActiveApplication.Indicators != null)
            {
                foreach (var cond in ActiveApplication.Indicators.UnmetConditions)
                {
                    if (!items.Contains(cond)) items.Add(cond);
                }
            }

            return items;
        }
    }
}

public class CreateApplicationInputModel
{
    public string ProductType { get; set; } = "Mortgage"; // "Mortgage", "PersonalLoan", or "AutoLoan"
    public decimal RequestedAmount { get; set; } = 350000m;
    public int TermMonths { get; set; } = 360;
    public decimal EstimatedPropertyValue { get; set; } = 500000m;
    public decimal MonthlyGrossIncome { get; set; } = 12000m;
    public decimal MonthlyDebts { get; set; } = 3000m;
    public int CreditScore { get; set; } = 750;
    public string EmploymentStatus { get; set; } = "Full-Time Employed";
    public string LoanPurpose { get; set; } = "Primary Residence Purchase";
}
