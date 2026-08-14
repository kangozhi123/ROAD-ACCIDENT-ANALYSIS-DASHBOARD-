# Phase 1: Authentication — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the PHP authentication layer with an ASP.NET Core Razor Pages application where an officer can register, log in, reach an authorized dashboard, and log out.

**Architecture:** One flat Razor Pages project plus one xUnit test project. Cookie authentication (not ASP.NET Core Identity) with password hashing via `IPasswordHasher<User>`. EF Core with SQLite, one migration. An `AuthService` holds all credential logic so it is unit-testable without a web server.

**Tech Stack:** .NET 10 (LTS), ASP.NET Core Razor Pages, EF Core 10.0.11 + SQLite, `Microsoft.Extensions.Identity.Core` 10.0.11 (for `PasswordHasher<T>` only), xUnit, `Microsoft.AspNetCore.Mvc.Testing` 10.0.11.

**Spec:** `docs/superpowers/specs/2026-08-15-csharp-rebuild-design.md`

## Global Constraints

- Target framework: `net10.0`. .NET 10 is the LTS release; .NET 9 is also installed but must not be used.
- All Microsoft NuGet packages pinned to `10.0.11`.
- ASP.NET Core Identity must **not** be used. Only `PasswordHasher<T>` from `Microsoft.Extensions.Identity.Core`.
- No homemade cryptography. Password hashing is `IPasswordHasher<User>` only.
- Failed login returns the generic message `Invalid credentials` — it must never reveal whether a force number exists.
- Exception details are never returned to the browser. Log server-side, return generic text.
- `ForceNumber` is the login identifier, and is unique. `Email` is also unique.
- Phase 1 delivers a near-empty `Dashboard` page. No charts, no map, no accident data — that is phase 2.
- Working branch: `csharp-rebuild`.

## File Structure

| File | Responsibility |
|---|---|
| `RoadSafety.sln` | Solution tying the two projects together |
| `RoadSafety.Web/Program.cs` | Service registration, auth wiring, middleware pipeline, startup seed |
| `RoadSafety.Web/Data/Company.cs` | The `Company` entity — top of the hierarchy |
| `RoadSafety.Web/Data/Branch.cs` | The `Branch` entity — belongs to a company |
| `RoadSafety.Web/Data/User.cs` | The `User` entity — data only, no behaviour |
| `RoadSafety.Web/Data/AppDbContext.cs` | EF Core context, relationships, unique indexes, lookup seed data |
| `RoadSafety.Web/Data/SeedData.cs` | Applies migrations and inserts the demo officer once at startup |
| `RoadSafety.Web/Services/AuthService.cs` | Registration and credential verification — all auth logic, no HTTP |
| `RoadSafety.Web/Pages/Index.cshtml(.cs)` | Login page |
| `RoadSafety.Web/Pages/Register.cshtml(.cs)` | Registration page |
| `RoadSafety.Web/Pages/Dashboard.cshtml(.cs)` | Authorized landing page |
| `RoadSafety.Web/Pages/Logout.cshtml.cs` | Sign-out handler |
| `RoadSafety.Tests/AuthServiceTests.cs` | Unit tests for registration and credential checks |
| `RoadSafety.Tests/TestWebAppFactory.cs` | Boots the app against in-memory SQLite for integration tests |
| `RoadSafety.Tests/DashboardAccessTests.cs` | Integration test for the anonymous redirect |

`AuthService` deliberately has no dependency on `HttpContext`. That is what makes tasks 3 and 4 testable without spinning up a web server, and it is the single most important structural decision in this phase.

---

### Task 1: Solution scaffold and PHP retirement

**Files:**
- Create: `RoadSafety.sln`, `RoadSafety.Web/` (template output), `RoadSafety.Tests/` (template output), `.gitignore`
- Delete: `index.php`, `login.php`, `logout.php`, `register.php`, `create-account.php`, `dashboard.php`, `db.php`, `predict.php`, `seed_db.php`, `app.py`
- Untrack: `database.sqlite`

**Interfaces:**
- Consumes: nothing (first task)
- Produces: a building solution with projects named `RoadSafety.Web` and `RoadSafety.Tests`; `RoadSafety.Tests` references `RoadSafety.Web`

- [ ] **Step 1: Confirm you are on the working branch**

```bash
git rev-parse --abbrev-ref HEAD
```
Expected: `csharp-rebuild`. If not, run `git checkout csharp-rebuild`.

- [ ] **Step 2: Create the solution and both projects**

```bash
dotnet new sln -n RoadSafety
dotnet new webapp -n RoadSafety.Web -f net10.0
dotnet new xunit -n RoadSafety.Tests -f net10.0
dotnet sln add RoadSafety.Web/RoadSafety.Web.csproj
dotnet sln add RoadSafety.Tests/RoadSafety.Tests.csproj
dotnet add RoadSafety.Tests/RoadSafety.Tests.csproj reference RoadSafety.Web/RoadSafety.Web.csproj
```

- [ ] **Step 3: Change the test project SDK so it can host the web app**

`WebApplicationFactory` (task 7) needs the ASP.NET Core shared framework. Open `RoadSafety.Tests/RoadSafety.Tests.csproj` and change the very first line from `<Project Sdk="Microsoft.NET.Sdk">` to:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
```

Leave the rest of the file untouched.

- [ ] **Step 4: Add the .gitignore**

```bash
dotnet new gitignore
```

Then append these three lines to `.gitignore` (the template does not cover them):

```gitignore
# Local database — never commit
*.db
*.db-shm
*.db-wal
database.sqlite
```

- [ ] **Step 5: Delete the PHP application and the dead Python file**

```bash
git rm index.php login.php logout.php register.php create-account.php dashboard.php db.php predict.php seed_db.php app.py
git rm --cached database.sqlite
```

`app.py` is removed because it references `accident_model.pkl` and `templates/index.html`, neither of which exists — it has never run. `database.sqlite` is untracked but left on disk; `.gitignore` now keeps it out.

Keep `index.html`, `dashboard.html`, `create-account.html`, `script.js`, `styles.css` for now. Phase 2 decides their fate.

- [ ] **Step 6: Verify the solution builds and the test runner works**

```bash
dotnet build
dotnet test
```
Expected: build succeeds with 0 errors; `dotnet test` runs and passes (the template ships one trivial test, or zero tests — either is fine).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Scaffold ASP.NET Core solution and retire PHP application

Adds RoadSafety.Web (Razor Pages) and RoadSafety.Tests (xUnit) on
net10.0. Removes the PHP files and app.py, which referenced a model
file and templates directory that never existed in the repository.
Untracks database.sqlite and gitignores local database files."
```

---

### Task 2: Company, Branch and User entities, DbContext, and migration

**Files:**
- Create: `RoadSafety.Web/Data/Company.cs`, `RoadSafety.Web/Data/Branch.cs`, `RoadSafety.Web/Data/User.cs`, `RoadSafety.Web/Data/AppDbContext.cs`
- Create: `RoadSafety.Tests/AuthServiceTests.cs`
- Modify: `RoadSafety.Web/Program.cs`, `RoadSafety.Web/appsettings.json`

**Interfaces:**
- Consumes: the project layout from Task 1
- Produces:
  - `RoadSafety.Web.Data.Company` with `int Id`, `string ReferenceNumber`, `string Code`, `string Name`, `string? RegistrationNumber`
  - `RoadSafety.Web.Data.Branch` with `int Id`, `int CompanyId`, `Company? Company`, `string ReferenceNumber`, `string Code`, `string Name`
  - `RoadSafety.Web.Data.User` with `int Id`, `string FullName`, `string ForceNumber`, `string Email`, `string PasswordHash`, `string BranchReferenceNumber`, `Branch? Branch`, `DateTime CreatedAt`
  - `RoadSafety.Web.Data.AppDbContext` with constructor `AppDbContext(DbContextOptions<AppDbContext> options)`, and `DbSet<User> Users`, `DbSet<Branch> Branches`, `DbSet<Company> Companies`
  - Two seeded companies (`CO-001`, `CO-002`) and four seeded branches (`BR-001` … `BR-004`)
  - Test helper `AuthServiceTests.CreateContext()` returning `AppDbContext` backed by open in-memory SQLite, with companies and branches already seeded

**Design notes.**

The hierarchy is **Company → Branch → User**. `User` deliberately carries no
company id: the company is reached through the branch, so the fact is stored
once and cannot disagree with itself.

`User.BranchReferenceNumber` is a foreign key onto `Branch.ReferenceNumber` —
an *alternate* key, not the primary key. That is what makes the officer row
carry the reference number itself rather than an opaque integer id. EF supports
it through `HasPrincipalKey`. `Branch.CompanyId` by contrast is a conventional
integer FK onto `Company.Id`, matching the Falcon convention.

- [ ] **Step 1: Add the EF Core packages**

```bash
dotnet add RoadSafety.Web package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.11
dotnet add RoadSafety.Web package Microsoft.EntityFrameworkCore.Design --version 10.0.11
dotnet add RoadSafety.Web package Microsoft.Extensions.Identity.Core --version 10.0.11
dotnet add RoadSafety.Tests package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.11
```

- [ ] **Step 2: Write the failing test**

First delete the placeholder test the template generated, so the final test
count is predictable:

```bash
rm RoadSafety.Tests/UnitTest1.cs
```

Then create `RoadSafety.Tests/AuthServiceTests.cs` with exactly this content:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Data;

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
```

Note the SQLite specifics: foreign keys are enforced only when the connection
has them enabled, which `Microsoft.Data.Sqlite` does by default, so the last
test is meaningful rather than vacuous.

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test
```
Expected: FAIL — compile error, `Company`, `Branch`, `User` and `AppDbContext`
do not exist.

- [ ] **Step 4: Create the Company entity**

`RoadSafety.Web/Data/Company.cs`:

```csharp
namespace RoadSafety.Web.Data;

/// <summary>
/// The organisation a branch belongs to. Sits at the top of the
/// Company -> Branch -> User hierarchy.
/// </summary>
public class Company
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
```

- [ ] **Step 5: Create the Branch entity**

`RoadSafety.Web/Data/Branch.cs`:

```csharp
namespace RoadSafety.Web.Data;

/// <summary>
/// A station belonging to a company. Officers reference a branch by its
/// ReferenceNumber rather than by its integer id.
/// </summary>
public class Branch
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}
```

- [ ] **Step 6: Create the User entity**

`RoadSafety.Web/Data/User.cs`:

```csharp
namespace RoadSafety.Web.Data;

/// <summary>
/// A police officer who can sign in. ForceNumber is the login identifier,
/// not the email address — this mirrors how officers are actually identified.
///
/// There is deliberately no CompanyId here: the company is reached through
/// the branch, so it is stored once and cannot contradict itself.
/// </summary>
public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ForceNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public string BranchReferenceNumber { get; set; } = string.Empty;
    public Branch? Branch { get; set; }

    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 7: Create the DbContext with seed data**

`RoadSafety.Web/Data/AppDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace RoadSafety.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.Property(c => c.ReferenceNumber).IsRequired().HasMaxLength(50);
            entity.Property(c => c.Code).IsRequired().HasMaxLength(50);
            entity.Property(c => c.Name).IsRequired().HasMaxLength(200);
            entity.Property(c => c.RegistrationNumber).HasMaxLength(100);

            entity.HasIndex(c => c.ReferenceNumber).IsUnique();
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.Property(b => b.ReferenceNumber).IsRequired().HasMaxLength(50);
            entity.Property(b => b.Code).IsRequired().HasMaxLength(50);
            entity.Property(b => b.Name).IsRequired().HasMaxLength(200);

            entity.HasIndex(b => b.ReferenceNumber).IsUnique();

            entity.HasOne(b => b.Company)
                  .WithMany(c => c.Branches)
                  .HasForeignKey(b => b.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.FullName).IsRequired();
            entity.Property(u => u.ForceNumber).IsRequired();
            entity.Property(u => u.Email).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.BranchReferenceNumber).IsRequired().HasMaxLength(50);

            entity.HasIndex(u => u.ForceNumber).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();

            // The foreign key targets Branch.ReferenceNumber, an alternate key,
            // rather than Branch.Id. HasPrincipalKey is what permits that.
            entity.HasOne(u => u.Branch)
                  .WithMany(b => b.Users)
                  .HasForeignKey(u => u.BranchReferenceNumber)
                  .HasPrincipalKey(b => b.ReferenceNumber)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        SeedLookups(modelBuilder);
    }

    /// <summary>
    /// Companies and branches are static reference data, so they are seeded
    /// through the model and land in the migration. The demo user cannot be
    /// seeded this way because its password hash is randomly salted.
    /// </summary>
    private static void SeedLookups(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>().HasData(
            new Company { Id = 1, ReferenceNumber = "CO-001", Code = "ZPS", Name = "Zambia Police Service", RegistrationNumber = "ZPS-1965" },
            new Company { Id = 2, ReferenceNumber = "CO-002", Code = "RTSA", Name = "Road Transport and Safety Agency", RegistrationNumber = "RTSA-2002" }
        );

        modelBuilder.Entity<Branch>().HasData(
            new Branch { Id = 1, CompanyId = 1, ReferenceNumber = "BR-001", Code = "KTW-CENTRAL", Name = "Kitwe Central" },
            new Branch { Id = 2, CompanyId = 1, ReferenceNumber = "BR-002", Code = "WUSAKILE",    Name = "Wusakile" },
            new Branch { Id = 3, CompanyId = 1, ReferenceNumber = "BR-003", Code = "CHAMBOLI",    Name = "Chamboli" },
            new Branch { Id = 4, CompanyId = 2, ReferenceNumber = "BR-004", Code = "RTSA-KTW",    Name = "RTSA Kitwe Station" }
        );
    }
}
```

- [ ] **Step 8: Register the DbContext and set the connection string**

In `RoadSafety.Web/appsettings.json`, add a `ConnectionStrings` section as a sibling of the existing `Logging` section:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=roadsafety.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

In `RoadSafety.Web/Program.cs`, add these `using` lines at the very top:

```csharp
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Data;
```

and add this immediately after the existing `builder.Services.AddRazorPages();` line:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
```

- [ ] **Step 9: Run the tests to verify they pass**

```bash
dotnet test
```
Expected: PASS, 4 tests.

- [ ] **Step 10: Create the migration**

```bash
dotnet ef migrations add InitialCreate --project RoadSafety.Web
```
Expected: a `RoadSafety.Web/Migrations/` folder appears, and the generated
migration contains `InsertData` calls for the two companies and four branches.

If this fails with a version-mismatch error, the global tool is older than the packages — run `dotnet tool update --global dotnet-ef --version 10.0.11` and retry.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "Add Company, Branch and User entities with initial migration

Replaces the PHP schema's free-text station column with a
Company -> Branch -> User hierarchy. Officers reference a branch by its
ReferenceNumber via an EF alternate key. Companies and branches are
static reference data and are seeded through the migration."
```

---

### Task 3: Registration in AuthService

**Files:**
- Create: `RoadSafety.Web/Services/AuthService.cs`
- Modify: `RoadSafety.Tests/AuthServiceTests.cs`

**Interfaces:**
- Consumes: `User`, `AppDbContext` from Task 2
- Produces:
  - `enum RoadSafety.Web.Services.RegistrationResult { Success, DuplicateForceNumber, DuplicateEmail, UnknownBranch }`
  - `class RoadSafety.Web.Services.AuthService` with constructor `AuthService(AppDbContext db, IPasswordHasher<User> hasher)`
  - `Task<RegistrationResult> RegisterAsync(string fullName, string forceNumber, string email, string password, string branchReferenceNumber)`

- [ ] **Step 1: Write the failing tests**

Add these four tests inside the existing `AuthServiceTests` class in `RoadSafety.Tests/AuthServiceTests.cs`, and add `using Microsoft.AspNetCore.Identity;` and `using RoadSafety.Web.Services;` to the top of that file:

```csharp
    private static AuthService CreateService(AppDbContext db) =>
        new(db, new PasswordHasher<User>());

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
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test
```
Expected: FAIL — compile error, `AuthService` and `RegistrationResult` do not exist.

- [ ] **Step 3: Write the implementation**

`RoadSafety.Web/Services/AuthService.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Data;

namespace RoadSafety.Web.Services;

public enum RegistrationResult
{
    Success,
    DuplicateForceNumber,
    DuplicateEmail,
    UnknownBranch
}

/// <summary>
/// All credential logic lives here and nowhere else. It deliberately knows
/// nothing about HTTP, which is what makes it testable without a web server.
/// </summary>
public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AuthService(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task<RegistrationResult> RegisterAsync(
        string fullName, string forceNumber, string email, string password, string branchReferenceNumber)
    {
        forceNumber = forceNumber.Trim();
        email = email.Trim();
        branchReferenceNumber = branchReferenceNumber.Trim();

        if (await _db.Users.AnyAsync(u => u.ForceNumber == forceNumber))
        {
            return RegistrationResult.DuplicateForceNumber;
        }

        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return RegistrationResult.DuplicateEmail;
        }

        // Checked explicitly so a bad branch produces a typed result rather
        // than a foreign-key exception surfacing from SaveChangesAsync.
        if (!await _db.Branches.AnyAsync(b => b.ReferenceNumber == branchReferenceNumber))
        {
            return RegistrationResult.UnknownBranch;
        }

        var user = new User
        {
            FullName = fullName.Trim(),
            ForceNumber = forceNumber,
            Email = email,
            BranchReferenceNumber = branchReferenceNumber,
            CreatedAt = DateTime.UtcNow
        };

        // Hash after the user exists, because PasswordHasher takes the user
        // as context. The plain password is never assigned to the entity.
        user.PasswordHash = _hasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return RegistrationResult.Success;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test
```
Expected: PASS, 8 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add registration to AuthService with hashed passwords

Duplicate force numbers, duplicate emails, and unknown branch references
are reported as typed results rather than surfaced as database
exceptions. Passwords are hashed with IPasswordHasher; the plain text is
never assigned to the entity."
```

---

### Task 4: Credential verification in AuthService

**Files:**
- Modify: `RoadSafety.Web/Services/AuthService.cs`, `RoadSafety.Tests/AuthServiceTests.cs`

**Interfaces:**
- Consumes: `AuthService`, `RegistrationResult` from Task 3
- Produces: `Task<User?> ValidateCredentialsAsync(string forceNumber, string password)` on `AuthService` — returns the `User` on success, `null` on any failure

- [ ] **Step 1: Write the failing tests**

Add these three tests inside `AuthServiceTests`:

```csharp
    [Fact]
    public async Task A_correct_password_authenticates()
    {
        using var db = CreateContext();
        var auth = CreateService(db);
        await auth.RegisterAsync("Grace Banda", "ZP-01234", "grace.banda@police.gov.zm", "Password123!", "kitwe_central");

        var user = await auth.ValidateCredentialsAsync("ZP-01234", "Password123!");

        Assert.NotNull(user);
        Assert.Equal("Grace Banda", user!.FullName);
    }

    [Fact]
    public async Task An_incorrect_password_does_not_authenticate()
    {
        using var db = CreateContext();
        var auth = CreateService(db);
        await auth.RegisterAsync("Grace Banda", "ZP-01234", "grace.banda@police.gov.zm", "Password123!", "kitwe_central");

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
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test
```
Expected: FAIL — compile error, `ValidateCredentialsAsync` does not exist.

- [ ] **Step 3: Write the implementation**

Add this method to `AuthService`, directly below `RegisterAsync`:

```csharp
    /// <summary>
    /// Returns the user when the credentials are valid, otherwise null.
    /// The caller cannot distinguish "no such officer" from "wrong password" —
    /// that is deliberate, so the login page cannot be used to discover
    /// which force numbers are registered.
    /// </summary>
    public async Task<User?> ValidateCredentialsAsync(string forceNumber, string password)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.ForceNumber == forceNumber.Trim());
        if (user is null)
        {
            return null;
        }

        var outcome = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);

        return outcome is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded
            ? user
            : null;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test
```
Expected: PASS, 8 tests.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Add credential verification to AuthService

Returns null for both an unknown force number and a wrong password so
the login page cannot be used to enumerate registered officers."
```

---

### Task 5: Cookie authentication wiring and the login page

**Files:**
- Modify: `RoadSafety.Web/Program.cs`
- Create: `RoadSafety.Web/Pages/Index.cshtml`, `RoadSafety.Web/Pages/Index.cshtml.cs` (replacing the template's versions)

**Interfaces:**
- Consumes: `AuthService.ValidateCredentialsAsync` from Task 4
- Produces: a registered cookie scheme with `LoginPath = "/"`; claims `ClaimTypes.NameIdentifier`, `ClaimTypes.Name`, `"ForceNumber"`, `"Station"`; DI registrations for `IPasswordHasher<User>` and `AuthService`

- [ ] **Step 1: Replace Program.cs entirely**

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Data;
using RoadSafety.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Order matters: authentication establishes who you are,
// authorization then decides what you may reach.
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

// Exposed so the integration tests in RoadSafety.Tests can boot this app
// via WebApplicationFactory<Program>.
public partial class Program { }
```

- [ ] **Step 2: Write the login page model**

Replace `RoadSafety.Web/Pages/Index.cshtml.cs` entirely:

```csharp
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RoadSafety.Web.Services;

namespace RoadSafety.Web.Pages;

public class IndexModel : PageModel
{
    private readonly AuthService _auth;

    public IndexModel(AuthService auth) => _auth = auth;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Force number is required")]
        [Display(Name = "Force Number")]
        public string ForceNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Dashboard");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _auth.ValidateCredentialsAsync(Input.ForceNumber, Input.Password);
        if (user is null)
        {
            // Deliberately generic — see AuthService.ValidateCredentialsAsync.
            ErrorMessage = "Invalid credentials";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("ForceNumber", user.ForceNumber),
            new("Station", user.Station)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToPage("/Dashboard");
    }
}
```

- [ ] **Step 3: Write the login view**

Replace `RoadSafety.Web/Pages/Index.cshtml` entirely:

```html
@page
@model RoadSafety.Web.Pages.IndexModel
@{
    ViewData["Title"] = "Sign In";
}

<div class="row justify-content-center mt-5">
    <div class="col-md-5">
        <h1 class="h3 mb-1">Road Accident Analysis</h1>
        <p class="text-muted mb-4">Officer sign in</p>

        @if (Model.ErrorMessage is not null)
        {
            <div class="alert alert-danger">@Model.ErrorMessage</div>
        }

        <form method="post">
            <div asp-validation-summary="ModelOnly" class="text-danger"></div>

            <div class="mb-3">
                <label asp-for="Input.ForceNumber" class="form-label"></label>
                <input asp-for="Input.ForceNumber" class="form-control" autocomplete="username" />
                <span asp-validation-for="Input.ForceNumber" class="text-danger"></span>
            </div>

            <div class="mb-3">
                <label asp-for="Input.Password" class="form-label"></label>
                <input asp-for="Input.Password" class="form-control" autocomplete="current-password" />
                <span asp-validation-for="Input.Password" class="text-danger"></span>
            </div>

            <button type="submit" class="btn btn-primary w-100">Sign In</button>
        </form>

        <p class="mt-3 text-center">
            <a asp-page="/Register">Create an account</a>
        </p>
    </div>
</div>
```

The `<form method="post">` tag helper emits an antiforgery token automatically, and Razor Pages validates it on POST. This is CSRF protection the PHP version did not have, and it costs nothing.

- [ ] **Step 4: Verify the app builds and the existing tests still pass**

```bash
dotnet build
dotnet test
```
Expected: build succeeds; 8 tests pass.

The app will not run successfully yet — `/Dashboard` and `/Register` do not exist, so the `asp-page` tag helpers will throw at render time. Tasks 6 and 7 create them.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Wire cookie authentication and add the login page

Uses the cookie scheme directly rather than ASP.NET Core Identity.
Razor Pages supplies antiforgery tokens automatically, which closes
the CSRF gap the PHP version had."
```

---

### Task 6: Registration page

**Files:**
- Create: `RoadSafety.Web/Pages/Register.cshtml`, `RoadSafety.Web/Pages/Register.cshtml.cs`

**Interfaces:**
- Consumes: `AuthService.RegisterAsync` and `RegistrationResult` from Task 3
- Produces: a `/Register` page reachable by `asp-page="/Register"`

- [ ] **Step 1: Write the page model**

`RoadSafety.Web/Pages/Register.cshtml.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RoadSafety.Web.Services;

namespace RoadSafety.Web.Pages;

public class RegisterModel : PageModel
{
    private readonly AuthService _auth;

    public RegisterModel(AuthService auth) => _auth = auth;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Force number is required")]
        [Display(Name = "Force Number")]
        public string ForceNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Station is required")]
        public string Station { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _auth.RegisterAsync(
            Input.FullName, Input.ForceNumber, Input.Email, Input.Password, Input.Station);

        switch (result)
        {
            case RegistrationResult.DuplicateForceNumber:
                ErrorMessage = "An account already exists for that force number.";
                return Page();

            case RegistrationResult.DuplicateEmail:
                ErrorMessage = "An account already exists for that email address.";
                return Page();

            default:
                return RedirectToPage("/Index");
        }
    }
}
```

The `[Required]`, `[EmailAddress]`, `[MinLength(8)]`, and `[Compare]` attributes are what the old `register.php` lacked entirely — it accepted any string as an email and any length of password.

- [ ] **Step 2: Write the view**

`RoadSafety.Web/Pages/Register.cshtml`:

```html
@page
@model RoadSafety.Web.Pages.RegisterModel
@{
    ViewData["Title"] = "Create Account";
}

<div class="row justify-content-center mt-5">
    <div class="col-md-6">
        <h1 class="h3 mb-4">Create Officer Account</h1>

        @if (Model.ErrorMessage is not null)
        {
            <div class="alert alert-danger">@Model.ErrorMessage</div>
        }

        <form method="post">
            <div asp-validation-summary="ModelOnly" class="text-danger"></div>

            <div class="mb-3">
                <label asp-for="Input.FullName" class="form-label"></label>
                <input asp-for="Input.FullName" class="form-control" />
                <span asp-validation-for="Input.FullName" class="text-danger"></span>
            </div>

            <div class="mb-3">
                <label asp-for="Input.ForceNumber" class="form-label"></label>
                <input asp-for="Input.ForceNumber" class="form-control" placeholder="ZP-00001" />
                <span asp-validation-for="Input.ForceNumber" class="text-danger"></span>
            </div>

            <div class="mb-3">
                <label asp-for="Input.Email" class="form-label"></label>
                <input asp-for="Input.Email" class="form-control" />
                <span asp-validation-for="Input.Email" class="text-danger"></span>
            </div>

            <div class="mb-3">
                <label asp-for="Input.Station" class="form-label"></label>
                <select asp-for="Input.Station" class="form-select">
                    <option value="">Select a station</option>
                    <option value="kitwe_central">Kitwe Central</option>
                    <option value="wusakile">Wusakile</option>
                    <option value="chamboli">Chamboli</option>
                    <option value="nkana_east">Nkana East</option>
                </select>
                <span asp-validation-for="Input.Station" class="text-danger"></span>
            </div>

            <div class="mb-3">
                <label asp-for="Input.Password" class="form-label"></label>
                <input asp-for="Input.Password" class="form-control" autocomplete="new-password" />
                <span asp-validation-for="Input.Password" class="text-danger"></span>
            </div>

            <div class="mb-3">
                <label asp-for="Input.ConfirmPassword" class="form-label"></label>
                <input asp-for="Input.ConfirmPassword" class="form-control" autocomplete="new-password" />
                <span asp-validation-for="Input.ConfirmPassword" class="text-danger"></span>
            </div>

            <button type="submit" class="btn btn-primary w-100">Create Account</button>
        </form>

        <p class="mt-3 text-center">
            <a asp-page="/Index">Back to sign in</a>
        </p>
    </div>
</div>
```

- [ ] **Step 3: Verify the build and tests**

```bash
dotnet build
dotnet test
```
Expected: build succeeds; 8 tests pass.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Add registration page with server-side validation

Enforces email format, an 8-character password minimum, and password
confirmation. The PHP version validated none of these."
```

---

### Task 7: Dashboard page, logout, and the anonymous-access test

**Files:**
- Create: `RoadSafety.Web/Pages/Dashboard.cshtml`, `RoadSafety.Web/Pages/Dashboard.cshtml.cs`
- Create: `RoadSafety.Web/Pages/Logout.cshtml`, `RoadSafety.Web/Pages/Logout.cshtml.cs`
- Create: `RoadSafety.Tests/TestWebAppFactory.cs`, `RoadSafety.Tests/DashboardAccessTests.cs`

**Interfaces:**
- Consumes: the cookie scheme and `Program` from Task 5
- Produces: `/Dashboard` (requires authentication), `/Logout` (POST signs out), and `TestWebAppFactory : WebApplicationFactory<Program>`

- [ ] **Step 1: Add the integration testing package**

```bash
dotnet add RoadSafety.Tests package Microsoft.AspNetCore.Mvc.Testing --version 10.0.11
```

- [ ] **Step 2: Write the test host factory**

`RoadSafety.Tests/TestWebAppFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoadSafety.Web.Data;

namespace RoadSafety.Tests;

/// <summary>
/// Boots the real application but swaps the SQLite file for an in-memory
/// database, so tests never touch roadsafety.db.
///
/// The schema is deliberately NOT created here. From Task 8 onward the
/// application itself runs migrations at startup, and calling EnsureCreated
/// as well would build the tables without a migration-history row — the
/// subsequent migration would then fail because the tables already exist.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
```

- [ ] **Step 3: Write the failing test**

`RoadSafety.Tests/DashboardAccessTests.cs`:

```csharp
using System.Net;

namespace RoadSafety.Tests;

public class DashboardAccessTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public DashboardAccessTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task An_anonymous_visitor_is_redirected_away_from_the_dashboard()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/", response.Headers.Location!.OriginalString);
        Assert.Contains("ReturnUrl", response.Headers.Location!.OriginalString);
    }
}
```

This is the security-critical test in phase 1 — it proves the dashboard is actually gated, rather than merely appearing to be.

- [ ] **Step 4: Run the test to verify it fails**

```bash
dotnet test
```
Expected: FAIL — `/Dashboard` does not exist, so the response is 404, not 302.

- [ ] **Step 5: Write the dashboard page**

`RoadSafety.Web/Pages/Dashboard.cshtml.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RoadSafety.Web.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    public string OfficerName => User.Identity?.Name ?? "Officer";
    public string Station => User.FindFirst("Station")?.Value ?? "Unknown";
}
```

`RoadSafety.Web/Pages/Dashboard.cshtml`:

```html
@page
@model RoadSafety.Web.Pages.DashboardModel
@{
    ViewData["Title"] = "Dashboard";
}

<div class="mt-5">
    <h1 class="h3">Welcome, @Model.OfficerName</h1>
    <p class="text-muted">Station: @Model.Station</p>

    <div class="alert alert-secondary mt-4">
        Dashboard content arrives in phase 2, once accident data is loaded.
    </div>

    <form method="post" asp-page="/Logout">
        <button type="submit" class="btn btn-outline-secondary">Log out</button>
    </form>
</div>
```

- [ ] **Step 6: Write the logout handler**

`RoadSafety.Web/Pages/Logout.cshtml`:

```html
@page
@model RoadSafety.Web.Pages.LogoutModel
```

`RoadSafety.Web/Pages/Logout.cshtml.cs`:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RoadSafety.Web.Pages;

public class LogoutModel : PageModel
{
    // GET on /Logout should not sign anyone out — a logout must be a
    // deliberate POST, or any <img src="/Logout"> on another site could
    // trigger it.
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

```bash
dotnet test
```
Expected: PASS, 9 tests.

If this test reports `307 TemporaryRedirect` instead of `302 Found`, the
redirect came from `UseHttpsRedirection` rather than from the auth challenge.
That means an HTTPS port leaked into the test host's configuration. Fix it by
moving `app.UseHttpsRedirection();` inside the existing
`if (!app.Environment.IsDevelopment())` block in `Program.cs` rather than by
changing the assertion — the assertion is testing the right thing.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Add authorized dashboard, logout, and access-control test

The dashboard is intentionally empty; it exists to prove the cookie
gates it. Logout is POST-only so a cross-site GET cannot trigger it."
```

---

### Task 8: Startup seeding, README, and manual verification

**Files:**
- Create: `RoadSafety.Web/Data/SeedData.cs`
- Modify: `RoadSafety.Web/Program.cs`, `README.md`

**Interfaces:**
- Consumes: `AppDbContext`, `AuthService` from Tasks 2–4
- Produces: `static Task SeedData.InitialiseAsync(IServiceProvider services)`

**Note on a deliberate deviation from the spec.** The spec said seeding would happen inside the migration. `PasswordHasher` generates a random salt per call, so EF's `HasData` would require a hard-coded hash string committed to the repository — worse, not better. Seeding therefore runs once at application startup and is a no-op if the officer already exists. This still fixes the actual defect in `db.php`, which re-seeded on *every database connection*.

- [ ] **Step 1: Write the seeder**

`RoadSafety.Web/Data/SeedData.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RoadSafety.Web.Services;

namespace RoadSafety.Web.Data;

public static class SeedData
{
    /// <summary>
    /// Applies pending migrations and inserts the demo officer if absent.
    /// Runs once per application start — not once per connection, which is
    /// what the old db.php did.
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
            "kitwe_central");
    }
}
```

- [ ] **Step 2: Call the seeder at startup**

In `RoadSafety.Web/Program.cs`, insert this line immediately after
`var app = builder.Build();`:

```csharp
await SeedData.InitialiseAsync(app.Services);
```

- [ ] **Step 3: Run the application and verify the full flow by hand**

```bash
dotnet run --project RoadSafety.Web
```

Open the URL printed in the console and confirm all six of these:

1. `/` shows the sign-in form.
2. Signing in as `ZP-00001` / `Password123!` lands on `/Dashboard` showing "Welcome, Test Officer".
3. Signing in with a wrong password shows `Invalid credentials` and nothing more specific.
4. Visiting `/Dashboard` in a private window redirects to the sign-in page.
5. `/Register` rejects a malformed email, a password under 8 characters, and mismatched passwords.
6. "Log out" returns to sign-in, and `/Dashboard` is unreachable again afterwards.

Stop the app with Ctrl+C.

- [ ] **Step 4: Rewrite the README**

Replace `README.md` entirely:

```markdown
# Road Accident Analysis Dashboard

A road safety dashboard for analysing accident data and identifying
high-risk locations, built with ASP.NET Core.

## Status

- **Phase 1 — Authentication:** complete
- **Phase 2 — Dashboard on real accident data:** not started
- **Phase 3 — Trained prediction model:** not started

## Requirements

- .NET 10 SDK
- Python 3.13 (phase 3 only, for model training)

## Running the application

```bash
dotnet run --project RoadSafety.Web
```

The SQLite database is created automatically on first run.

**Demo login:** force number `ZP-00001`, password `Password123!`

## Running the tests

```bash
dotnet test
```

## Project structure

| Path | Purpose |
|---|---|
| `RoadSafety.Web/` | ASP.NET Core Razor Pages application |
| `RoadSafety.Tests/` | xUnit unit and integration tests |
| `docs/superpowers/specs/` | Design documents |
| `docs/superpowers/plans/` | Implementation plans |

## Data source (phases 2 and 3)

Accident data comes from the UK Department for Transport's STATS19 road
safety dataset, which is published as open data.

Zambia does not publish incident-level open crash data. The analysis is
therefore demonstrated on UK data, and the import pipeline accepts any
dataset providing collision date, severity, weather, district, coordinates,
vehicle count, and casualty count.
```

- [ ] **Step 5: Run the full test suite one last time**

```bash
dotnet build
dotnet test
```
Expected: build succeeds with 0 warnings that mention `RoadSafety`; 9 tests pass.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Seed the demo officer at startup and rewrite the README

Seeding runs once per application start rather than on every database
connection as db.php did. README documents setup, the demo login, and
the provenance of the UK dataset used in later phases."
```

---

## Phase 1 completion criteria

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes with 9 tests
- [ ] An officer can register, sign in, reach `/Dashboard`, and sign out
- [ ] An anonymous visitor cannot reach `/Dashboard`
- [ ] No `.php` files and no `app.py` remain in the repository
- [ ] `database.sqlite` and `*.db` are gitignored and untracked
- [ ] `README.md` documents setup and the demo login

Phase 2 begins by downloading the STATS19 CSV and confirming its real column
names before any schema is written.
