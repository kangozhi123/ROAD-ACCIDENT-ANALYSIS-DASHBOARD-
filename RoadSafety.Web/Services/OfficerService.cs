using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Services;

public enum OfficerUpdateResult
{
    Success,
    NotFound,
    DuplicateEmail,
    UnknownBranch,
    Forbidden
}

public enum OfficerDeleteResult
{
    Success,
    NotFound,
    CannotDeleteSelf,
    Forbidden
}

/// <summary>
/// Reading, changing and removing officers. Registration stays in
/// <see cref="AuthService"/> because it owns password hashing; everything
/// else about an officer lives here.
///
/// Every method takes an <see cref="AccessScope"/> and applies it itself. The
/// pages hide what a role cannot use, but hiding a button is presentation —
/// this class is what actually stops a request that reaches past its station.
/// </summary>
public class OfficerService(AppDbContext db)
{
    public async Task<List<OfficerRow>> ListAsync(AccessScope scope) =>
        await Visible(scope)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new OfficerRow(
                u.Id,
                u.FullName,
                u.ForceNumber,
                u.Email,
                u.Branch!.Name,
                u.Branch!.Company!.Name,
                u.Role,
                u.CreatedAt))
            .ToListAsync();

    /// <summary>
    /// Returns null both when the officer does not exist and when they sit
    /// outside the caller's station. The caller cannot tell the two apart, so
    /// the endpoint cannot be used to discover who works elsewhere.
    /// </summary>
    public async Task<OfficerDetail?> GetAsync(int id, AccessScope scope) =>
        await Visible(scope)
            .Where(u => u.Id == id)
            .Select(u => new OfficerDetail(
                u.Id,
                u.FullName,
                u.ForceNumber,
                u.Email,
                u.BranchReferenceNumber,
                u.Branch!.Name,
                u.Branch!.CompanyId,
                u.Branch!.Company!.Name,
                u.Role,
                u.CreatedAt))
            .SingleOrDefaultAsync();

    /// <summary>
    /// Changes the details an administrator is allowed to change. The force
    /// number is deliberately not among them: officers sign in with it, so
    /// editing it would silently lock someone out of their own account.
    /// </summary>
    public async Task<OfficerUpdateResult> UpdateAsync(
        int id, string fullName, string email, string branchReferenceNumber, AccessScope scope)
    {
        if (!scope.CanManageOfficers)
        {
            return OfficerUpdateResult.Forbidden;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return OfficerUpdateResult.NotFound;
        }

        // Reported as not found rather than forbidden: confirming that an
        // officer exists elsewhere is itself something to withhold.
        if (!scope.Covers(user.BranchReferenceNumber))
        {
            return OfficerUpdateResult.NotFound;
        }

        email = email.Trim();

        // A station administrator cannot post someone to another station, so
        // whatever branch arrived is resolved against their own reach.
        var branch = scope.ResolveBranch(branchReferenceNumber);

        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id))
        {
            return OfficerUpdateResult.DuplicateEmail;
        }

        if (!await db.Branches.AnyAsync(b => b.ReferenceNumber == branch))
        {
            return OfficerUpdateResult.UnknownBranch;
        }

        user.FullName = fullName.Trim();
        user.Email = email;
        user.BranchReferenceNumber = branch;

        await db.SaveChangesAsync();
        return OfficerUpdateResult.Success;
    }

    public async Task<OfficerDeleteResult> DeleteAsync(int id, AccessScope scope)
    {
        if (!scope.CanManageOfficers)
        {
            return OfficerDeleteResult.Forbidden;
        }

        // Removing your own account would sign you out mid-action, and could
        // leave a station with nobody able to administer it.
        if (id == scope.UserId)
        {
            return OfficerDeleteResult.CannotDeleteSelf;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user is null || !scope.Covers(user.BranchReferenceNumber))
        {
            return OfficerDeleteResult.NotFound;
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return OfficerDeleteResult.Success;
    }

    /// <summary>
    /// The officers this caller may see: everyone for a system administrator,
    /// otherwise only those posted to the caller's own station.
    /// </summary>
    private IQueryable<User> Visible(AccessScope scope)
    {
        var query = db.Users
            .Include(u => u.Branch)!.ThenInclude(b => b!.Company)
            .AsQueryable();

        return scope.SeesEveryBranch
            ? query
            : query.Where(u => u.BranchReferenceNumber == scope.BranchReferenceNumber);
    }
}
