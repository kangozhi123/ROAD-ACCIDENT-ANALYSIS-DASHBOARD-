using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RoadSafety.Web.Services;

namespace RoadSafety.Web.Pages;

public class IndexModel : PageModel
{
    private readonly AuthService _auth;

    public IndexModel(AuthService auth) => _auth = auth;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Force number is required")]
        [Display(Name = "Force Number")]
        public string ForceNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Dashboard");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _auth.ValidateCredentialsAsync(Input.ForceNumber, Input.Password);
        if (user is null)
        {
            // Deliberately generic — see AuthService.ValidateCredentialsAsync.
            ErrorMessage = "Invalid credentials";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("ForceNumber", user.ForceNumber),
            new("BranchReferenceNumber", user.BranchReferenceNumber),
            new("BranchName", user.Branch?.Name ?? string.Empty),
            new("CompanyName", user.Branch?.Company?.Name ?? string.Empty)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToPage("/Dashboard");
    }
}
