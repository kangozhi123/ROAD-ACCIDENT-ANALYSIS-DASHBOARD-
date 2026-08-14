using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Services;
using RoadSafety.Web.ViewModels;

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
    public RegisterViewModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    /// <summary>Companies for the first dropdown.</summary>
    public List<CompanyOption> Companies { get; private set; } = [];

    /// <summary>
    /// All branches, serialised for the browser so choosing a company can
    /// filter the branch list without a round-trip or an extra endpoint.
    /// </summary>
    public string BranchesJson { get; private set; } = "[]";

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
