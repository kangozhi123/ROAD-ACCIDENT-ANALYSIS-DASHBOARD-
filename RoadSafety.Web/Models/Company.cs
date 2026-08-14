namespace RoadSafety.Web.Models;

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
