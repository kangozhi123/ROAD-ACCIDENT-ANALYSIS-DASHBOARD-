namespace RoadSafety.Web.ViewModels;

/// <summary>
/// An entry in the branch dropdown. CompanyId is what the browser filters on
/// once a company is chosen.
/// </summary>
public record BranchOption(int CompanyId, string ReferenceNumber, string Name);
