using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Pages;

[Authorize(Policy = "AssignRoles")]
public class RolesModel(OfficerService officers, RoleService roles) : PageModel
{
    public List<OfficerRow> Officers { get; private set; } = [];
    public List<RoleRow> Roles { get; private set; } = [];

    private AccessScope Scope => AccessScope.From(User);

    public int CurrentUserId => Scope.UserId;
    public int CurrentRoleId => Scope.RoleId;
    public bool SeesEveryBranch => Scope.SeesEveryBranch;
    public bool CanManageRoles => Scope.CanManageRoles;

    /// <summary>Roles this caller may hand out — those granting nothing extra.</summary>
    public List<RoleRow> AssignableRoles { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    // ── Assigning a role to an officer ─────────────────────────────────

    public async Task<IActionResult> OnPostAssignAsync([FromForm] int id, [FromForm] int roleId)
    {
        var result = await officers.ChangeRoleAsync(id, roleId, Scope);
        var role = await roles.GetAsync(roleId);

        return result switch
        {
            RoleChangeResult.Success => new JsonResult(new
            {
                message = $"Role updated to {role!.Name}.",
                roleId,
                label = role.Name
            }),
            RoleChangeResult.NotFound => NotFound(new { error = "That officer no longer exists." }),
            RoleChangeResult.CannotChangeOwnRole => Bad("You cannot change your own role. Ask another administrator."),
            _ => Forbidden("That role is not yours to assign.")
        };
    }

    // ── The roles themselves ───────────────────────────────────────────

    public async Task<IActionResult> OnGetRoleAsync(int id)
    {
        var role = await roles.GetAsync(id);

        return role is null
            ? NotFound(new { error = "That role no longer exists." })
            : new JsonResult(new
            {
                role.Id,
                role.Name,
                role.Description,
                role.SeesEveryBranch,
                role.CanManageOfficers,
                role.CanAssignRoles,
                role.CanManageRoles,
                role.IsBuiltIn
            });
    }

    public async Task<IActionResult> OnPostCreateRoleAsync(
        [FromForm] string name, [FromForm] string description,
        [FromForm] bool seesEveryBranch, [FromForm] bool canManageOfficers,
        [FromForm] bool canAssignRoles, [FromForm] bool canManageRoles)
    {
        var (result, id) = await roles.CreateAsync(
            name, description ?? string.Empty,
            Wanted(seesEveryBranch, canManageOfficers, canAssignRoles, canManageRoles),
            Scope);

        return result == RoleSaveResult.Success
            ? new JsonResult(new { message = $"{name.Trim()} created.", id })
            : Explain(result);
    }

    public async Task<IActionResult> OnPostUpdateRoleAsync(
        [FromForm] int id, [FromForm] string name, [FromForm] string description,
        [FromForm] bool seesEveryBranch, [FromForm] bool canManageOfficers,
        [FromForm] bool canAssignRoles, [FromForm] bool canManageRoles)
    {
        var result = await roles.UpdateAsync(
            id, name, description ?? string.Empty,
            Wanted(seesEveryBranch, canManageOfficers, canAssignRoles, canManageRoles),
            Scope);

        return result == RoleSaveResult.Success
            ? new JsonResult(new { message = $"{name.Trim()} updated." })
            : Explain(result);
    }

    public async Task<IActionResult> OnPostDeleteRoleAsync([FromForm] int id)
    {
        var result = await roles.DeleteAsync(id, Scope);

        return result switch
        {
            RoleDeleteResult.Success => new JsonResult(new { message = "Role deleted." }),
            RoleDeleteResult.NotFound => NotFound(new { error = "That role no longer exists." }),
            RoleDeleteResult.BuiltInCannotBeDeleted =>
                Bad("The built-in roles cannot be deleted. Rename one instead."),
            RoleDeleteResult.StillInUse =>
                Bad("Officers still hold this role. Move them to another role first."),
            RoleDeleteResult.CannotDeleteOwnRole =>
                Bad("You hold this role, so you cannot delete it."),
            _ => Forbidden("You cannot manage roles.")
        };
    }

    private static Role Wanted(bool branches, bool officers, bool assign, bool manage) => new()
    {
        SeesEveryBranch = branches,
        CanManageOfficers = officers,
        CanAssignRoles = assign,
        CanManageRoles = manage
    };

    private IActionResult Explain(RoleSaveResult result) => result switch
    {
        RoleSaveResult.NotFound => NotFound(new { error = "That role no longer exists." }),
        RoleSaveResult.NameRequired => Bad("Give the role a name."),
        RoleSaveResult.DuplicateName => Bad("A role with that name already exists."),
        RoleSaveResult.BuiltInPermissionsLocked =>
            Bad("A built-in role's permissions are fixed. Create a new role instead."),
        RoleSaveResult.CannotEditOwnRole =>
            Bad("You hold this role, so you cannot change what it grants."),
        _ => Forbidden("A role cannot grant more than you hold yourself.")
    };

    private static JsonResult Bad(string error) => new(new { error }) { StatusCode = 400 };

    private static JsonResult Forbidden(string error) => new(new { error }) { StatusCode = 403 };

    private async Task LoadAsync()
    {
        Officers = await officers.ListAsync(Scope);
        Roles = await roles.ListAsync();

        var mine = Scope.AsRole();

        AssignableRoles = Roles
            .Where(r => new Role
            {
                SeesEveryBranch = r.SeesEveryBranch,
                CanManageOfficers = r.CanManageOfficers,
                CanAssignRoles = r.CanAssignRoles,
                CanManageRoles = r.CanManageRoles
            }.IsWithin(mine))
            .ToList();
    }
}
