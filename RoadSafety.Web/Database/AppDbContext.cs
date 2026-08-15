using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Models;

namespace RoadSafety.Web.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Role> Roles => Set<Role>();

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

            entity.HasOne(u => u.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(u => u.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(r => r.Name).IsRequired().HasMaxLength(60);
            entity.Property(r => r.Description).IsRequired().HasMaxLength(200);

            entity.HasIndex(r => r.Name).IsUnique();
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
        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = Role.OfficerId,
                Name = "Officer",
                Description = "Reads their own station's records. Cannot add or change officers.",
                IsBuiltIn = true
            },
            new Role
            {
                Id = Role.StationAdministratorId,
                Name = "Station administrator",
                Description = "Adds, edits and removes the officers posted to their own station.",
                CanManageOfficers = true,
                CanAssignRoles = true,
                IsBuiltIn = true
            },
            new Role
            {
                Id = Role.SystemAdministratorId,
                Name = "System administrator",
                Description = "Manages every station, and the roles themselves.",
                SeesEveryBranch = true,
                CanManageOfficers = true,
                CanAssignRoles = true,
                CanManageRoles = true,
                IsBuiltIn = true
            }
        );

        modelBuilder.Entity<Company>().HasData(
            new Company { Id = 1, ReferenceNumber = "CO-001", Code = "ZPS", Name = "Zambia Police Service", RegistrationNumber = "ZPS-1965" },
            new Company { Id = 2, ReferenceNumber = "CO-002", Code = "RTSA", Name = "Road Transport and Safety Agency", RegistrationNumber = "RTSA-2002" }
        );

        modelBuilder.Entity<Branch>().HasData(
            new Branch { Id = 1, CompanyId = 1, ReferenceNumber = "BR-001", Code = "KTW-CENTRAL", Name = "Kitwe Central" },
            new Branch { Id = 2, CompanyId = 1, ReferenceNumber = "BR-002", Code = "WUSAKILE", Name = "Wusakile" },
            new Branch { Id = 3, CompanyId = 1, ReferenceNumber = "BR-003", Code = "CHAMBOLI", Name = "Chamboli" },
            new Branch { Id = 4, CompanyId = 2, ReferenceNumber = "BR-004", Code = "RTSA-KTW", Name = "RTSA Kitwe Station" }
        );
    }
}
