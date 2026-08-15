using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;

namespace RoadSafety.Tests;

public class OfficerServiceTests
{
    // BR-001 Kitwe Central and BR-002 Wusakile both belong to the police
    // service; BR-004 belongs to RTSA.
    private const string Kitwe = "BR-001";
    private const string Wusakile = "BR-002";

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
        AppDbContext db, string forceNumber, string email,
        string branch = Kitwe, UserRole role = UserRole.Officer, string name = "Grace Banda")
    {
        var auth = new AuthService(db, new PasswordHasher<User>());
        await auth.RegisterAsync(name, forceNumber, email, "Password123!", branch, role);

        return (await db.Users.SingleAsync(u => u.ForceNumber == forceNumber)).Id;
    }

    private static AccessScope Scope(int userId, UserRole role, string branch) =>
        new(userId, role, branch);

    private static AccessScope SystemAdmin(int userId = 999) =>
        Scope(userId, UserRole.SystemAdministrator, Kitwe);

    // ── Branch isolation ───────────────────────────────────────────────

    [Fact]
    public async Task A_station_administrator_sees_only_their_own_station()
    {
        using var db = CreateContext();
        var mine = await AddOfficerAsync(db, "KC-00001", "kc@police.gov.zm", Kitwe);
        await AddOfficerAsync(db, "WU-00001", "wu@police.gov.zm", Wusakile);
        var officers = new OfficerService(db);

        var rows = await officers.ListAsync(Scope(mine, UserRole.StationAdministrator, Kitwe));

        Assert.Single(rows);
        Assert.Equal("KC-00001", rows[0].ForceNumber);
    }

    [Fact]
    public async Task A_plain_officer_is_scoped_to_their_station_too()
    {
        using var db = CreateContext();
        var mine = await AddOfficerAsync(db, "KC-00002", "kc2@police.gov.zm", Kitwe);
        await AddOfficerAsync(db, "WU-00002", "wu2@police.gov.zm", Wusakile);
        var officers = new OfficerService(db);

        var rows = await officers.ListAsync(Scope(mine, UserRole.Officer, Kitwe));

        Assert.Single(rows);
    }

    [Fact]
    public async Task A_system_administrator_sees_every_station()
    {
        using var db = CreateContext();
        await AddOfficerAsync(db, "KC-00003", "kc3@police.gov.zm", Kitwe);
        await AddOfficerAsync(db, "WU-00003", "wu3@police.gov.zm", Wusakile);
        var officers = new OfficerService(db);

        Assert.Equal(2, (await officers.ListAsync(SystemAdmin())).Count);
    }

    [Fact]
    public async Task Fetching_an_officer_from_another_station_returns_null()
    {
        using var db = CreateContext();
        var mine = await AddOfficerAsync(db, "KC-00004", "kc4@police.gov.zm", Kitwe);
        var theirs = await AddOfficerAsync(db, "WU-00004", "wu4@police.gov.zm", Wusakile);
        var officers = new OfficerService(db);

        // Indistinguishable from "no such officer", so the endpoint cannot be
        // used to discover who works at another station.
        Assert.Null(await officers.GetAsync(theirs, Scope(mine, UserRole.StationAdministrator, Kitwe)));
        Assert.NotNull(await officers.GetAsync(theirs, SystemAdmin()));
    }

    [Fact]
    public async Task Editing_an_officer_from_another_station_is_refused()
    {
        using var db = CreateContext();
        var mine = await AddOfficerAsync(db, "KC-00005", "kc5@police.gov.zm", Kitwe);
        var theirs = await AddOfficerAsync(db, "WU-00005", "wu5@police.gov.zm", Wusakile);
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(
            theirs, "Renamed", "wu5@police.gov.zm", Wusakile,
            Scope(mine, UserRole.StationAdministrator, Kitwe));

        Assert.Equal(OfficerUpdateResult.NotFound, result);
        Assert.Equal("Grace Banda", (await db.Users.SingleAsync(u => u.Id == theirs)).FullName);
    }

    [Fact]
    public async Task Deleting_an_officer_from_another_station_is_refused()
    {
        using var db = CreateContext();
        var mine = await AddOfficerAsync(db, "KC-00006", "kc6@police.gov.zm", Kitwe);
        var theirs = await AddOfficerAsync(db, "WU-00006", "wu6@police.gov.zm", Wusakile);
        var officers = new OfficerService(db);

        var result = await officers.DeleteAsync(theirs, Scope(mine, UserRole.StationAdministrator, Kitwe));

        Assert.Equal(OfficerDeleteResult.NotFound, result);
        Assert.Equal(2, await db.Users.CountAsync());
    }

    [Fact]
    public async Task A_station_administrator_cannot_move_an_officer_to_another_station()
    {
        using var db = CreateContext();
        var mine = await AddOfficerAsync(db, "KC-00007", "kc7@police.gov.zm", Kitwe);
        var officers = new OfficerService(db);

        // Asking for Wusakile is resolved back to the caller's own station.
        await officers.UpdateAsync(
            mine, "Grace Banda", "kc7@police.gov.zm", Wusakile,
            Scope(mine, UserRole.StationAdministrator, Kitwe));

        Assert.Equal(Kitwe, (await db.Users.SingleAsync(u => u.Id == mine)).BranchReferenceNumber);
    }

    [Fact]
    public async Task A_system_administrator_can_move_an_officer_between_stations()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00008", "kc8@police.gov.zm", Kitwe);
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(
            id, "Grace Banda", "kc8@police.gov.zm", Wusakile, SystemAdmin());

        Assert.Equal(OfficerUpdateResult.Success, result);
        Assert.Equal(Wusakile, (await db.Users.SingleAsync(u => u.Id == id)).BranchReferenceNumber);
    }

    // ── Role permissions ───────────────────────────────────────────────

    [Fact]
    public async Task A_plain_officer_cannot_edit_anyone_even_at_their_own_station()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00009", "kc9@police.gov.zm", Kitwe);
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(
            id, "Renamed", "kc9@police.gov.zm", Kitwe, Scope(id, UserRole.Officer, Kitwe));

        Assert.Equal(OfficerUpdateResult.Forbidden, result);
    }

    [Fact]
    public async Task A_plain_officer_cannot_delete_anyone()
    {
        using var db = CreateContext();
        var me = await AddOfficerAsync(db, "KC-00010", "kc10@police.gov.zm", Kitwe);
        var other = await AddOfficerAsync(db, "KC-00011", "kc11@police.gov.zm", Kitwe);
        var officers = new OfficerService(db);

        var result = await officers.DeleteAsync(other, Scope(me, UserRole.Officer, Kitwe));

        Assert.Equal(OfficerDeleteResult.Forbidden, result);
        Assert.Equal(2, await db.Users.CountAsync());
    }

    [Fact]
    public async Task A_new_account_is_a_plain_officer_unless_told_otherwise()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00012", "kc12@police.gov.zm", Kitwe);

        Assert.Equal(UserRole.Officer, (await db.Users.SingleAsync(u => u.Id == id)).Role);
    }


    // ── Role assignment ────────────────────────────────────────────────

    [Fact]
    public async Task A_station_administrator_can_promote_someone_at_their_station()
    {
        using var db = CreateContext();
        var admin = await AddOfficerAsync(db, "AD-00001", "ad@police.gov.zm", Kitwe, UserRole.StationAdministrator);
        var target = await AddOfficerAsync(db, "TG-00001", "tg@police.gov.zm", Kitwe);
        var officers = new OfficerService(db);

        var result = await officers.ChangeRoleAsync(
            target, UserRole.StationAdministrator, Scope(admin, UserRole.StationAdministrator, Kitwe));

        Assert.Equal(RoleChangeResult.Success, result);
        Assert.Equal(UserRole.StationAdministrator, (await db.Users.SingleAsync(u => u.Id == target)).Role);
    }

    [Fact]
    public async Task A_station_administrator_cannot_grant_system_administrator()
    {
        using var db = CreateContext();
        var admin = await AddOfficerAsync(db, "AD-00002", "ad2@police.gov.zm", Kitwe, UserRole.StationAdministrator);
        var target = await AddOfficerAsync(db, "TG-00002", "tg2@police.gov.zm", Kitwe);
        var officers = new OfficerService(db);

        // A role nobody can grant is a role nobody can grant themselves.
        var result = await officers.ChangeRoleAsync(
            target, UserRole.SystemAdministrator, Scope(admin, UserRole.StationAdministrator, Kitwe));

        Assert.Equal(RoleChangeResult.Forbidden, result);
        Assert.Equal(UserRole.Officer, (await db.Users.SingleAsync(u => u.Id == target)).Role);
    }

    [Fact]
    public async Task A_station_administrator_cannot_demote_a_system_administrator()
    {
        using var db = CreateContext();
        var admin = await AddOfficerAsync(db, "AD-00003", "ad3@police.gov.zm", Kitwe, UserRole.StationAdministrator);
        var boss = await AddOfficerAsync(db, "BS-00001", "bs@police.gov.zm", Kitwe, UserRole.SystemAdministrator);
        var officers = new OfficerService(db);

        // Otherwise they could strip the account that outranks them.
        var result = await officers.ChangeRoleAsync(
            boss, UserRole.Officer, Scope(admin, UserRole.StationAdministrator, Kitwe));

        Assert.Equal(RoleChangeResult.Forbidden, result);
        Assert.Equal(UserRole.SystemAdministrator, (await db.Users.SingleAsync(u => u.Id == boss)).Role);
    }

    [Fact]
    public async Task Nobody_changes_their_own_role()
    {
        using var db = CreateContext();
        var admin = await AddOfficerAsync(db, "AD-00004", "ad4@police.gov.zm", Kitwe, UserRole.StationAdministrator);
        var officers = new OfficerService(db);

        var result = await officers.ChangeRoleAsync(
            admin, UserRole.SystemAdministrator, Scope(admin, UserRole.StationAdministrator, Kitwe));

        Assert.Equal(RoleChangeResult.CannotChangeOwnRole, result);
    }

    [Fact]
    public async Task A_role_cannot_be_changed_at_another_station()
    {
        using var db = CreateContext();
        var admin = await AddOfficerAsync(db, "AD-00005", "ad5@police.gov.zm", Kitwe, UserRole.StationAdministrator);
        var theirs = await AddOfficerAsync(db, "WU-00099", "wu99@police.gov.zm", Wusakile);
        var officers = new OfficerService(db);

        var result = await officers.ChangeRoleAsync(
            theirs, UserRole.StationAdministrator, Scope(admin, UserRole.StationAdministrator, Kitwe));

        Assert.Equal(RoleChangeResult.NotFound, result);
    }

    [Fact]
    public async Task A_plain_officer_cannot_assign_roles()
    {
        using var db = CreateContext();
        var me = await AddOfficerAsync(db, "PO-00001", "po@police.gov.zm", Kitwe);
        var other = await AddOfficerAsync(db, "PO-00002", "po2@police.gov.zm", Kitwe);
        var officers = new OfficerService(db);

        var result = await officers.ChangeRoleAsync(
            other, UserRole.StationAdministrator, Scope(me, UserRole.Officer, Kitwe));

        Assert.Equal(RoleChangeResult.Forbidden, result);
    }

    [Fact]
    public async Task A_system_administrator_can_grant_every_role()
    {
        using var db = CreateContext();
        var target = await AddOfficerAsync(db, "TG-00003", "tg3@police.gov.zm", Wusakile);
        var officers = new OfficerService(db);

        Assert.Equal(RoleChangeResult.Success,
            await officers.ChangeRoleAsync(target, UserRole.SystemAdministrator, SystemAdmin()));
        Assert.Equal(UserRole.SystemAdministrator, (await db.Users.SingleAsync(u => u.Id == target)).Role);
    }

    [Fact]
    public void Assignable_roles_stop_short_of_the_callers_own_ceiling()
    {
        var station = Scope(1, UserRole.StationAdministrator, Kitwe);
        var system = Scope(1, UserRole.SystemAdministrator, Kitwe);

        Assert.DoesNotContain(UserRole.SystemAdministrator, station.AssignableRoles);
        Assert.Contains(UserRole.StationAdministrator, station.AssignableRoles);
        Assert.Equal(3, system.AssignableRoles.Count);
    }

    // ── The rules that already held ────────────────────────────────────


    [Fact]
    public async Task An_officer_can_be_fetched_by_id_with_station_and_organisation()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "RT-00001", "rt@police.gov.zm", "BR-004");
        var officers = new OfficerService(db);

        var officer = await officers.GetAsync(id, SystemAdmin());

        Assert.NotNull(officer);
        Assert.Equal("RTSA Kitwe Station", officer!.BranchName);
        Assert.Equal("Road Transport and Safety Agency", officer.CompanyName);
        Assert.Equal(2, officer.CompanyId);
    }

    [Fact]
    public async Task Fetching_an_officer_that_does_not_exist_returns_null()
    {
        using var db = CreateContext();

        Assert.Null(await new OfficerService(db).GetAsync(4242, SystemAdmin()));
    }

    [Fact]
    public async Task Updating_changes_the_name_email_and_posting()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00013", "before@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(
            id, "Grace M Banda", "after@police.gov.zm", Wusakile, SystemAdmin());

        Assert.Equal(OfficerUpdateResult.Success, result);

        var stored = await db.Users.SingleAsync(u => u.Id == id);
        Assert.Equal("Grace M Banda", stored.FullName);
        Assert.Equal("after@police.gov.zm", stored.Email);
        Assert.Equal(Wusakile, stored.BranchReferenceNumber);
    }

    [Fact]
    public async Task Updating_never_changes_the_force_number()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00014", "keep@police.gov.zm");
        var officers = new OfficerService(db);

        await officers.UpdateAsync(id, "New Name", "new@police.gov.zm", Kitwe, SystemAdmin());

        // Officers sign in with the force number; the update path must not
        // offer a way to change it out from under them.
        Assert.Equal("KC-00014", (await db.Users.SingleAsync(u => u.Id == id)).ForceNumber);
    }

    [Fact]
    public async Task Updating_rejects_an_email_another_officer_already_uses()
    {
        using var db = CreateContext();
        await AddOfficerAsync(db, "KC-00015", "taken@police.gov.zm");
        var id = await AddOfficerAsync(db, "KC-00016", "mine@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(
            id, "Grace Banda", "taken@police.gov.zm", Kitwe, SystemAdmin());

        Assert.Equal(OfficerUpdateResult.DuplicateEmail, result);
    }

    [Fact]
    public async Task Keeping_your_own_email_while_editing_is_allowed()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00017", "same@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(
            id, "Renamed Officer", "same@police.gov.zm", Kitwe, SystemAdmin());

        Assert.Equal(OfficerUpdateResult.Success, result);
    }

    [Fact]
    public async Task Updating_rejects_a_station_that_does_not_exist()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00018", "b@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.UpdateAsync(
            id, "Grace Banda", "b@police.gov.zm", "BR-NOPE", SystemAdmin());

        Assert.Equal(OfficerUpdateResult.UnknownBranch, result);
    }

    [Fact]
    public async Task Updating_an_officer_that_does_not_exist_reports_not_found()
    {
        using var db = CreateContext();

        var result = await new OfficerService(db).UpdateAsync(
            999, "Nobody", "nobody@police.gov.zm", Kitwe, SystemAdmin());

        Assert.Equal(OfficerUpdateResult.NotFound, result);
    }

    [Fact]
    public async Task Deleting_removes_the_officer()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00019", "gone@police.gov.zm");
        var officers = new OfficerService(db);

        var result = await officers.DeleteAsync(id, SystemAdmin());

        Assert.Equal(OfficerDeleteResult.Success, result);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task An_officer_cannot_delete_their_own_account()
    {
        using var db = CreateContext();
        var id = await AddOfficerAsync(db, "KC-00020", "self@police.gov.zm",
            role: UserRole.SystemAdministrator);
        var officers = new OfficerService(db);

        var result = await officers.DeleteAsync(id, Scope(id, UserRole.SystemAdministrator, Kitwe));

        Assert.Equal(OfficerDeleteResult.CannotDeleteSelf, result);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Deleting_an_officer_that_does_not_exist_reports_not_found()
    {
        using var db = CreateContext();

        Assert.Equal(OfficerDeleteResult.NotFound,
            await new OfficerService(db).DeleteAsync(777, SystemAdmin()));
    }

    [Fact]
    public async Task The_list_returns_the_newest_officer_first()
    {
        using var db = CreateContext();
        await AddOfficerAsync(db, "KC-00021", "first@police.gov.zm");
        await Task.Delay(10);
        await AddOfficerAsync(db, "KC-00022", "second@police.gov.zm");
        var officers = new OfficerService(db);

        var rows = await officers.ListAsync(SystemAdmin());

        Assert.Equal(2, rows.Count);
        Assert.Equal("KC-00022", rows[0].ForceNumber);
    }
}
