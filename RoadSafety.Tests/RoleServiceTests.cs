using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;

namespace RoadSafety.Tests;

public class RoleServiceTests
{
    private const string Kitwe = "BR-001";

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

    private static AccessScope SystemAdmin(int userId = 999) =>
        new(userId, Role.SystemAdministratorId, Kitwe, true, true, true, true);

    private static AccessScope StationAdmin(int userId = 500) =>
        new(userId, Role.StationAdministratorId, Kitwe, false, true, true, false);

    private static AccessScope PlainOfficer(int userId = 100) =>
        new(userId, Role.OfficerId, Kitwe, false, false, false, false);

    private static Role Grants(bool branches = false, bool officers = false,
                               bool assign = false, bool manage = false) => new()
    {
        SeesEveryBranch = branches,
        CanManageOfficers = officers,
        CanAssignRoles = assign,
        CanManageRoles = manage
    };

    // ── Seeded state ───────────────────────────────────────────────────

    [Fact]
    public async Task The_three_built_in_roles_are_seeded()
    {
        using var db = CreateContext();

        var rows = await new RoleService(db).ListAsync();

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.True(r.IsBuiltIn));
        Assert.Contains(rows, r => r.Name == "System administrator" && r.CanManageRoles);
    }

    // ── Creating ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_role_can_be_created()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);

        var (result, id) = await roles.CreateAsync(
            "Traffic supervisor", "Watches the junctions.", Grants(officers: true), SystemAdmin());

        Assert.Equal(RoleSaveResult.Success, result);

        var created = await roles.GetAsync(id);
        Assert.Equal("Traffic supervisor", created!.Name);
        Assert.True(created.CanManageOfficers);
        Assert.False(created.IsBuiltIn);
    }

    [Fact]
    public async Task A_role_cannot_be_created_granting_more_than_the_caller_holds()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);

        // Otherwise a station administrator could write a stronger role, assign
        // it to someone, and have that person hand it back.
        var (result, _) = await roles.CreateAsync(
            "Sneaky", "More than I have.", Grants(branches: true, manage: true), StationAdmin());

        Assert.Equal(RoleSaveResult.Forbidden, result);
        Assert.Equal(3, (await roles.ListAsync()).Count);
    }

    [Fact]
    public async Task A_role_needs_a_name()
    {
        using var db = CreateContext();

        var (result, _) = await new RoleService(db).CreateAsync("   ", "", Grants(), SystemAdmin());

        Assert.Equal(RoleSaveResult.NameRequired, result);
    }

    [Fact]
    public async Task Two_roles_cannot_share_a_name()
    {
        using var db = CreateContext();

        var (result, _) = await new RoleService(db).CreateAsync("Officer", "", Grants(), SystemAdmin());

        Assert.Equal(RoleSaveResult.DuplicateName, result);
    }

    [Fact]
    public async Task A_plain_officer_cannot_create_a_role()
    {
        using var db = CreateContext();

        var (result, _) = await new RoleService(db).CreateAsync("Anything", "", Grants(), PlainOfficer());

        Assert.Equal(RoleSaveResult.Forbidden, result);
    }

    // ── Editing ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_custom_roles_name_and_grants_can_be_edited()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);
        var (_, id) = await roles.CreateAsync("Supervisor", "First draft.", Grants(), SystemAdmin());

        var result = await roles.UpdateAsync(
            id, "Traffic supervisor", "Second draft.", Grants(officers: true), SystemAdmin());

        Assert.Equal(RoleSaveResult.Success, result);

        var updated = await roles.GetAsync(id);
        Assert.Equal("Traffic supervisor", updated!.Name);
        Assert.True(updated.CanManageOfficers);
    }

    [Fact]
    public async Task A_built_in_role_can_be_renamed_but_not_regranted()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);

        // A name is a label, so renaming is fine.
        var rename = await roles.UpdateAsync(
            Role.OfficerId, "Constable", "Reads their own station.", Grants(), SystemAdmin());
        Assert.Equal(RoleSaveResult.Success, rename);

        // Its grants are fixed, so there is always a working ladder back.
        var regrant = await roles.UpdateAsync(
            Role.OfficerId, "Constable", "Reads their own station.",
            Grants(branches: true, manage: true), SystemAdmin());
        Assert.Equal(RoleSaveResult.BuiltInPermissionsLocked, regrant);

        Assert.False((await roles.GetAsync(Role.OfficerId))!.CanManageRoles);
    }

    [Fact]
    public async Task Nobody_edits_the_grants_of_the_role_they_hold()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);
        var (_, id) = await roles.CreateAsync("Supervisor", "", Grants(officers: true), SystemAdmin());

        // Standing on the role you are editing is how you lock yourself out,
        // or quietly promote yourself.
        var mine = new AccessScope(7, id, Kitwe, true, true, true, true);
        var result = await roles.UpdateAsync(id, "Supervisor", "", Grants(), mine);

        Assert.Equal(RoleSaveResult.CannotEditOwnRole, result);
    }

    [Fact]
    public async Task Editing_only_the_name_of_your_own_role_is_allowed()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);
        var (_, id) = await roles.CreateAsync("Supervisor", "", Grants(officers: true), SystemAdmin());

        var mine = new AccessScope(7, id, Kitwe, true, true, true, true);
        var result = await roles.UpdateAsync(id, "Shift supervisor", "Renamed.", Grants(officers: true), mine);

        Assert.Equal(RoleSaveResult.Success, result);
    }

    [Fact]
    public async Task Editing_a_role_that_does_not_exist_reports_not_found()
    {
        using var db = CreateContext();

        var result = await new RoleService(db).UpdateAsync(404, "Ghost", "", Grants(), SystemAdmin());

        Assert.Equal(RoleSaveResult.NotFound, result);
    }

    // ── Deleting ───────────────────────────────────────────────────────

    [Fact]
    public async Task A_custom_role_nobody_holds_can_be_deleted()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);
        var (_, id) = await roles.CreateAsync("Temporary", "", Grants(), SystemAdmin());

        Assert.Equal(RoleDeleteResult.Success, await roles.DeleteAsync(id, SystemAdmin()));
        Assert.Null(await roles.GetAsync(id));
    }

    [Fact]
    public async Task A_built_in_role_cannot_be_deleted()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);

        // Officer, not the caller's own role — otherwise the own-role guard
        // fires first and this would not be testing what it claims to.
        Assert.Equal(RoleDeleteResult.BuiltInCannotBeDeleted,
            await roles.DeleteAsync(Role.OfficerId, SystemAdmin()));
    }

    [Fact]
    public async Task A_role_officers_still_hold_cannot_be_deleted()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);
        var (_, id) = await roles.CreateAsync("Supervisor", "", Grants(), SystemAdmin());

        var auth = new AuthService(db, new PasswordHasher<User>());
        await auth.RegisterAsync("Grace Banda", "GB-00001", "gb@police.gov.zm", "Password123!", Kitwe, id);

        // Deleting it would either orphan the row or silently reassign a real
        // person, so the officers have to be moved first.
        Assert.Equal(RoleDeleteResult.StillInUse, await roles.DeleteAsync(id, SystemAdmin()));
        Assert.NotNull(await roles.GetAsync(id));
    }

    [Fact]
    public async Task Nobody_deletes_the_role_they_hold()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);
        var (_, id) = await roles.CreateAsync("Supervisor", "", Grants(), SystemAdmin());

        var mine = new AccessScope(7, id, Kitwe, true, true, true, true);

        Assert.Equal(RoleDeleteResult.CannotDeleteOwnRole, await roles.DeleteAsync(id, mine));
    }

    [Fact]
    public async Task A_plain_officer_cannot_delete_a_role()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);
        var (_, id) = await roles.CreateAsync("Temporary", "", Grants(), SystemAdmin());

        Assert.Equal(RoleDeleteResult.Forbidden, await roles.DeleteAsync(id, PlainOfficer()));
    }

    [Fact]
    public async Task A_station_administrator_cannot_manage_roles_at_all()
    {
        using var db = CreateContext();
        var roles = new RoleService(db);
        var (_, id) = await roles.CreateAsync("Temporary", "", Grants(), SystemAdmin());

        Assert.Equal(RoleDeleteResult.Forbidden, await roles.DeleteAsync(id, StationAdmin()));
    }
}
