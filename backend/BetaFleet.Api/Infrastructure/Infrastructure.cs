using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Infrastructure;

/// A business rule was broken (409) or input was invalid (400). Message is shown to the user as-is.
public class DomainException(string message, int status = StatusCodes.Status409Conflict) : Exception(message)
{
    public int Status { get; } = status;
}

public static class Roles
{
    public const string Clerk = "clerk", OpsManager = "ops-manager", Finance = "finance", UnitHead = "unit-head",
        Workshop = "workshop", Storekeeper = "storekeeper", Driver = "driver", Customer = "customer", Admin = "admin";
    public static readonly string[] All = [Clerk, OpsManager, UnitHead, Finance, Workshop, Storekeeper, Driver, Customer, Admin];
}

public static class Claims
{
    public const string Name = "name", Role = "role", UserId = "sub", CustomerId = "customer_id";
}

/// Who is calling, taken from the signed JWT. Endpoints never trust anything the client says about identity.
public record Actor(int UserId, string Name, string Role, int? CustomerId)
{
    public static Actor From(HttpContext ctx)
    {
        var u = ctx.User;
        if (u.Identity?.IsAuthenticated != true) throw new DomainException("Sign in first.", StatusCodes.Status401Unauthorized);
        var cid = u.FindFirst(Claims.CustomerId)?.Value;
        return new Actor(int.Parse(u.FindFirst(Claims.UserId)!.Value), u.FindFirst(Claims.Name)!.Value, u.FindFirst(Claims.Role)!.Value,
            cid is null ? null : int.Parse(cid));
    }

    public void Require(params string[] roles)
    {
        if (Role == Roles.Admin || roles.Contains(Role)) return;
        throw new DomainException($"Your role ({Role}) cannot do this. Needs: {string.Join(" or ", roles)}.", StatusCodes.Status403Forbidden);
    }
}

public static class DbExtensions
{
    /// Hands out the next document number for a series, e.g. Next("FM", "FM-2026-", 4) → FM-2026-0312.
    public static async Task<string> NextNumber(this AppDb db, string series, string prefix, int pad)
    {
        var seq = await db.Sequences.FindAsync(series) ?? throw new InvalidOperationException($"Sequence {series} missing");
        var n = seq.Next++;
        return prefix + n.ToString().PadLeft(pad, '0');
    }

    public static void Log(this AppDb db, string entity, string id, Actor actor, string text) =>
        db.Activity.Add(new ActivityLog { Entity = entity, EntityId = id, At = DateTimeOffset.UtcNow, Actor = $"{actor.Name} ({actor.Role})", Text = text });

    public static Task<List<ActivityLog>> History(this AppDb db, string entity, string id) =>
        db.Activity.Where(a => a.Entity == entity && a.EntityId == id).OrderBy(a => a.At).ThenBy(a => a.Id).ToListAsync();

    public static void AddTimeline(this AppDb db, string jobId, string unit, string title, string detail = "", bool done = true) =>
        db.Timeline.Add(new TimelineEvent { JobId = jobId, Unit = unit, Title = title, Detail = detail, Done = done, Date = Clock.Today });

    public static async Task<T> Get<T>(this DbSet<T> set, object id, string label) where T : class =>
        await set.FindAsync(id) ?? throw new DomainException($"{label} {id} not found.", StatusCodes.Status404NotFound);
}

public static class Clock
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)); // East Africa Time
}

public static class ErrorHandling
{
    public static void UseDomainErrors(this WebApplication app) => app.UseExceptionHandler(e => e.Run(async ctx =>
    {
        var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, title) = ex switch
        {
            DomainException d => (d.Status, d.Message),
            DbUpdateConcurrencyException => (409, "Someone else changed this record. Reload and try again."),
            _ => (500, "Unexpected server error.")
        };
        if (status == 500) app.Logger.LogError(ex, "Unhandled error");
        ctx.Response.StatusCode = status;
        await Results.Problem(title: title, statusCode: status).ExecuteAsync(ctx);
    }));
}
