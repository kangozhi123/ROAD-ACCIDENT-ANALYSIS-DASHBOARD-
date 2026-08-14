using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Services;

namespace RoadSafety.Web.Pages;

public class RegisterModel : PageModel
{
    private readonly AuthService _auth;
    private readonly AppDbContext _db;

    public RegisterModel(AuthService auth, AppDbContext db)
    {
        _auth = auth;
        _db = db;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    /// <summary>Companies for the first dropdown.</summary>
    public List<CompanyOption> Companies { get; private set; } = [];

    /// <summary>
    /// All branches, serialised for the browser so choosing a company can
    /// filter the branch list without a round-trip or an extra endpoint.
    /// </summary>
    public string BranchesJson { get; private set; } = "[]";

    public record CompanyOption(int Id, string Name);

    public record BranchOption(int CompanyId, string ReferenceNumber, string Name);

    public class InputModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Force number is required")]
        [Display(Name = "Force Number")]
        public string ForceNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Company is required")]
        public int? CompanyId { get; set; }

        [Required(ErrorMessage = "Branch is required")]
        [Display(Name = "Branch")]
        public string BranchReferenceNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync() => await LoadLookupsAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _auth.RegisterAsync(
            Input.FullName, Input.ForceNumber, Input.Email, Input.Password, Input.BranchReferenceNumber);

        switch (result)
        {
            case RegistrationResult.DuplicateForceNumber:
                ErrorMessage = "An account already exists for that force number.";
                return Page();

            case RegistrationResult.DuplicateEmail:
                ErrorMessage = "An account already exists for that email address.";
                return Page();

            case RegistrationResult.UnknownBranch:
                ErrorMessage = "That branch was not recognised. Please choose one from the list.";
                return Page();

            default:
                return RedirectToPage("/Index");
        }
    }

    private async Task LoadLookupsAsync()
    {
        Companies = await _db.Companies
            .OrderBy(c => c.Name)
            .Select(c => new CompanyOption(c.Id, c.Name))
            .ToListAsync();

        var branches = await _db.Branches
            .OrderBy(b => b.Name)
            .Select(b => new BranchOption(b.CompanyId, b.ReferenceNumber, b.Name))
            .ToListAsync();

        BranchesJson = JsonSerializer.Serialize(branches);
    }
}
