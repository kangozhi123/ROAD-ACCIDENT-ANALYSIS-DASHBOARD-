using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;

namespace RoadSafety.Tests;

public class AuthServiceTests
{
    /// <summary>
    /// Creates a throwaway database in memory. The connection must stay open —
    /// SQLite discards an in-memory database the moment its last connection closes.
    /// </summary>
    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static AuthService CreateService(AppDbContext db) =>
        new(db, new PasswordHasher<User>());

    // ── Schema ────────────────────────────────────────────────

    [Fact]
    public async Task Companies_and_branches_are_seeded_by_the_model()
    {
        using var db = CreateContext();

        Assert.Equal(2, await db.Companies.CountAsync());
        Assert.Equal(4, await db.Branches.CountAsync());

        var branch = await db.Branches
            .Include(b => b.Company)
            .SingleAsync(b => b.ReferenceNumber == "BR-001");

        Assert.Equal("Kitwe Central", branch.Name);
        Assert.Equal("Zambia Police Service", branch.Company!.Name);
    }

    [Fact]
    public async Task Users_table_persists_and_returns_a_user_with_its_branch()
    {
        using var db = CreateContext();

        db.Users.Add(new User
        {
            FullName = "Test Officer",
            ForceNumber = "ZP-00001",
            Email = "test.officer@police.gov.zm",
            PasswordHash = "not-a-real-hash",
            BranchReferenceNumber = "BR-001",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var found = await db.Users
            .Include(u => u.Branch)
            .SingleAsync(u => u.ForceNumber == "ZP-00001");

        Assert.Equal("Test Officer", found.FullName);
        Assert.Equal("BR-001", found.BranchReferenceNumber);
        Assert.Equal("Kitwe Central", found.Branch!.Name);
    }

    [Fact]
    public async Task Duplicate_force_number_is_rejected_by_the_database()
    {
        using var db = CreateContext();

        for (var i = 0; i < 2; i++)
        {
            db.Users.Add(new User
            {
                FullName = $"Officer {i}",
                ForceNumber = "ZP-00002",
                Email = $"officer{i}@police.gov.zm",
                PasswordHash = "not-a-real-hash",
                BranchReferenceNumber = "BR-001",
                CreatedAt = DateTime.UtcNow
            });
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_user_cannot_reference_a_branch_that_does_not_exist()
    {
        using var db = CreateContext();

        db.Users.Add(new User
        {
            FullName = "Ghost Officer",
            ForceNumber = "ZP-00003",
            Email = "ghost@police.gov.zm",
            PasswordHash = "not-a-real-hash",
            BranchReferenceNumber = "BR-DOES-NOT-EXIST",
            CreatedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ── Registration ──────────────────────────────────────────

    [Fact]
    public async Task Registration_stores_the_password_as_a_hash_never_as_plain_text()
    {
        using var db = CreateContext();
        var auth = CreateService(db);

        var result = await auth.RegisterAsync(
            "Grace Banda", "ZP-01234", "grace.banda@police.gov.zm", "Password123!", "BR-001");

        Assert.Equal(RegistrationResult.Success, result);

        var stored = await db.Users.SingleAsync(u => u.ForceNumber == "ZP-01234");
        Assert.NotEqual("Password123!", stored.PasswordHash);
        Assert.NotEmpty(stored.PasswordHash);
        Assert.Equal("BR-001", stored.BranchReferenceNumber);
    }

    [Fact]
    public async Task Registration_rejects_a_duplicate_force_number()
    {
        using var db = CreateContext();
        var auth = CreateService(db);

        await auth.RegisterAsync("First Officer", "ZP-05555", "first@police.gov.zm", "Password123!", "BR-001");
        var result = await auth.RegisterAsync("Second Officer", "ZP-05555", "second@police.gov.zm", "Password123!", "BR-002");

        Assert.Equal(RegistrationResult.DuplicateForceNumber, result);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Registration_rejects_a_duplicate_email()
    {
        using var db = CreateContext();
        var auth = CreateService(db);

        await auth.RegisterAsync("First Officer", "ZP-06001", "shared@police.gov.zm", "Password123!", "BR-001");
        var result = await auth.RegisterAsync("Second Officer", "ZP-06002", "shared@police.gov.zm", "Password123!", "BR-002");

        Assert.Equal(RegistrationResult.DuplicateEmail, result);
    }

    [Fact]
    public async Task Registration_rejects_an_unknown_branch()
    {
        using var db = CreateContext();
        var auth = CreateService(db);

        var result = await auth.RegisterAsync(
            "Grace Banda", "ZP-07000", "grace.b@police.gov.zm", "Password123!", "BR-NOPE");

        Assert.Equal(RegistrationResult.UnknownBranch, result);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    // ── Credential verification ───────────────────────────────

    [Fact]
    public async Task A_correct_password_authenticates()
    {
        using var db = CreateContext();
        var auth = CreateService(db);
        await auth.RegisterAsync("Grace Banda", "ZP-01234", "grace.banda@police.gov.zm", "Password123!", "BR-001");

        var user = await auth.ValidateCredentialsAsync("ZP-01234", "Password123!");

        Assert.NotNull(user);
        Assert.Equal("Grace Banda", user!.FullName);
    }

    [Fact]
    public async Task An_incorrect_password_does_not_authenticate()
    {
        using var db = CreateContext();
        var auth = CreateService(db);
        await auth.RegisterAsync("Grace Banda", "ZP-01234", "grace.banda@police.gov.zm", "Password123!", "BR-001");

        var user = await auth.ValidateCredentialsAsync("ZP-01234", "WrongPassword!");

        Assert.Null(user);
    }

    [Fact]
    public async Task An_unknown_force_number_does_not_authenticate()
    {
        using var db = CreateContext();
        var auth = CreateService(db);

        var user = await auth.ValidateCredentialsAsync("ZP-99999", "Password123!");

        Assert.Null(user);
    }

    [Fact]
    public async Task A_signed_in_user_carries_its_branch_and_company()
    {
        using var db = CreateContext();
        var auth = CreateService(db);
        await auth.RegisterAsync("Grace Banda", "ZP-01234", "grace.banda@police.gov.zm", "Password123!", "BR-004");

        var user = await auth.ValidateCredentialsAsync("ZP-01234", "Password123!");

        Assert.NotNull(user);
        Assert.Equal("RTSA Kitwe Station", user!.Branch!.Name);
        Assert.Equal("Road Transport and Safety Agency", user.Branch!.Company!.Name);
    }
}
