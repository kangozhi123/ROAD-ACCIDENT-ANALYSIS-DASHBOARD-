using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;

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
}
