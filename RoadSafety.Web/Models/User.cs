namespace RoadSafety.Web.Models;

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
