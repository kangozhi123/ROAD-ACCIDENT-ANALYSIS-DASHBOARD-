using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;

namespace RoadSafety.Tests;

public class OfficerServiceTests
{
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

    private static async Task<int> AddOfficerAsync(
        AppDbContext db, string forceNumber, string email, string branch = "BR-001")
    {
        var auth = new AuthService(db, new PasswordHasher<User>());
        await auth.RegisterAsync("Grace Banda", forceNumber, email, "Password123!", branch);

        return (await db.Users.SingleAsync(u => u.ForceNumber == forceNumber)).Id;
    }

    [Fact]
    public async Task An_officer_can_be_fetched_by_id_with_station_and_organisation()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "ZP-01000", "a@police.gov.zm", "BR-004");
        var officers = new OfficerService(db);

        var officer = await officers.GetAsync(id);

        Assert.NotNull(officer);
        Assert.Equal("ZP-01000", officer!.ForceNumber);
        Assert.Equal("RTSA Kitwe Station", officer.BranchName);
        Assert.Equal("Road Transport and Safety Agency", officer.CompanyName);
        Assert.Equal(2, officer.CompanyId);
    }

    [Fact]
    public async Task Fetching_an_officer_that_does_not_exist_returns_null()
    {
        using var db = CreateContext();
        var officers = new OfficerService(db);

        Assert.Null(await officers.GetAsync(4242));
    }

    [Fact]
    public async Task Updating_changes_the_name_email_and_posting()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "ZP-02000", "before@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(id, "Grace M Banda", "after@police.gov.zm", "BR-002");

        Assert.Equal(OfficerUpdateResult.Success, result);

        var stored = await db.Users.SingleAsync(u => u.Id == id);
        Assert.Equal("Grace M Banda", stored.FullName);
        Assert.Equal("after@police.gov.zm", stored.Email);
        Assert.Equal("BR-002", stored.BranchReferenceNumber);
    }

    [Fact]
    public async Task Updating_never_changes_the_force_number()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "ZP-02500", "keep@police.gov.zm");
        var officers = new OfficerService(db);

        await officers.UpdateAsync(id, "New Name", "new@police.gov.zm", "BR-001");

        // Officers sign in with the force number; the update path must not
        // offer a way to change it out from under them.
        Assert.Equal("ZP-02500", (await db.Users.SingleAsync(u => u.Id == id)).ForceNumber);
    }

    [Fact]
    public async Task Updating_rejects_an_email_another_officer_already_uses()
    {
        using var db = CreateContext();
        await AddOfficerAsync(db, "ZP-03000", "taken@police.gov.zm");
        var id = await AddOfficerAsync(db, "ZP-03001", "mine@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(id, "Grace Banda", "taken@police.gov.zm", "BR-001");

        Assert.Equal(OfficerUpdateResult.DuplicateEmail, result);
    }

    [Fact]
    public async Task Keeping_your_own_email_while_editing_is_allowed()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "ZP-03100", "same@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(id, "Renamed Officer", "same@police.gov.zm", "BR-001");

        Assert.Equal(OfficerUpdateResult.Success, result);
    }

    [Fact]
    public async Task Updating_rejects_a_station_that_does_not_exist()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "ZP-03200", "b@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(id, "Grace Banda", "b@police.gov.zm", "BR-NOPE");

        Assert.Equal(OfficerUpdateResult.UnknownBranch, result);
    }

    [Fact]
    public async Task Updating_an_officer_that_does_not_exist_reports_not_found()
    {
        using var db = CreateContext();
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(999, "Nobody", "nobody@police.gov.zm", "BR-001");

        Assert.Equal(OfficerUpdateResult.NotFound, result);
    }

    [Fact]
    public async Task Deleting_removes_the_officer()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "ZP-04000", "gone@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.DeleteAsync(id, currentUserId: 999);

        Assert.Equal(OfficerDeleteResult.Success, result);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task An_officer_cannot_delete_their_own_account()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "ZP-04100", "self@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.DeleteAsync(id, currentUserId: id);

        Assert.Equal(OfficerDeleteResult.CannotDeleteSelf, result);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Deleting_an_officer_that_does_not_exist_reports_not_found()
    {
        using var db = CreateContext();
        var officers = new OfficerService(db);

        Assert.Equal(OfficerDeleteResult.NotFound, await officers.DeleteAsync(777, currentUserId: 1));
    }

    [Fact]
    public async Task The_list_returns_the_newest_officer_first()
    {
        using var db = CreateContext();
        await AddOfficerAsync(db, "ZP-05000", "first@police.gov.zm");
        await Task.Delay(10);
        await AddOfficerAsync(db, "ZP-05001", "second@police.gov.zm");
        var officers = new OfficerService(db);

        var rows = await officers.ListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal("ZP-05001", rows[0].ForceNumber);
    }
}
