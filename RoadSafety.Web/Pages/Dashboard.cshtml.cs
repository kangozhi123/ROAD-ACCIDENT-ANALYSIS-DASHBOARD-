using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RoadSafety.Web.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    public string OfficerName => User.Identity?.Name ?? "Officer";
    public string ForceNumber => User.FindFirst("ForceNumber")?.Value ?? "—";
    public string BranchName => User.FindFirst("BranchName")?.Value ?? "—";
    public string CompanyName => User.FindFirst("CompanyName")?.Value ?? "—";
}
