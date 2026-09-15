using Loan.Infrastructure.Persistence.DbContext;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Loan.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDefaultForRole();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["ReturnUrl"] = model.ReturnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in successfully.", model.Email);
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToDefaultForRole();
        }

        ModelState.AddModelError(string.Empty, "Invalid login credentials. Please verify your email and password.");
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickLogin(string persona, string? returnUrl = null)
    {
        var (email, password) = persona.ToLowerInvariant() switch
        {
            "applicant" => ("applicant@apex.local", "Applicant123!"),
            "officer" or "loanofficer" => ("officer@apex.local", "Officer123!"),
            "compliance" or "compliancereviewer" => ("compliance@apex.local", "Compliance123!"),
            "admin" or "administrator" => ("admin@apex.local", "Admin123!"),
            _ => ("applicant@apex.local", "Applicant123!")
        };

        await _signInManager.SignOutAsync();
        var result = await _signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            _logger.LogInformation("Persona QuickLogin succeeded for {Persona} ({Email})", persona, email);
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToDefaultForRole();
        }

        TempData["Error"] = $"Unable to authenticate demo persona '{persona}'.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    private IActionResult RedirectToDefaultForRole()
    {
        if (User.IsInRole("Administrator"))
        {
            return RedirectToAction("Index", "Admin");
        }
        if (User.IsInRole("LoanOfficer"))
        {
            return RedirectToAction("Index", "Officer");
        }
        if (User.IsInRole("ComplianceReviewer"))
        {
            return RedirectToAction("Index", "Compliance");
        }
        return RedirectToAction("Index", "Applicant");
    }
}

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
}
