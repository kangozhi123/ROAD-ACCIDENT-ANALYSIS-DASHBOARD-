using System.Security.Claims;
using RoadSafety.Web.Models;

namespace RoadSafety.Web.Services;

/// <summary>Claim values for the permissions, named once.</summary>
public static class Permissions
{
    public const string SeesEveryBranch = "branches.all";
    public const string ManageOfficers = "officers.manage";
    public const string AssignRoles = "roles.assign";
    public const string ManageRoles = "roles.manage";

    /// <summary>The permission claims a role grants.</summary>
    public static IEnumerable<string> For(Role role)
    {
        if (role.SeesEveryBranch) yield return SeesEveryBranch;
        if (role.CanManageOfficers) yield return ManageOfficers;
        if (role.CanAssignRoles) yield return AssignRoles;
        if (role.CanManageRoles) yield return ManageRoles;
    }
}

/// <summary>
/// Who is asking, and how far their reach extends. Built from the signed-in
/// principal and passed into every officer query, so the rules live in one
/// place rather than being re-decided at each call site.
/// </summary>
/// <param name="UserId">The signed-in officer's id.</param>
/// <param name="RoleId">The role they hold.</param>
/// <param name="BranchReferenceNumber">The station they are posted to.</param>
public record AccessScope(
    int UserId,
    int RoleId,
    string BranchReferenceNumber,
    bool SeesEveryBranch,
    bool CanManageOfficers,
    bool CanAssignRoles,
    bool CanManageRoles)
{
    /// <summary>
    /// Whether a record belonging to <paramref name="branchReferenceNumber"/>
    /// is within reach.
    /// </summary>
    public bool Covers(string? branchReferenceNumber) =>
        SeesEveryBranch ||
        string.Equals(branchReferenceNumber, BranchReferenceNumber, StringComparison.Ordinal);

    /// <summary>
    /// The station an officer being created or edited must belong to, given
    /// what the caller asked for. Anyone who does not see every branch is
    /// pinned to their own station, so a posting outside it cannot be asked
    /// for even by editing the form.
    /// </summary>
    public string ResolveBranch(string? requested) =>
        SeesEveryBranch && !string.IsNullOrWhiteSpace(requested)
            ? requested.Trim()
            : BranchReferenceNumber;

    /// <summary>
    /// Whether this caller may hand out <paramref name="role"/>. A role is
    /// assignable only when it grants nothing the caller does not already
    /// hold — nobody gives away more than they have.
    /// </summary>
    public bool CanAssign(Role role) => CanAssignRoles && role.IsWithin(AsRole());

    /// <summary>The caller's own permissions, shaped as a role for comparison.</summary>
    public Role AsRole() => new()
    {
        Id = RoleId,
        SeesEveryBranch = SeesEveryBranch,
        CanManageOfficers = CanManageOfficers,
        CanAssignRoles = CanAssignRoles,
        CanManageRoles = CanManageRoles
    };

    /// <summary>
    /// Permissions come from claims stamped at sign-in, so editing a role does
    /// not change the sessions of people already signed in until they sign in
    /// again. Stated here so nobody expects it to take effect mid-session.
    /// </summary>
    public static AccessScope From(ClaimsPrincipal principal)
    {
        _ = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        _ = int.TryParse(principal.FindFirstValue("RoleId"), out var roleId);

        return new AccessScope(
            userId,
            roleId,
            principal.FindFirstValue("BranchReferenceNumber") ?? string.Empty,
            principal.HasClaim("perm", Permissions.SeesEveryBranch),
            principal.HasClaim("perm", Permissions.ManageOfficers),
            principal.HasClaim("perm", Permissions.AssignRoles),
            principal.HasClaim("perm", Permissions.ManageRoles));
    }
}
