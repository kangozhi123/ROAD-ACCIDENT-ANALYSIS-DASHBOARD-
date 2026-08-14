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

        // Hashed after the user exists, because PasswordHasher takes the user
        // as context. The plain password is never assigned to the entity.
        user.PasswordHash = _hasher.HashPassword(user, password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return RegistrationResult.Success;
    }

    /// <summary>
    /// Returns the user when the credentials are valid, otherwise null.
    /// The caller cannot distinguish "no such officer" from "wrong password" —
    /// that is deliberate, so the login page cannot be used to discover
    /// which force numbers are registered.
    /// </summary>
    public async Task<User?> ValidateCredentialsAsync(string forceNumber, string password)
    {
        forceNumber = forceNumber.Trim();

        var user = await _db.Users
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
