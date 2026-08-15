namespace RoadSafety.Web.Models;

/// <summary>
/// What an officer is allowed to see and do.
///
/// The default is the narrowest: a new account sees only its own station and
/// changes nothing. Widening it is a deliberate act by an administrator.
/// </summary>
public enum UserRole
{
    /// <summary>Reads their own station's records. Cannot add or change officers.</summary>
    Officer = 0,

    /// <summary>Manages the officers posted to their own station, and no others.</summary>
    StationAdministrator = 1,

    /// <summary>Manages every station in every organisation.</summary>
    SystemAdministrator = 2
}
