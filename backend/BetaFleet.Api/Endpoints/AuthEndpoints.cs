using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using BetaFleet.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BetaFleet.Api.Endpoints;

public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record CreateUserRequest(string Username, string DisplayName, string Role, int? CustomerId, string Password);
public record UpdateUserRequest(string? DisplayName, string? Role, int? CustomerId, bool? Active, string? NewPassword);

public static class AuthEndpoints
{
    static object Me(User u) => new { u.Id, u.Username, u.DisplayName, u.Role, u.CustomerId, Customer = u.Customer?.Name, u.MustChangePassword };

    public static void MapAuth(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Authentication");
        auth.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("login");
        auth.MapGet("/me", async (AppDb db, HttpContext ctx) =>
        {
            var a = Actor.From(ctx);
            var u = await db.Users.Include(x => x.Customer).FirstAsync(x => x.Id == a.UserId);
            return Me(u);
        });
        auth.MapPost("/change-password", ChangePassword);

        var users = api.MapGroup("/users").WithTags("Users");
        users.MapGet("/", async (AppDb db, HttpContext ctx) =>
        {
            Actor.From(ctx).Require(Roles.Admin);
            return await db.Users.Include(u => u.Customer).OrderBy(u => u.Role).ThenBy(u => u.Username)
                .Select(u => new { u.Id, u.Username, u.DisplayName, u.Role, u.CustomerId, Customer = u.Customer != null ? u.Customer.Name : null, u.Active, u.MustChangePassword, u.LastLoginAt, Locked = u.LockedUntil > DateTimeOffset.UtcNow })
                .ToListAsync();
        });
        users.MapPost("/", CreateUser);
        users.MapPut("/{id:int}", UpdateUser);
    }

    static async Task<IResult> Login(AppDb db, IPasswordHasher<User> hasher, IOptions<JwtOptions> jwt, LoginRequest r)
    {
        const string bad = "Wrong username or password.";
        var name = (r.Username ?? "").Trim().ToLowerInvariant();
        var u = await db.Users.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Username == name);
        if (u is null) { hasher.HashPassword(null!, r.Password ?? ""); return Results.Problem(title: bad, statusCode: 401); } // same cost as a real check
        if (!u.Active) return Results.Problem(title: "This account is disabled. Ask an administrator.", statusCode: 401);
        if (u.LockedUntil > DateTimeOffset.UtcNow) return Results.Problem(title: $"Too many failed attempts. Try again after {u.LockedUntil.Value.ToOffset(TimeSpan.FromHours(3)):HH:mm} EAT.", statusCode: 423);

        var result = hasher.VerifyHashedPassword(u, u.PasswordHash, r.Password ?? "");
        if (result == PasswordVerificationResult.Failed)
        {
            u.FailedLogins++;
            if (u.FailedLogins >= User.MaxFailedLogins) { u.LockedUntil = DateTimeOffset.UtcNow.Add(User.LockoutTime); u.FailedLogins = 0; }
            await db.SaveChangesAsync();
            return Results.Problem(title: bad, statusCode: 401);
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded) u.PasswordHash = hasher.HashPassword(u, r.Password!);
        u.FailedLogins = 0; u.LockedUntil = null; u.LastLoginAt = DateTimeOffset.UtcNow;
        db.Activity.Add(new ActivityLog { Entity = "User", EntityId = u.Username, At = DateTimeOffset.UtcNow, Actor = $"{u.DisplayName} ({u.Role})", Text = "Signed in" });
        await db.SaveChangesAsync();
        return Results.Ok(new { Token = AuthSetup.IssueToken(u, jwt.Value), User = Me(u) });
    }

    static async Task<IResult> ChangePassword(AppDb db, HttpContext ctx, IPasswordHasher<User> hasher, IOptions<JwtOptions> jwt, ChangePasswordRequest r)
    {
        var a = Actor.From(ctx);
        var u = await db.Users.Include(x => x.Customer).FirstAsync(x => x.Id == a.UserId);
        if (hasher.VerifyHashedPassword(u, u.PasswordHash, r.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
            throw new DomainException("Current password is wrong.", 400);
        AuthSetup.ValidatePassword(r.NewPassword);
        if (r.NewPassword == r.CurrentPassword) throw new DomainException("Choose a password you haven't used here.", 400);
        u.PasswordHash = hasher.HashPassword(u, r.NewPassword); u.MustChangePassword = false; u.TokenVersion++;
        db.Log("User", u.Username, a, "Changed own password");
        await db.SaveChangesAsync();
        // Other sessions are signed out; this one gets a fresh token.
        return Results.Ok(new { Token = AuthSetup.IssueToken(u, jwt.Value), User = Me(u) });
    }

    static void CheckRole(string role, int? customerId)
    {
        if (!Roles.All.Contains(role)) throw new DomainException("Unknown role.", 400);
        if (role == Roles.Customer && customerId is null) throw new DomainException("A customer account must be linked to a customer.", 400);
    }

    static async Task<IResult> CreateUser(AppDb db, HttpContext ctx, IPasswordHasher<User> hasher, CreateUserRequest r)
    {
        var a = Actor.From(ctx); a.Require(Roles.Admin);
        var name = (r.Username ?? "").Trim().ToLowerInvariant();
        if (name.Length < 3 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')) throw new DomainException("Username: 3+ characters, letters, digits, . - _ only.", 400);
        if (string.IsNullOrWhiteSpace(r.DisplayName)) throw new DomainException("Display name is required.", 400);
        CheckRole(r.Role, r.CustomerId);
        AuthSetup.ValidatePassword(r.Password);
        if (await db.Users.AnyAsync(u => u.Username == name)) throw new DomainException($"Username {name} is taken.");
        var u = new User
        {
            Username = name, DisplayName = r.DisplayName.Trim(), Role = r.Role, CustomerId = r.Role == Roles.Customer ? r.CustomerId : null,
            PasswordHash = "", MustChangePassword = true, CreatedAt = DateTimeOffset.UtcNow
        };
        u.PasswordHash = hasher.HashPassword(u, r.Password);
        db.Users.Add(u);
        db.Log("User", name, a, $"Created as {r.Role}");
        await db.SaveChangesAsync();
        return Results.Created($"/api/users/{u.Id}", new { u.Id });
    }

    static async Task<IResult> UpdateUser(AppDb db, HttpContext ctx, IPasswordHasher<User> hasher, int id, UpdateUserRequest r)
    {
        var a = Actor.From(ctx); a.Require(Roles.Admin);
        var u = await db.Users.Get(id, "User");
        if (u.Id == a.UserId && (r.Active == false || (r.Role is not null && r.Role != Roles.Admin)))
            throw new DomainException("You cannot disable or demote your own account.");
        var changes = new List<string>();
        if (r.DisplayName is { Length: > 0 } dn && dn != u.DisplayName) { u.DisplayName = dn.Trim(); changes.Add("name"); }
        if (r.Role is not null && (r.Role != u.Role || r.CustomerId != u.CustomerId))
        {
            CheckRole(r.Role, r.CustomerId);
            u.Role = r.Role; u.CustomerId = r.Role == Roles.Customer ? r.CustomerId : null; u.TokenVersion++; changes.Add($"role → {r.Role}");
        }
        if (r.Active is { } act && act != u.Active) { u.Active = act; u.TokenVersion++; changes.Add(act ? "enabled" : "disabled"); }
        if (r.NewPassword is { Length: > 0 } pw)
        {
            AuthSetup.ValidatePassword(pw);
            u.PasswordHash = hasher.HashPassword(u, pw); u.MustChangePassword = true; u.TokenVersion++; u.FailedLogins = 0; u.LockedUntil = null; changes.Add("password reset");
        }
        if (changes.Count > 0) db.Log("User", u.Username, a, string.Join(", ", changes));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
