namespace RoadSafety.Web.ViewModels;

/// <summary>A station matched by the top-bar search.</summary>
public record StationResult(string Name, string ReferenceNumber, string CompanyName, int OfficerCount);
