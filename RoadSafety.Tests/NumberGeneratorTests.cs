using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Helpers;
using RoadSafety.Web.Models;
using RoadSafety.Web.Services;

namespace RoadSafety.Tests;

public class ReferenceNumberTests
{
    [Theory]
    [InlineData("ZP", 1, 5, "ZP-00001")]
    [InlineData("ZP", 42, 5, "ZP-00042")]
    [InlineData("BR", 7, 3, "BR-007")]
    [InlineData("CO", 123, 3, "CO-123")]
    [InlineData("BR", 1234, 3, "BR-1234")]   // outgrows the width rather than truncating
    public void Format_pads_the_sequence_to_the_requested_width(
        string prefix, int sequence, int width, string expected)
    {
        Assert.Equal(expected, ReferenceNumber.Format(prefix, sequence, width));
    }

    [Theory]
    [InlineData("ZP-00001", "ZP", 1)]
    [InlineData("ZP-04422", "ZP", 4422)]
    [InlineData("br-007", "BR", 7)]          // prefix match is case insensitive
    public void TryParseSequence_reads_the_numeric_tail(string value, string prefix, int expected)
    {
        Assert.True(ReferenceNumber.TryParseSequence(value, prefix, out var sequence));
        Assert.Equal(expected, sequence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ZP")]
    [InlineData("ZP-")]
    [InlineData("ZP-ABC")]
    [InlineData("ZP-12A")]
    [InlineData("BR-001")]                   // wrong prefix for ZP
    [InlineData("ZP-00001-X")]
    public void TryParseSequence_refuses_anything_that_is_not_prefix_then_digits(string? value)
    {
        Assert.False(ReferenceNumber.TryParseSequence(value, "ZP", out _));
    }

    [Theory]
    [InlineData("Grace Banda", "GB")]
    [InlineData("grace banda", "GB")]
    [InlineData("  Grace   Banda  ", "GB")]
    [InlineData("Grace Mutale Banda", "GM")]      // first two words only
    [InlineData("Thelma", "TH")]                  // one name uses two letters
    [InlineData("T", "T")]
    [InlineData("Mary-Jane Zulu", "MZ")]
    [InlineData("O'Brien Smith", "OS")]
    [InlineData("2Pac Shakur", "PS")]             // skips the leading digit
    public void InitialsFrom_builds_the_prefix_from_the_name(string name, string expected)
    {
        Assert.Equal(expected, ReferenceNumber.InitialsFrom(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123 456")]
    public void InitialsFrom_falls_back_when_the_name_has_no_letters(string? name)
    {
        // A force number still has to be issued, so this cannot simply fail.
        Assert.Equal("ZP", ReferenceNumber.InitialsFrom(name));
    }

    [Fact]
    public void Next_starts_at_one_when_nothing_exists()
    {
        Assert.Equal("ZP-00001", ReferenceNumber.Next([], "ZP", 5));
    }

    [Fact]
    public void Next_follows_the_highest_not_the_count()
    {
        // Two rows, but the highest is 9 — counting would hand out a duplicate.
        var existing = new[] { "ZP-00001", "ZP-00009" };

        Assert.Equal("ZP-00010", ReferenceNumber.Next(existing, "ZP", 5));
    }

    [Fact]
    public void Next_ignores_rows_it_cannot_read()
    {
        var existing = new[] { "ZP-00003", "legacy-7", "", null, "BR-900" };

        Assert.Equal("ZP-00004", ReferenceNumber.Next(existing, "ZP", 5));
    }

    [Fact]
    public void Next_does_not_reuse_a_gap_left_by_a_deleted_officer()
    {
        // ZP-00002 was removed. Reissuing it would attach a new officer to a
        // number that already appears in older paperwork.
        var existing = new[] { "ZP-00001", "ZP-00003" };

        Assert.Equal("ZP-00004", ReferenceNumber.Next(existing, "ZP", 5));
    }
}

public class NumberGeneratorTests
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

    [Fact]
    public async Task The_first_force_number_is_ZP_00001()
    {
        using var db = CreateContext();

        Assert.Equal("ZP-00001", await new NumberGenerator(db).NextForceNumberAsync());
    }

    [Fact]
    public async Task The_next_force_number_follows_the_officers_already_stored()
    {
        using var db = CreateContext();
        var auth = new AuthService(db, new PasswordHasher<User>());
        await auth.RegisterAsync("Grace Banda", "ZP-00007", "g@police.gov.zm", "Password123!", "BR-001");

        Assert.Equal("ZP-00008", await new NumberGenerator(db).NextForceNumberAsync());
    }

    [Fact]
    public async Task Station_and_organisation_references_continue_from_the_seeded_rows()
    {
        using var db = CreateContext();
        var numbers = new NumberGenerator(db);

        // The migration seeds BR-001..BR-004 and CO-001..CO-002.
        Assert.Equal("BR-005", await numbers.NextBranchReferenceAsync());
        Assert.Equal("CO-003", await numbers.NextCompanyReferenceAsync());
    }

    [Fact]
    public async Task A_force_number_is_prefixed_with_the_officers_initials()
    {
        using var db = CreateContext();

        Assert.Equal("GB-00001", await new NumberGenerator(db).NextForceNumberForAsync("Grace Banda"));
    }

    [Fact]
    public async Task Each_set_of_initials_carries_its_own_sequence()
    {
        using var db = CreateContext();
        var numbers = new NumberGenerator(db);
        var auth = new AuthService(db, new PasswordHasher<User>());

        await auth.RegisterAsync("Grace Banda", "GB-00001", "gb@police.gov.zm", "Password123!", "BR-001");

        // A second GB continues that run; a different name starts its own.
        Assert.Equal("GB-00002", await numbers.NextForceNumberForAsync("Gift Bwalya"));
        Assert.Equal("PM-00001", await numbers.NextForceNumberForAsync("Peter Mwale"));
    }

    [Fact]
    public async Task An_officer_with_no_usable_name_still_gets_a_number()
    {
        using var db = CreateContext();

        Assert.Equal("ZP-00001", await new NumberGenerator(db).NextForceNumberForAsync("   "));
    }

    [Fact]
    public async Task A_generated_force_number_is_accepted_by_registration()
    {
        using var db = CreateContext();
        var numbers = new NumberGenerator(db);
        var auth = new AuthService(db, new PasswordHasher<User>());

        var generated = await numbers.NextForceNumberForAsync("Grace Banda");
        var result = await auth.RegisterAsync(
            "Grace Banda", generated, "g@police.gov.zm", "Password123!", "BR-001");

        Assert.Equal(RegistrationResult.Success, result);
        Assert.Equal("GB-00001", generated);

        // And the following call has moved on rather than repeating itself.
        Assert.NotEqual(generated, await numbers.NextForceNumberForAsync("Grace Banda"));
    }
}
