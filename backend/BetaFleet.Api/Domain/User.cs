namespace BetaFleet.Api.Domain;

public class User
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(15);

    public int Id { get; set; }
    public required string Username { get; set; }      // stored lower-case
    public required string DisplayName { get; set; }   // used on audit trails, e.g. "J. Kimaro"
    public required string Role { get; set; }
    public required string PasswordHash { get; set; }
    public int? CustomerId { get; set; }               // customer-portal users see only this customer
    public Customer? Customer { get; set; }
    public bool Active { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int FailedLogins { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// Bumped on password change, reset or deactivation; tokens carrying an older stamp are rejected.
    public int TokenVersion { get; set; }
}
