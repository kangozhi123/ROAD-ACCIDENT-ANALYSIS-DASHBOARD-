using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Services;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Pages;

[Authorize]
public class UsersModel(AppDbContext db, AuthService auth) : PageModel
{
    public List<OfficerRow> Officers { get; private set; } = [];
    public List<CompanyOption> Companies { get; private set; } = [];
    public string BranchesJson { get; private set; } = "[]";

    [BindProperty]
    public RegisterViewModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Set when a submission fails, so the page can reopen the dialog with the
    /// officer's entries still in it rather than losing their typing.
    /// </summary>
    public bool ReopenDialog { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();

        if (!ModelState.IsValid)
        {
            ReopenDialog = true;
            return Page();
        }

        var result = await auth.RegisterAsync(
            Input.FullName, Input.ForceNumber, Input.Email, Input.Password, Input.BranchReferenceNumber);

        switch (result)
        {
            case RegistrationResult.DuplicateForceNumber:
                ErrorMessage = "An officer is already registered with that force number.";
                ReopenDialog = true;
                return Page();

            case RegistrationResult.DuplicateEmail:
                ErrorMessage = "An officer is already registered with that email address.";
                ReopenDialog = true;
                return Page();

            case RegistrationResult.UnknownBranch:
                ErrorMessage = "That station was not recognised. Choose one from the list.";
                ReopenDialog = true;
                return Page();

            default:
                TempData["Toast"] = $"{Input.FullName.Trim()} added.";
                return RedirectToPage();
        }
    }

    private async Task LoadAsync()
    {
        Officers = await db.Users
            .Include(u => u.Branch)!.ThenInclude(b => b!.Company)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new OfficerRow(
                u.FullName,
                u.ForceNumber,
                u.Email,
                u.Branch!.Name,
                u.Branch!.Company!.Name,
                u.CreatedAt))
            .ToListAsync();

        Companies = await db.Companies
            .OrderBy(c => c.Name)
            .Select(c => new CompanyOption(c.Id, c.Name))
            .ToListAsync();

        var branches = await db.Branches
            .OrderBy(b => b.Name)
            .Select(b => new BranchOption(b.CompanyId, b.ReferenceNumber, b.Name))
            .ToListAsync();

        BranchesJson = JsonSerializer.Serialize(branches);
    }
}
