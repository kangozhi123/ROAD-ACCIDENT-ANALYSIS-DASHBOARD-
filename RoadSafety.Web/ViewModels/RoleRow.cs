namespace RoadSafety.Web.ViewModels;

/// <summary>A role as shown on the roles page, with how many officers hold it.</summary>
public record RoleRow(
    int Id,
    string Name,
    string Description,
    bool SeesEveryBranch,
    bool CanManageOfficers,
    bool CanAssignRoles,
    bool CanManageRoles,
    bool IsBuiltIn,
    int HolderCount);
