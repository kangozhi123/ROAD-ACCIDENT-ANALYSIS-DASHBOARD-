using System.Security.Claims;
using RoadSafety.Web.Models;

namespace RoadSafety.Web.Services;

/// <summary>
/// Who is asking, and how far their reach extends. Built from the signed-in
/// principal and passed into every officer query, so the rules live in one
/// place rather than being re-decided at each call site.
/// </summary>
/// <param name="UserId">The signed-in officer's id.</param>
/// <param name="Role">Their role.</param>
/// <param name="BranchReferenceNumber">The station they are posted to.</param>
public record AccessScope(int UserId, UserRole Role, string BranchReferenceNumber)
{
    /// <summary>True when the caller reaches beyond their own station.</summary>
    public bool SeesEveryBranch => Role == UserRole.SystemAdministrator;

    /// <summary>True when the caller may add, change or remove officers.</summary>
    public bool CanManageOfficers =>
        Role is UserRole.StationAdministrator or UserRole.SystemAdministrator;

    /// <summary>
    /// Whether a record belonging to <paramref name="branchReferenceNumber"/>
    /// is within reach.
    /// </summary>
    public bool Covers(string? branchReferenceNumber) =>
    SeesEveryBranch ||
    string.Equals(branchReferenceNumber, BranchReferenceNumber, StringComparison.Ordinal);

    public string ResolveBranch(string? requested) =>
        SeesEveryBranch && !string.IsNullOrWhiteSpace(requested)
            ? requested.Trim()
            : BranchReferenceNumber;

    public IReadOnlyList<UserRole> AssignableRoles => SeesEveryBranch
        ? [UserRole.Officer, UserRole.StationAdministrator, UserRole.SystemAdministrator]
        : [UserRole.Officer, UserRole.StationAdministrator];

    public bool CanAssign(UserRole role) => AssignableRoles.Contains(role);

    public static AccessScope From(ClaimsPrincipal principal)
    {
        _ = int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);

        _ = Enum.TryParse<UserRole>(principal.FindFirstValue(ClaimTypes.Role), out var role);

        return new AccessScope(
            userId,
            role,
            principal.FindFirstValue("BranchReferenceNumber") ?? string.Empty);
    }
}
