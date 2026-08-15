using RoadSafety.Web.Models;

namespace RoadSafety.Web.ViewModels;

/// <summary>A row in the officers table.</summary>
public record OfficerRow(
    int Id,
    string FullName,
    string ForceNumber,
    string Email,
    string BranchName,
    string CompanyName,
    UserRole Role,
    DateTime CreatedAt);
