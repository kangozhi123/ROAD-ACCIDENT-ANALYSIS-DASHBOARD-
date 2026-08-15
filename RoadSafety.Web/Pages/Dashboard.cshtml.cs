using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Pages;

[Authorize]
public class DashboardModel(AppDbContext db) : PageModel
{
    public string OfficerName => User.Identity?.Name ?? "Officer";
    public string ForceNumber => User.FindFirst("ForceNumber")?.Value ?? "—";
    public string BranchName => User.FindFirst("BranchName")?.Value ?? "—";
    public string CompanyName => User.FindFirst("CompanyName")?.Value ?? "—";

    /// <summary>
    /// Always zero until the collision dataset is imported — there is no
    /// Accidents table yet. Shown rather than hidden so the dashboard states
    /// plainly that it holds no crash data, instead of implying otherwise.
    /// </summary>
    public int CollisionCount => 0;

    public int StationCount { get; private set; }
    public int OrganisationCount { get; private set; }
    public int OfficerCount { get; private set; }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    public bool IsSearching => !string.IsNullOrWhiteSpace(Query);

    /// <summary>Search hits when searching, otherwise the five newest officers.</summary>
    public List<RecentOfficer> Officers { get; private set; } = [];

    /// <summary>Stations matching the search. Empty when not searching.</summary>
    public List<StationResult> Stations { get; private set; } = [];

    public async Task OnGetAsync()
    {
        StationCount = await db.Branches.CountAsync();
        OrganisationCount = await db.Companies.CountAsync();
        OfficerCount = await db.Users.CountAsync();

        var officers = db.Users.Include(u => u.Branch).AsQueryable();

        if (IsSearching)
        {
            // Lower-cased on both sides rather than using Contains directly:
            // EF translates Contains to SQLite's instr(), which is case
            // sensitive, so "grace" would never match "Grace Banda". This
            // form also survives a move to PostgreSQL, where LIKE is case
            // sensitive too.
            var term = Query!.Trim().ToLower();

            officers = officers.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.ForceNumber.ToLower().Contains(term) ||
                u.Branch!.Name.ToLower().Contains(term));

            Stations = await db.Branches
                .Include(b => b.Company)
                .Where(b => b.Name.ToLower().Contains(term)
                         || b.ReferenceNumber.ToLower().Contains(term)
                         || b.Code.ToLower().Contains(term)
                         || b.Company!.Name.ToLower().Contains(term))
                .OrderBy(b => b.Name)
                .Select(b => new StationResult(
                    b.Name,
                    b.ReferenceNumber,
                    b.Company!.Name,
                    b.Users.Count))
                .ToListAsync();
        }

        Officers = await officers
            .OrderByDescending(u => u.CreatedAt)
            .Take(IsSearching ? 25 : 5)
            .Select(u => new RecentOfficer(
                u.FullName,
                u.ForceNumber,
                u.Branch!.Name,
                u.CreatedAt))
            .ToListAsync();
    }
}
