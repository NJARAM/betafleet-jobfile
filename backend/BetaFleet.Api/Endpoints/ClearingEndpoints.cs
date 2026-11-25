using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using BetaFleet.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Endpoints;

public record AdvanceClearingRequest(string? EntryNo);
public record ContainerUpdateRequest(string Status, string? Depot, string? Note);
public record AddContainerRequest(string JobId, string ClearingId, string Number, string Size, DateOnly DischargedOn);

public static class ClearingEndpoints
{
    public static void MapClearing(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/clearing").WithTags("Clearing & Forwarding");
        g.MapGet("/", async (AppDb db) =>
        {
            var rows = await (from c in db.ClearingEntries
                              join j in db.Jobs on c.JobId equals j.Id
                              orderby c.Stage, c.Id descending
                              select new { c, Customer = j.Customer.Name }).ToListAsync();
            return rows.Select(r => new
            {
                r.c.Id, r.c.JobId, r.Customer, r.c.EntryNo, r.c.EntryType, r.c.Containers, r.c.Stage, StageName = ClearingEntry.Stages[r.c.Stage],
                r.c.NextUnit, r.c.HandedOver, Stages = ClearingEntry.Stages
            });
        });
        g.MapPost("/{id}/advance", Advance);

        var c = api.MapGroup("/containers").WithTags("Container tracking");
        c.MapGet("/", async (AppDb db) =>
        {
            var list = await db.Containers.Include(x => x.Events).OrderBy(x => x.JobId).ThenBy(x => x.Number).ToListAsync();
            return list.Select(x => ContainerRow(x));
        });
        c.MapPost("/", AddContainer);
        c.MapPut("/{id:int}", UpdateContainer);
    }

    public static object ContainerRow(Container x)
    {
        var end = x.ReturnedOn ?? Clock.Today;
        var days = end.DayNumber - x.DischargedOn.DayNumber;
        var detention = Math.Max(0, days - Container.FreeDays);
        return new
        {
            x.Id, x.JobId, x.ClearingId, x.Number, x.Size, x.Status, x.DischargedOn, x.ReturnedOn, x.Depot,
            DaysOut = days, FreeDays = Container.FreeDays, DetentionDays = detention,
            DetentionState = x.ReturnedOn is not null ? (detention > 0 ? "Returned late" : "Returned") : detention > 0 ? "In detention" : days >= Container.FreeDays - 3 ? "Free time ending" : "Within free time",
            Events = x.Events.OrderBy(e => e.Date).ThenBy(e => e.Id).Select(e => new { e.Date, e.Text })
        };
    }

    static async Task<IResult> Advance(AppDb db, HttpContext ctx, string id, AdvanceClearingRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk, Roles.UnitHead);
        var e = await db.ClearingEntries.Get(id, "Clearing entry");
        if (e.Stage >= ClearingEntry.Stages.Length - 1) throw new DomainException("This entry is already gated out.");
        if (e.Stage == 0)
        {
            if (string.IsNullOrWhiteSpace(r.EntryNo)) throw new DomainException("Enter the customs entry (TANSAD) number to lodge.", 400);
            e.EntryNo = r.EntryNo.Trim();
        }
        e.Stage++;
        var stage = ClearingEntry.Stages[e.Stage];
        db.AddTimeline(e.JobId, Units.Clearing, $"{e.EntryType} entry: {stage.ToLowerInvariant()}", $"{e.Id} · {e.EntryNo}");

        var job = await db.Jobs.Include(j => j.Legs).Include(j => j.Charges).FirstAsync(j => j.Id == e.JobId);
        var leg = job.Legs.FirstOrDefault(l => l.Unit == Units.Clearing);
        var entries = await db.ClearingEntries.Where(x => x.JobId == e.JobId).ToListAsync();
        if (leg is not null)
        {
            leg.ProgressPct = (int)Math.Round(entries.Average(x => x.Stage) / (ClearingEntry.Stages.Length - 1) * 100);
            leg.Status = entries.All(x => x.Stage >= ClearingEntry.ReleasedStage) ? "Completed" : stage;
            if (leg.Status == "Completed") leg.ProgressPct = 100;
            leg.Summary = $"{entries.Count(x => x.Stage >= ClearingEntry.ReleasedStage)} of {entries.Count} entries released";
        }

        // Release hands the cargo to the next unit automatically.
        if (e.Stage == ClearingEntry.ReleasedStage && !e.HandedOver)
        {
            e.HandedOver = true;
            if (e.NextUnit is "W" or "T" or "D")
            {
                db.Handoffs.Add(new Handoff
                {
                    Unit = e.NextUnit, JobId = e.JobId, FromUnit = Units.Clearing, CreatedAt = DateTimeOffset.UtcNow,
                    Title = e.NextUnit switch { "W" => $"Receive {e.Containers} into warehouse", "T" => $"Plan trucks for {e.Containers}", _ => $"Deliver {e.Containers}" },
                    Note = $"{e.EntryType} entry {e.EntryNo} released {Clock.Today:dd MMM}"
                });
                db.AddTimeline(e.JobId, Units.Clearing, $"Released → hand-over to {Units.Names[e.NextUnit]}", e.Id);
            }
        }
        JobRules.Recompute(job);
        db.Log("Clearing", id, actor, $"Stage → {stage}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> AddContainer(AppDb db, HttpContext ctx, AddContainerRequest r)
    {
        Actor.From(ctx).Require(Roles.Clerk, Roles.UnitHead);
        var no = r.Number.Replace(" ", "").ToUpperInvariant();
        if (!Iso6346.IsValid(no)) throw new DomainException($"{r.Number} is not a valid ISO 6346 container number (check digit).", 400);
        if (await db.Containers.AnyAsync(x => x.Number == no)) throw new DomainException($"{no} is already tracked.");
        var c = new Container { JobId = r.JobId, ClearingId = r.ClearingId, Number = no, Size = r.Size, Status = "At port", DischargedOn = r.DischargedOn };
        c.Events.Add(new ContainerEvent { Date = r.DischargedOn, Text = "Discharged at Dar es Salaam port" });
        db.Containers.Add(c);
        await db.SaveChangesAsync();
        return Results.Created($"/api/containers/{c.Id}", ContainerRow(c));
    }

    static async Task<IResult> UpdateContainer(AppDb db, HttpContext ctx, int id, ContainerUpdateRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk, Roles.UnitHead);
        var c = await db.Containers.Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Container not found", 404);
        if (c.ReturnedOn is not null) throw new DomainException("Empty already returned; the container is closed.");
        c.Status = r.Status;
        if (r.Status == "Empty returned")
        {
            if (string.IsNullOrWhiteSpace(r.Depot)) throw new DomainException("Pick the empty depot.", 400);
            c.ReturnedOn = Clock.Today; c.Depot = r.Depot;
        }
        c.Events.Add(new ContainerEvent { Date = Clock.Today, Text = r.Status + (r.Depot is { Length: > 0 } ? $" · {r.Depot}" : "") + (r.Note is { Length: > 0 } ? $" · {r.Note}" : "") });
        db.Log("Container", c.Number, actor, $"Status → {r.Status}");
        await db.SaveChangesAsync();
        return Results.Ok(ContainerRow(c));
    }
}

public static class Iso6346
{
    /// Validates the check digit of an ISO 6346 container number (4 letters + 6 digits + check digit).
    public static bool IsValid(string no)
    {
        if (no.Length != 11 || !no[..4].All(char.IsAsciiLetterUpper) || !no[4..].All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 10; i++) sum += Value(no[i]) * (1 << i);
        return sum % 11 % 10 == no[10] - '0';
    }

    public static int CheckDigit(string first10)
    {
        var sum = 0;
        for (var i = 0; i < 10; i++) sum += Value(first10[i]) * (1 << i);
        return sum % 11 % 10;
    }

    static int Value(char ch)
    {
        if (char.IsAsciiDigit(ch)) return ch - '0';
        var v = 10;
        for (var c = 'A'; c < ch; c++) { v++; if (v % 11 == 0) v++; }
        return v;
    }
}
