namespace RoadSafety.Web.ViewModels;

/// <summary>A row in the "recently registered" list on the dashboard.</summary>
public record RecentOfficer(string FullName, string ForceNumber, string BranchName, DateTime CreatedAt);
