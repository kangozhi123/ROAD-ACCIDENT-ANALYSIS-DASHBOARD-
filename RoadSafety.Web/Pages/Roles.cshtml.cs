using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Pages;

[Authorize(Policy = "ManageOfficers")]
public class RolesModel(OfficerService officers) : PageModel
{
    public List<OfficerRow> Officers { get; private set; } = [];

    private AccessScope Scope => AccessScope.From(User);

    /// <summary>Roles this caller may hand out, shown in the assign dialog.</summary>
    public IReadOnlyList<UserRole> AssignableRoles => Scope.AssignableRoles;

    public int CurrentUserId => Scope.UserId;

    public bool SeesEveryBranch => Scope.SeesEveryBranch;

    public async Task OnGetAsync() => Officers = await officers.ListAsync(Scope);

    /// <summary>POST /Roles?handler=Assign</summary>
    public async Task<IActionResult> OnPostAssignAsync([FromForm] int id, [FromForm] string role)
    {
        if (!Enum.TryParse<UserRole>(role, out var parsed))
        {
            return BadRequest(new { error = "That is not a role." });
        }

        var result = await officers.ChangeRoleAsync(id, parsed, Scope);

        return result switch
        {
            RoleChangeResult.Success => new JsonResult(new
            {
                message = $"Role updated to {Label(parsed)}.",
                role = parsed.ToString(),
                label = Label(parsed)
            }),
            RoleChangeResult.NotFound => NotFound(new { error = "That officer no longer exists." }),
            RoleChangeResult.CannotChangeOwnRole => new JsonResult(
                new { error = "You cannot change your own role. Ask another administrator." })
            { StatusCode = 400 },
            _ => new JsonResult(new { error = "That role is not yours to assign." }) { StatusCode = 403 }
        };
    }

    public static string Label(UserRole role) => role switch
    {
        UserRole.SystemAdministrator => "System admin",
        UserRole.StationAdministrator => "Station admin",
        _ => "Officer"
    };

    public static string Describes(UserRole role) => role switch
    {
        UserRole.SystemAdministrator =>
            "Manages every station in every organisation, and is the only role that can post an officer elsewhere.",
        UserRole.StationAdministrator =>
            "Adds, edits and removes the officers posted to their own station. Sees no other station.",
        _ =>
            "Reads their own station's records. Cannot add or change officers."
    };
}
