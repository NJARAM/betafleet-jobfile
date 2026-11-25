using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BetaFleet.Api.Infrastructure;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "betafleet";
    public string Audience { get; set; } = "betafleet-web";
    public int Hours { get; set; } = 10;
}

public static class AuthSetup
{
    public const string TokenVersionClaim = "tv", MustChangeClaim = "pwd_change";

    public static void AddJwtAuth(this WebApplicationBuilder builder)
    {
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes. Set JWT_KEY (see .env.example).");
        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
        builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                NameClaimType = Claims.Name, RoleClaimType = Claims.Role, ClockSkew = TimeSpan.FromMinutes(1)
            };
            // A token dies as soon as the user is deactivated or their password changes.
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDb>();
                    var id = int.Parse(ctx.Principal!.FindFirst(Claims.UserId)!.Value);
                    var tv = ctx.Principal.FindFirst(TokenVersionClaim)?.Value;
                    var user = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => new { u.Active, u.TokenVersion }).FirstOrDefaultAsync();
                    if (user is null || !user.Active || tv != user.TokenVersion.ToString()) ctx.Fail("Session is no longer valid.");
                }
            };
        });
        builder.Services.AddAuthorization();
    }

    public static string IssueToken(User u, JwtOptions o)
    {
        var claims = new List<Claim>
        {
            new(Claims.UserId, u.Id.ToString()), new(Claims.Name, u.DisplayName), new(Claims.Role, u.Role),
            new(TokenVersionClaim, u.TokenVersion.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (u.CustomerId is { } c) claims.Add(new(Claims.CustomerId, c.ToString()));
        if (u.MustChangePassword) claims.Add(new(MustChangeClaim, "1"));
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, expires: DateTime.UtcNow.AddHours(o.Hours), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void ValidatePassword(string? pw)
    {
        if (pw is null || pw.Length < 10 || !pw.Any(char.IsLetter) || !pw.Any(char.IsDigit))
            throw new DomainException("Password must be at least 10 characters and include letters and digits.", 400);
    }

    /// Customers and drivers get a narrow slice of the API; everyone else uses role checks inside each endpoint.
    public static async ValueTask<object?> ScopeFilter(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var http = ctx.HttpContext;
        // A temporary password (new account or admin reset) only opens the change-password screen.
        if (http.User.FindFirst(MustChangeClaim) is not null && !(http.Request.Path.Value ?? "").StartsWith("/api/auth/"))
            return Results.Problem(title: "Change your temporary password first.", statusCode: 403);
        if (http.User.FindFirst(Claims.Role)?.Value == Roles.Driver)
        {
            var p = http.Request.Path.Value ?? "";
            if (!p.StartsWith("/api/auth/") && !p.StartsWith("/api/shipments"))
                return Results.Problem(title: "Driver accounts can only use the driver app.", statusCode: 403);
        }
        if (http.User.Identity?.IsAuthenticated == true && http.User.FindFirst(Claims.Role)?.Value == Roles.Customer)
        {
            var path = http.Request.Path.Value ?? "";
            var cid = http.User.FindFirst(Claims.CustomerId)?.Value;
            var allowed = path.StartsWith("/api/auth/")
                ||(path.StartsWith("/api/portal/") && path.Split('/') is [_, _, _, var seg] && seg == cid)
                || (path == "/api/portal/requests" && http.Request.Method == "POST");
            if (!allowed) return Results.Problem(title: "Customer accounts can only use the customer portal.", statusCode: 403);
        }
        return await next(ctx);
    }
}
