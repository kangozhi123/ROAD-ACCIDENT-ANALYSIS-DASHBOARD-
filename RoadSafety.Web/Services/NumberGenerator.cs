using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Helpers;

namespace RoadSafety.Web.Services;

public class NumberGenerator(AppDbContext db)
{
    public const string ForcePrefix = "ZP";
    public const string BranchPrefix = "BR";
    public const string CompanyPrefix = "CO";

    private const int ForceWidth = 5;
    private const int ReferenceWidth = 3;

    /// <summary>Next officer force number, e.g. ZP-00007.</summary>
    public async Task<string> NextForceNumberAsync(
        string prefix = ForcePrefix, CancellationToken ct = default)
    {
        var existing = await db.Users
            .Where(u => u.ForceNumber.StartsWith(prefix + "-"))
            .Select(u => u.ForceNumber)
            .ToListAsync(ct);

        return ReferenceNumber.Next(existing, prefix, ForceWidth);
    }

    /// <summary>Next station reference number, e.g. BR-005.</summary>
    public async Task<string> NextBranchReferenceAsync(CancellationToken ct = default)
    {
        var existing = await db.Branches
            .Where(b => b.ReferenceNumber.StartsWith(BranchPrefix + "-"))
            .Select(b => b.ReferenceNumber)
            .ToListAsync(ct);

        return ReferenceNumber.Next(existing, BranchPrefix, ReferenceWidth);
    }

    /// <summary>Next organisation reference number, e.g. CO-003.</summary>
    public async Task<string> NextCompanyReferenceAsync(CancellationToken ct = default)
    {
        var existing = await db.Companies
            .Where(c => c.ReferenceNumber.StartsWith(CompanyPrefix + "-"))
            .Select(c => c.ReferenceNumber)
            .ToListAsync(ct);

        return ReferenceNumber.Next(existing, CompanyPrefix, ReferenceWidth);
    }
}
