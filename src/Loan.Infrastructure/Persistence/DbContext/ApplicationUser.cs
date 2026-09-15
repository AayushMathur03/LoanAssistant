using Microsoft.AspNetCore.Identity;

namespace Loan.Infrastructure.Persistence.DbContext;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? LinkedApplicationId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
