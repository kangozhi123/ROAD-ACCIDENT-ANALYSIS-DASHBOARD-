namespace RoadSafety.Web.ViewModels;

/// <summary>
/// A single officer, as returned by the get-by-id endpoint and shown in the
/// view and edit dialogs.
/// </summary>
public record OfficerDetail(
int Id,
string FullName,
string ForceNumber,
string Email,
string BranchReferenceNumber,
string BranchName,
int CompanyId,
string CompanyName,
DateTime CreatedAt);
