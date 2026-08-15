using System.Security.Claims;
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
public class UsersModel(AppDbContext db, AuthService auth, OfficerService officers, NumberGenerator numbers) : PageModel
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

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>
    /// GET /Users?handler=NextForceNumber&amp;fullName=Grace%20Banda
    ///
    /// The prefix comes from the officer's initials, so the number can only be
    /// worked out once there is a name to read it from. The add form calls this
    /// as soon as the name field is filled in.
    /// </summary>
    public async Task<IActionResult> OnGetNextForceNumberAsync(string? fullName)
    {
        var forceNumber = await numbers.NextForceNumberForAsync(fullName);

        return new JsonResult(new { forceNumber });
    }

    // ── Endpoints ──────────────────────────────────────────────────────

    /// <summary>GET /Users?handler=Officer&amp;id=5</summary>
    public async Task<IActionResult> OnGetOfficerAsync(int id)
    {
        var officer = await officers.GetAsync(id);

        return officer is null
            ? NotFound(new { error = "That officer no longer exists." })
            : new JsonResult(officer);
    }

    /// <summary>POST /Users?handler=Update</summary>
    public async Task<IActionResult> OnPostUpdateAsync(
        [FromForm] int id,
        [FromForm] string fullName,
        [FromForm] string email,
        [FromForm] string branchReferenceNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Name and email are both required." });
        }

        var result = await officers.UpdateAsync(id, fullName, email, branchReferenceNumber);

        return result switch
        {
            // The changed officer comes back so the table row can be rewritten
            // in place rather than reloading the whole page.
            OfficerUpdateResult.Success => new JsonResult(new
            {
                message = $"{fullName.Trim()} updated.",
                officer = await officers.GetAsync(id)
            }),
            OfficerUpdateResult.NotFound => NotFound(new { error = "That officer no longer exists." }),
            // PageModel has no Conflict() helper, so the 409 is set explicitly.
            OfficerUpdateResult.DuplicateEmail => new JsonResult(
                new { error = "Another officer already uses that email address." }) { StatusCode = 409 },
            _ => BadRequest(new { error = "That station was not recognised." })
        };
    }

    /// <summary>POST /Users?handler=Delete</summary>
    public async Task<IActionResult> OnPostDeleteAsync([FromForm] int id)
    {
        var result = await officers.DeleteAsync(id, CurrentUserId);

        return result switch
        {
            OfficerDeleteResult.Success => new JsonResult(new { message = "Officer removed." }),
            OfficerDeleteResult.NotFound => NotFound(new { error = "That officer no longer exists." }),
            _ => BadRequest(new { error = "You cannot remove your own account." })
        };
    }

    // ── Add officer ────────────────────────────────────────────────────

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
                return Rejected("An officer is already registered with that force number.");

            case RegistrationResult.DuplicateEmail:
                return Rejected("An officer is already registered with that email address.");

            case RegistrationResult.UnknownBranch:
                return Rejected("That station was not recognised. Choose one from the list.");

            default:
                TempData["Toast"] = $"{Input.FullName.Trim()} added.";
                return RedirectToPage();
        }
    }

    /// <summary>
    /// Reports a rejected submission twice over: inside the dialog, where the
    /// user is looking, and as a toast, which is visible even if the dialog is
    /// scrolled past its alert.
    /// </summary>
    private PageResult Rejected(string message)
    {
        ErrorMessage = message;
        ReopenDialog = true;
        TempData["ToastError"] = message;
        return Page();
    }

    private async Task LoadAsync()
    {
        Officers = await officers.ListAsync();

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
