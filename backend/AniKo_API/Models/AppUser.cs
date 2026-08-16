namespace AniKo_API.Models;

/// <summary>
/// Which side of a trade a person is on. Persisted as its name, not its ordinal —
/// see <c>AniKoDbContext.OnModelCreating</c> for why.
/// Mirrors <c>UserRole</c> in <c>frontend/src/lib/session.ts</c>, which this eventually replaces.
/// </summary>
public enum UserRole
{
    Buyer,
    Farmer,
}

/// <summary>
/// A person using the marketplace. The dashboard shows the name, the verified tick
/// and the avatar in the header, and nothing else about them — so nothing else is stored.
/// </summary>
public class AppUser
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public UserRole Role { get; set; }

    /// <summary>Drives the "Verified account" line under the name in the header.</summary>
    public bool Verified { get; set; }

    /// <summary>
    /// Nullable because the frontend already renders initials when it is absent
    /// (<c>session.ts</c> ships <c>avatarUrl: null</c>). An empty string would make
    /// "no avatar" indistinguishable from "a broken avatar URL".
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>UTC. Formatting to Asia/Manila belongs to the frontend.</summary>
    public DateTime CreatedAt { get; set; }
}
