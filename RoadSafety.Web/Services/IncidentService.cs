using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Services;

public enum IncidentIntakeResult
{
    Accepted,
    UnknownDevice,
    DeviceDisabled,
    Invalid
}

/// <summary>
/// Taking in what devices report, and reading it back out for the dashboard.
///
/// Everything the device sends is treated as a claim rather than a fact: the
/// station comes from the registered device, not from the payload, and the
/// server records its own arrival time alongside the device's.
/// </summary>
public class IncidentService(AppDbContext db)
{
    /// <summary>
    /// SHA-256 rather than a password hash: device tokens are long random
    /// strings, so there is nothing to brute force, and the lookup has to be a
    /// single indexed query rather than a salted comparison per row.
    /// </summary>
    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()))).ToLowerInvariant();

    public async Task<Device?> FindDeviceAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = HashToken(token);

        return await db.Devices.SingleOrDefaultAsync(d => d.TokenHash == hash, ct);
    }

    public async Task<(IncidentIntakeResult Result, int Id)> RecordAsync(
        string? token, IncidentReading reading, CancellationToken ct = default)
    {
        var device = await FindDeviceAsync(token, ct);

        if (device is null)
        {
            return (IncidentIntakeResult.UnknownDevice, 0);
        }

        if (!device.IsActive)
        {
            return (IncidentIntakeResult.DeviceDisabled, 0);
        }

        if (reading.ImpactG <= 0)
        {
            return (IncidentIntakeResult.Invalid, 0);
        }

        // A GPS fix takes time to acquire, so coordinates are optional — an
        // incident with no fix is still worth recording. Nonsense coordinates
        // are dropped rather than plotted in the sea off West Africa.
        var hasFix = reading.Latitude is >= -90 and <= 90
                  && reading.Longitude is >= -180 and <= 180
                  && !(reading.Latitude == 0 && reading.Longitude == 0);

        var now = DateTime.UtcNow;

        var incident = new Incident
        {
            DeviceId = device.Id,
            BranchReferenceNumber = device.BranchReferenceNumber,
            // A device with no clock and no fix reports nothing useful, so its
            // arrival time stands in.
            OccurredAt = reading.OccurredAt ?? now,
            ReceivedAt = now,
            Latitude = hasFix ? reading.Latitude : null,
            Longitude = hasFix ? reading.Longitude : null,
            ImpactG = reading.ImpactG,
            SpeedKph = reading.SpeedKph is >= 0 ? reading.SpeedKph : null,
            Status = IncidentStatus.Reported
        };

        device.LastSeenAt = now;

        db.Incidents.Add(incident);
        await db.SaveChangesAsync(ct);

        return (IncidentIntakeResult.Accepted, incident.Id);
    }

    public async Task<List<IncidentRow>> ListAsync(AccessScope scope, CancellationToken ct = default)
    {
        var query = db.Incidents
            .Include(i => i.Device)
            .Include(i => i.Branch)
            .AsQueryable();

        if (!scope.SeesEveryBranch)
        {
            query = query.Where(i => i.BranchReferenceNumber == scope.BranchReferenceNumber);
        }

        return await query
            .OrderByDescending(i => i.OccurredAt)
            .Select(i => new IncidentRow(
                i.Id,
                i.Device!.Name,
                i.Device!.VehicleRegistration,
                i.Branch!.Name,
                i.OccurredAt,
                i.Latitude,
                i.Longitude,
                i.ImpactG,
                i.SpeedKph,
                i.Status))
            .ToListAsync(ct);
    }
}

/// <summary>What a device sends when its motion sensor trips.</summary>
public record IncidentReading(
    DateTime? OccurredAt,
    double? Latitude,
    double? Longitude,
    double ImpactG,
    double? SpeedKph);
