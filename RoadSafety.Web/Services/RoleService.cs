using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Services;

public enum RoleSaveResult
{
    Success,
    NotFound,
    Forbidden,
    DuplicateName,
    NameRequired,
    BuiltInPermissionsLocked,
    CannotEditOwnRole
}

public enum RoleDeleteResult
{
    Success,
    NotFound,
    Forbidden,
    BuiltInCannotBeDeleted,
    StillInUse,
    CannotDeleteOwnRole
}

/// <summary>
/// Creating, editing and removing roles.
///
/// Two things are protected throughout. The built-in roles cannot be deleted
/// and their permissions cannot be edited, so there is always a working ladder
/// to climb back up if a custom role is misconfigured. And nobody may create
/// or edit a role granting more than they hold themselves, which is what stops
/// an administrator writing themselves a more powerful role.
/// </summary>
public class RoleService(AppDbContext db)
{
    public async Task<List<RoleRow>> ListAsync() =>
        await db.Roles
            .OrderBy(r => r.Id)
            .Select(r => new RoleRow(
                r.Id,
                r.Name,
                r.Description,
                r.SeesEveryBranch,
                r.CanManageOfficers,
                r.CanAssignRoles,
                r.CanManageRoles,
                r.IsBuiltIn,
                r.Users.Count))
            .ToListAsync();

    public async Task<Role?> GetAsync(int id) =>
        await db.Roles.SingleOrDefaultAsync(r => r.Id == id);

    public async Task<(RoleSaveResult Result, int Id)> CreateAsync(
        string name, string description, Role permissions, AccessScope scope)
    {
        if (!scope.CanManageRoles)
        {
            return (RoleSaveResult.Forbidden, 0);
        }

        // Writing a role stronger than your own would be a way to grant
        // yourself permissions by assigning it to someone, then to yourself.
        if (!permissions.IsWithin(scope.AsRole()))
        {
            return (RoleSaveResult.Forbidden, 0);
        }

        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return (RoleSaveResult.NameRequired, 0);
        }

        if (await db.Roles.AnyAsync(r => r.Name == name))
        {
            return (RoleSaveResult.DuplicateName, 0);
        }

        var role = new Role
        {
            Name = name,
            Description = description.Trim(),
            SeesEveryBranch = permissions.SeesEveryBranch,
            CanManageOfficers = permissions.CanManageOfficers,
            CanAssignRoles = permissions.CanAssignRoles,
            CanManageRoles = permissions.CanManageRoles,
            IsBuiltIn = false
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync();

        return (RoleSaveResult.Success, role.Id);
    }

    public async Task<RoleSaveResult> UpdateAsync(
        int id, string name, string description, Role permissions, AccessScope scope)
    {
        if (!scope.CanManageRoles)
        {
            return RoleSaveResult.Forbidden;
        }

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id);
        if (role is null)
        {
            return RoleSaveResult.NotFound;
        }

        name = name.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return RoleSaveResult.NameRequired;
        }

        if (await db.Roles.AnyAsync(r => r.Name == name && r.Id != id))
        {
            return RoleSaveResult.DuplicateName;
        }

        var permissionsChanged =
            role.SeesEveryBranch != permissions.SeesEveryBranch
            || role.CanManageOfficers != permissions.CanManageOfficers
            || role.CanAssignRoles != permissions.CanAssignRoles
            || role.CanManageRoles != permissions.CanManageRoles;

        if (permissionsChanged)
        {
            // Editing your own role's permissions is how you would lock
            // yourself out, or quietly promote yourself.
            if (role.Id == scope.RoleId)
            {
                return RoleSaveResult.CannotEditOwnRole;
            }

            if (role.IsBuiltIn)
            {
                return RoleSaveResult.BuiltInPermissionsLocked;
            }

            if (!permissions.IsWithin(scope.AsRole()) || !role.IsWithin(scope.AsRole()))
            {
                return RoleSaveResult.Forbidden;
            }

            role.SeesEveryBranch = permissions.SeesEveryBranch;
            role.CanManageOfficers = permissions.CanManageOfficers;
            role.CanAssignRoles = permissions.CanAssignRoles;
            role.CanManageRoles = permissions.CanManageRoles;
        }

        // Renaming is always allowed, including for built-ins: the name is a
        // label, not a permission.
        role.Name = name;
        role.Description = description.Trim();

        await db.SaveChangesAsync();
        return RoleSaveResult.Success;
    }

    public async Task<RoleDeleteResult> DeleteAsync(int id, AccessScope scope)
    {
        if (!scope.CanManageRoles)
        {
            return RoleDeleteResult.Forbidden;
        }

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id);
        if (role is null)
        {
            return RoleDeleteResult.NotFound;
        }

        if (role.Id == scope.RoleId)
        {
            return RoleDeleteResult.CannotDeleteOwnRole;
        }

        if (role.IsBuiltIn)
        {
            return RoleDeleteResult.BuiltInCannotBeDeleted;
        }

        // Officers must be moved first. Deleting the role underneath them
        // would either orphan the rows or silently reassign real people.
        var holders = await db.Users.CountAsync(u => u.RoleId == id);
        if (holders > 0)
        {
            return RoleDeleteResult.StillInUse;
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync();

        return RoleDeleteResult.Success;
    }
}
