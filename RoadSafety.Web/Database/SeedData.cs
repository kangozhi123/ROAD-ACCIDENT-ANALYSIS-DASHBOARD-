using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;

namespace RoadSafety.Web.Database;

public static class SeedData
{
    /// <summary>
    /// Applies pending migrations and inserts the demo officer if absent.
    /// Runs once per application start — not once per connection, which is
    /// what the old db.php did.
    ///
    /// Companies and branches are not seeded here: they are static data and
    /// already ship inside the migration. Only the officer needs code,
    /// because its password hash is randomly salted and so cannot be a
    /// literal in a migration.
    /// </summary>
    public static async Task InitialiseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var seeded = await db.Users.SingleOrDefaultAsync(u => u.ForceNumber == "ZP-00001");

        if (seeded is not null)
        {
            // A database created before roles existed has the demo account as a
            // plain officer, which would leave nobody able to manage anything.
            if (seeded.RoleId != Role.SystemAdministratorId)
            {
                seeded.RoleId = Role.SystemAdministratorId;
                await db.SaveChangesAsync();
            }

            await EnsureDemoDeviceAsync(db);
            return;
        }

        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();

        // The seeded account is a system administrator: without one, a fresh
        // database would have nobody able to create the first station admin.
        await auth.RegisterAsync(
        "Test Officer",
        "ZP-00001",
        "test.officer@police.gov.zm",
        "Password123!",
        "BR-001",
        Role.SystemAdministratorId);

        await EnsureDemoDeviceAsync(db);
    }

    /// <summary>
    /// A known development token so the ESP32 sketch works straight out of the
    /// repository. A deployed unit would be registered through the dashboard
    /// with a generated token shown once and never stored in plain text.
    /// </summary>
    public const string DemoDeviceToken = "ZP-DEMO-DEVICE-0001";

    private static async Task EnsureDemoDeviceAsync(AppDbContext db)
    {
        var hash = IncidentService.HashToken(DemoDeviceToken);

        if (await db.Devices.AnyAsync(d => d.TokenHash == hash))
        {
            return;
        }

        db.Devices.Add(new Device
        {
            Name = "Patrol Unit Alpha",
            VehicleRegistration = "ALH 1234 ZM",
            TokenHash = hash,
            BranchReferenceNumber = "BR-001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
