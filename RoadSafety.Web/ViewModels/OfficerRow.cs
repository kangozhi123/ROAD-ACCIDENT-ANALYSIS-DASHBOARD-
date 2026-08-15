namespace RoadSafety.Web.ViewModels;

/// <summary>A row in the officers table.</summary>
public record OfficerRow(
    string FullName,
    string ForceNumber,
    string Email,
    string BranchName,
    string CompanyName,
    DateTime CreatedAt);
