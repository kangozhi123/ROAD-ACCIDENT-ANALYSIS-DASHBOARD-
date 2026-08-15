namespace RoadSafety.Web.ViewModels;

/// <summary>A row in the officers table.</summary>
public record OfficerRow(
    int Id,
    string FullName,
    string ForceNumber,
    string Email,
    string BranchName,
    string CompanyName,
    int RoleId,
    string RoleName,
    DateTime CreatedAt);
