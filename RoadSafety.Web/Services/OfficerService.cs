using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.ViewModels;

namespace RoadSafety.Web.Services;

public enum OfficerUpdateResult
{
    Success,
    NotFound,
    DuplicateEmail,
    UnknownBranch
}

public enum OfficerDeleteResult
{
    Success,
    NotFound,
    CannotDeleteSelf
}

/// <summary>
/// Reading, changing and removing officers. Registration stays in
/// <see cref="AuthService"/> because it owns password hashing; everything
/// else about an officer lives here.
/// </summary>
public class OfficerService(AppDbContext db)
{
    public async Task<List<OfficerRow>> ListAsync() =>
        await db.Users
            .Include(u => u.Branch)!.ThenInclude(b => b!.Company)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new OfficerRow(
                u.Id,
                u.FullName,
                u.ForceNumber,
                u.Email,
                u.Branch!.Name,
                u.Branch!.Company!.Name,
                u.CreatedAt))
            .ToListAsync();

    public async Task<OfficerDetail?> GetAsync(int id) =>
        await db.Users
            .Where(u => u.Id == id)
            .Include(u => u.Branch)!.ThenInclude(b => b!.Company)
            .Select(u => new OfficerDetail(
                u.Id,
                u.FullName,
                u.ForceNumber,
                u.Email,
                u.BranchReferenceNumber,
                u.Branch!.Name,
                u.Branch!.CompanyId,
                u.Branch!.Company!.Name,
                u.CreatedAt))
            .SingleOrDefaultAsync();

    /// <summary>
    /// Changes the details an administrator is allowed to change. The force
    /// number is deliberately not among them: officers sign in with it, so
    /// editing it would silently lock someone out of their own account.
    /// </summary>
    public async Task<OfficerUpdateResult> UpdateAsync(
        int id, string fullName, string email, string branchReferenceNumber)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return OfficerUpdateResult.NotFound;
        }

        email = email.Trim();
        branchReferenceNumber = branchReferenceNumber.Trim();

        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id))
        {
            return OfficerUpdateResult.DuplicateEmail;
        }

        if (!await db.Branches.AnyAsync(b => b.ReferenceNumber == branchReferenceNumber))
        {
            return OfficerUpdateResult.UnknownBranch;
        }

        user.FullName = fullName.Trim();
        user.Email = email;
        user.BranchReferenceNumber = branchReferenceNumber;

        await db.SaveChangesAsync();
        return OfficerUpdateResult.Success;
    }

    /// <param name="currentUserId">
    /// The signed-in officer, who may not remove their own account — doing so
    /// would sign them out mid-action and could leave nobody able to sign in.
    /// </param>
    public async Task<OfficerDeleteResult> DeleteAsync(int id, int currentUserId)
    {
        if (id == currentUserId)
        {
            return OfficerDeleteResult.CannotDeleteSelf;
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return OfficerDeleteResult.NotFound;
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return OfficerDeleteResult.Success;
    }
}
