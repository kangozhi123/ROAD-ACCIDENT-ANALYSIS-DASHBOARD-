using Microsoft.EntityFrameworkCore;
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

        if (await db.Users.AnyAsync(u => u.ForceNumber == "ZP-00001"))
        {
            return;
        }

        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        await auth.RegisterAsync(
            "Test Officer",
            "ZP-00001",
            "test.officer@police.gov.zm",
            "Password123!",
            "BR-001");
    }
}
