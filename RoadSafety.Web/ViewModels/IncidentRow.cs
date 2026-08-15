using RoadSafety.Web.Models;

namespace RoadSafety.Web.ViewModels;

/// <summary>A suspected collision as shown in the dashboard.</summary>
public record IncidentRow(
int Id,
string DeviceName,
string? VehicleRegistration,
string BranchName,
DateTime OccurredAt,
double? Latitude,
double? Longitude,
double ImpactG,
double? SpeedKph,
IncidentStatus Status);
