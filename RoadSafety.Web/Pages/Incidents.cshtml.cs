using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Pages;

[Authorize]
public class IncidentsModel(IncidentService incidents) : PageModel
{
    public List<IncidentRow> Incidents { get; private set; } = [];

    private AccessScope Scope => AccessScope.From(User);

    public bool SeesEveryBranch => Scope.SeesEveryBranch;

    public int ReportedCount => Incidents.Count(i => i.Status == IncidentStatus.Reported);

    /// <summary>Incidents that arrived with a fix, for the map.</summary>
    public List<IncidentRow> Located => Incidents
        .Where(i => i.Latitude.HasValue && i.Longitude.HasValue)
        .ToList();

    public async Task OnGetAsync() => Incidents = await incidents.ListAsync(Scope);

    public static string StatusLabel(IncidentStatus status) => status switch
    {
        IncidentStatus.Confirmed => "Confirmed",
        IncidentStatus.Dismissed => "Dismissed",
        _ => "Awaiting review"
    };
}
