namespace RoadSafety.Web.Models;

/// <summary>
/// A unit fitted to a vehicle: ESP32, motion sensor, GPS, camera.
///
/// A device cannot sign in — it has no officer behind it — so it carries a
/// token instead. Only the token's hash is stored, for the same reason
/// passwords are hashed: a copy of the database should not hand someone the
/// ability to post false incidents.
/// </summary>
public class Device
{
    public int Id { get; set; }

    /// <summary>Shown in the dashboard, e.g. "Patrol Unit Alpha".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Vehicle registration the unit is fitted to, if known.</summary>
    public string? VehicleRegistration { get; set; }

    /// <summary>SHA-256 of the token the device sends. Never the token itself.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// The station this unit reports to. Incidents inherit it, so the branch
    /// scoping that governs officers governs incidents too.
    /// </summary>
    public string BranchReferenceNumber { get; set; } = string.Empty;
    public Branch? Branch { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Last time anything arrived from this unit.</summary>
    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
}
