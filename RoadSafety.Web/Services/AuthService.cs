using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoadSafety.Web.Database;
using RoadSafety.Web.Models;

namespace RoadSafety.Web.Services;

public enum RegistrationResult
{
    Success,
    DuplicateForceNumber,
    DuplicateEmail,
    UnknownBranch
}

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
    string fullName,
    string forceNumber, 
    string email, 
    string password, 
    string branchReferenceNumber,
    int roleId = Role.OfficerId)
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
            RoleId = roleId,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _hasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return RegistrationResult.Success;
    }

    public async Task<User?> ValidateCredentialsAsync(string forceNumber, string password)
    {
        forceNumber = forceNumber.Trim();

        var user = await _db.Users
            .Include(u => u.Role)
            .Include(u => u.Branch)
                .ThenInclude(b => b!.Company)
            .SingleOrDefaultAsync(u => u.ForceNumber == forceNumber);

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
}
