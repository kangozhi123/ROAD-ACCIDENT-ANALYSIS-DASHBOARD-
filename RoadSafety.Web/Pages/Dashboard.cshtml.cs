using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly AppDbContext _db;

    public DashboardModel(AppDbContext db) => _db = db;

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

    public List<RecentOfficer> RecentOfficers { get; private set; } = [];

    public async Task OnGetAsync()
    {
        StationCount = await _db.Branches.CountAsync();
        OrganisationCount = await _db.Companies.CountAsync();
        OfficerCount = await _db.Users.CountAsync();

        RecentOfficers = await _db.Users
            .Include(u => u.Branch)
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new RecentOfficer(
                u.FullName,
                u.ForceNumber,
                u.Branch!.Name,
                u.CreatedAt))
            .ToListAsync();
    }
}
