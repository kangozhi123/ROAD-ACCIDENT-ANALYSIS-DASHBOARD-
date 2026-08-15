namespace RoadSafety.Web.Models;

/// <summary>
/// A suspected collision reported by a device.
///
/// "Suspected" is deliberate: a threshold on acceleration cannot tell a crash
/// from a dropped unit or a kerb taken hard, so an officer confirms or
/// dismisses it. The dashboard must not present these as confirmed crashes.
/// </summary>
public class Incident
{
    public int Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>Copied from the device at the time, so moving a unit later
    /// does not rewrite where past incidents happened.</summary>
    public string BranchReferenceNumber { get; set; } = string.Empty;
    public Branch? Branch { get; set; }

    /// <summary>When the device says it happened, from its own clock or GPS.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>When the server received it. Differs when the unit was offline.</summary>
    public DateTime ReceivedAt { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Peak acceleration in g at the moment of the trigger.</summary>
    public double ImpactG { get; set; }

    /// <summary>Speed from GPS just before the trigger, if there was a fix.</summary>
    public double? SpeedKph { get; set; }

    public IncidentStatus Status { get; set; } = IncidentStatus.Reported;

    /// <summary>Whatever the officer who reviewed it wrote down.</summary>
    public string? Notes { get; set; }
}

public enum IncidentStatus
{
    /// <summary>Arrived from a device, nobody has looked at it.</summary>
    Reported = 0,

    /// <summary>An officer confirmed a collision occurred.</summary>
    Confirmed = 1,

    /// <summary>An officer decided it was not a collision.</summary>
    Dismissed = 2
}
