namespace RoadSafety.Web.Models;

/// <summary>
/// A named set of permissions officers can be assigned to.
///
/// Roles are rows rather than an enum so an administrator can add one without
/// a rebuild. The permissions are fixed in code, because each one is a branch
/// the application has to actually honour — a role carrying a permission
/// nothing checks would grant nothing.
/// </summary>
public class Role
{
    /// <summary>Ids of the built-in roles, fixed so seeding and code agree.</summary>
    public const int OfficerId = 1;
    public const int StationAdministratorId = 2;
    public const int SystemAdministratorId = 3;

    public int Id { get; set; }

    /// <summary>Shown wherever the role appears. Unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One line explaining what holding it means.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Reads and manages every station, not only their own.</summary>
    public bool SeesEveryBranch { get; set; }

    /// <summary>Adds, edits and removes officers.</summary>
    public bool CanManageOfficers { get; set; }

    /// <summary>Moves officers between roles.</summary>
    public bool CanAssignRoles { get; set; }

    /// <summary>Creates, edits and deletes the roles themselves.</summary>
    public bool CanManageRoles { get; set; }

    /// <summary>
    /// The three roles the system ships with. They cannot be deleted and their
    /// permissions cannot be edited, so there is always a working ladder to
    /// climb back up if a custom role is misconfigured.
    /// </summary>
    public bool IsBuiltIn { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();

    /// <summary>
    /// True when this role grants nothing beyond <paramref name="other"/>.
    /// Used to stop anyone handing out more than they hold themselves.
    /// </summary>
    public bool IsWithin(Role other) =>
        (!SeesEveryBranch || other.SeesEveryBranch)
        && (!CanManageOfficers || other.CanManageOfficers)
        && (!CanAssignRoles || other.CanAssignRoles)
        && (!CanManageRoles || other.CanManageRoles);
}
