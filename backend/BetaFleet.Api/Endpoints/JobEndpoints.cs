using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using BetaFleet.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Endpoints;

public record CreateJobRequest(
    int CustomerId, string Direction, string Reference, string Cargo, string CargoType, string? Quotation, string? Eta,
    string[] Units, string? Entry, string? Warehouse, int? StorageDays, int? Drops, string? Route, int? Trucks);

public record UpdateLegRequest(string Status, int ProgressPct, string? Summary);

public static class JobEndpoints
{
    public static void MapJobs(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", Dashboard);

        var jobs = api.MapGroup("/jobs").WithTags("Job files");
        jobs.MapGet("/", async (AppDb db, string? unit, string? q) =>
        {
            var query = db.Jobs.Include(j => j.Customer).AsQueryable();
            if (!string.IsNullOrEmpty(unit)) query = query.Where(j => j.Units.Contains(unit));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = $"%{q.Trim()}%";
                query = query.Where(j => EF.Functions.ILike(j.Id + " " + j.Customer.Name + " " + j.Reference + " " + j.Cargo, s));
            }
            return await query.OrderByDescending(j => j.Id).Select(j => JobRow(j)).ToListAsync();
        });
        jobs.MapGet("/{id}", JobDetail);
        jobs.MapPost("/", CreateJob);
        jobs.MapPost("/{id}/units/{unit}", AddUnit);
        jobs.MapPut("/{id}/legs/{unit}", UpdateLeg);

        var ho = api.MapGroup("/handoffs").WithTags("Hand-over queue");
        ho.MapGet("/", async (AppDb db, string? unit) =>
            await db.Handoffs.Where(h => unit == null || h.Unit == unit).OrderBy(h => h.State == "Waiting" ? 0 : 1).ThenByDescending(h => h.CreatedAt).ToListAsync());
        ho.MapPost("/{id:int}/accept", AcceptHandoff);
    }

    static object JobRow(Job j) => new
    {
        j.Id, Customer = j.Customer.Name, j.CustomerId, j.Reference, j.Cargo, j.CargoType, j.Direction, Units = j.Units.Select(c => c.ToString()),
        j.Stage, j.ProgressPct, j.Owner, j.OpenedOn, j.Eta, j.Alert
    };

    static async Task<object> Dashboard(AppDb db)
    {
        var jobs = await db.Jobs.Include(j => j.Customer).ToListAsync();
        var open = jobs.Where(j => j.Stage != "Closed").ToList();
        var docs = await db.ComplianceDocs.ToListAsync();
        var ready = await db.Charges.Where(c => c.Status == "Ready" && c.InvoiceNo == null).SumAsync(c => c.Kind == ChargeKind.Disbursement ? c.Cost : c.Revenue);
        var closedCycle = jobs.Where(j => j.Stage == "Closed").ToList();
        return new
        {
            Stages = new[] { "Customs", "Warehouse", "Distribution", "In transit", "Billing" }.Select(s => new { Stage = s, Count = jobs.Count(j => j.Stage == s) }),
            OpenJobs = open.Count,
            Exceptions = jobs.Where(j => j.Alert != "").Select(j => new { j.Id, Customer = j.Customer.Name, j.Alert }),
            HandoffsWaiting = await db.Handoffs.CountAsync(h => h.State == "Waiting"),
            ReadyToInvoice = ready,
            FmPending = await db.FillingMasters.CountAsync(f => f.Status == FmStatus.Pending),
            FmApproved = await db.FillingMasters.CountAsync(f => f.Status == FmStatus.Approved || f.Status == FmStatus.Disbursed),
            ExpiredDocs = docs.Count(FleetRules.Expired),
            ExpensesPending = await db.Expenses.CountAsync(e => e.Status == ExpStatus.PendingHead || e.Status == ExpStatus.PendingFinance),
            ExpensesToPay = await db.Expenses.CountAsync(e => e.Status == ExpStatus.Approved),
            OpenJobCards = await db.JobCards.CountAsync(j => !j.Closed),
            ActiveJobs = open.OrderByDescending(j => j.Id).Take(6).Select(JobRow),
            Alerts = await db.TrackingAlerts.OrderByDescending(a => a.At).Take(4).ToListAsync(),
            Handoffs = await db.Handoffs.Where(h => h.State == "Waiting").OrderByDescending(h => h.CreatedAt).Take(5).ToListAsync()
        };
    }

    static async Task<IResult> JobDetail(AppDb db, string id)
    {
        var j = await db.Jobs.Include(x => x.Customer).Include(x => x.Legs).Include(x => x.Documents).Include(x => x.Charges)
            .Include(x => x.Timeline).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id);
        if (j is null) return Results.NotFound();
        var expenses = await db.Expenses.Where(e => e.JobId == id).OrderByDescending(e => e.Date).ToListAsync();
        var revenue = j.Charges.Where(c => c.Kind == ChargeKind.Revenue).Sum(c => c.Revenue);
        var cost = j.Charges.Where(c => c.Kind == ChargeKind.Revenue).Sum(c => c.Cost)
                   + expenses.Where(e => !e.Rechargeable && e.Status is ExpStatus.Approved or ExpStatus.Paid).Sum(e => e.Amount);
        return Results.Ok(new
        {
            Job = JobRow(j),
            Legs = Units.Ordered.Select(u => (object?)j.Legs.FirstOrDefault(l => l.Unit == u) ?? new { Unit = u, Off = true }),
            Timeline = j.Timeline.OrderBy(t => t.Done ? 0 : 1).ThenBy(t => t.Date).ThenBy(t => t.Id),
            Documents = j.Documents.OrderBy(d => Array.IndexOf(Units.Ordered, d.Unit)),
            Charges = j.Charges.OrderBy(c => Array.IndexOf(Units.Ordered, c.Unit)),
            Expenses = expenses,
            Clearing = await db.ClearingEntries.Where(c => c.JobId == id).ToListAsync(),
            Containers = await db.Containers.Where(c => c.JobId == id).OrderBy(c => c.Number).ToListAsync(),
            Shipments = await db.ShipmentOrders.Where(s => s.JobId == id).ToListAsync(),
            Handoffs = await db.Handoffs.Where(h => h.JobId == id).OrderByDescending(h => h.CreatedAt).ToListAsync(),
            Profit = new { Revenue = revenue, Cost = cost, Margin = revenue - cost, MarginPct = revenue == 0 ? 0 : Math.Round((revenue - cost) / revenue * 100, 1) }
        });
    }

    static async Task<IResult> CreateJob(AppDb db, HttpContext ctx, CreateJobRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk, Roles.OpsManager);
        var units = Units.Ordered.Where(u => r.Units.Contains(u)).ToArray();
        if (units.Length == 0) throw new DomainException("Tick at least one business unit.", 400);
        if (string.IsNullOrWhiteSpace(r.Reference) || string.IsNullOrWhiteSpace(r.Cargo)) throw new DomainException("Reference and cargo are required.", 400);
        var customer = await db.Customers.Get(r.CustomerId, "Customer");

        var id = await db.NextNumber("JOB", $"JOB-{Clock.Today.Year}-", 5);
        var job = new Job
        {
            Id = id, CustomerId = customer.Id, Reference = r.Reference.Trim(), Cargo = r.Cargo.Trim(), CargoType = r.CargoType, Direction = r.Direction,
            Units = string.Concat(units), Owner = actor.Name, OpenedOn = Clock.Today, Eta = r.Eta ?? "", Quotation = r.Quotation
        };
        db.Jobs.Add(job);
        foreach (var u in units)
        {
            var legNo = await db.NextNumber("LEG-" + u, $"{JobRules.LegPrefix(u)}-{Clock.Today.Year}-", 4);
            job.Legs.Add(new JobLeg { JobId = id, Unit = u, Number = legNo, Status = "Hand-over sent", Summary = JobRules.FirstTask(u, r.Route ?? "", r.Trucks ?? 1, r.Warehouse ?? "", r.Drops ?? 1, r.Entry ?? "") });
            db.Handoffs.Add(new Handoff
            {
                Unit = u, JobId = id, FromUnit = Units.File, CreatedAt = DateTimeOffset.UtcNow,
                Title = JobRules.FirstTask(u, r.Route ?? "", r.Trucks ?? 1, r.Warehouse ?? "", r.Drops ?? 1, r.Entry ?? ""),
                Note = $"New file {id} · {customer.Name}" + (r.Quotation is { Length: > 0 } q ? $" · from {q}" : "")
            });
            if (u == Units.Clearing)
                db.ClearingEntries.Add(new ClearingEntry
                {
                    Id = legNo, JobId = id, EntryType = r.Entry ?? "Home consumption",
                    Containers = r.Cargo, NextUnit = units.Contains(Units.Warehousing) ? "W" : units.Contains(Units.Transit) ? "T" : units.Contains(Units.Distribution) ? "D" : "X"
                });
        }
        db.AddTimeline(id, Units.File, "File opened" + (r.Quotation is { Length: > 0 } qq ? $" from quotation {qq}" : ""), $"{customer.Name} · {r.Cargo} · units {string.Join(", ", units)}");
        db.Log("Job", id, actor, "Job file opened");
        await db.SaveChangesAsync();
        return Results.Created($"/api/jobs/{id}", new { id });
    }

    static async Task<IResult> AddUnit(AppDb db, HttpContext ctx, string id, string unit)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk, Roles.OpsManager);
        if (!Units.Ordered.Contains(unit)) throw new DomainException("Unknown unit.", 400);
        var job = await db.Jobs.Include(j => j.Legs).Include(j => j.Charges).FirstOrDefaultAsync(j => j.Id == id) ?? throw new DomainException("Job not found", 404);
        if (job.Units.Contains(unit)) throw new DomainException($"{Units.Names[unit]} is already on this file.");
        if (job.Stage == "Closed") throw new DomainException("A closed file cannot take new units.");
        job.Units = string.Concat(Units.Ordered.Where(u => job.Units.Contains(u) || u == unit));
        job.Legs.Add(new JobLeg { JobId = id, Unit = unit, Number = await db.NextNumber("LEG-" + unit, $"{JobRules.LegPrefix(unit)}-{Clock.Today.Year}-", 4), Status = "Hand-over sent" });
        db.Handoffs.Add(new Handoff { Unit = unit, JobId = id, FromUnit = Units.File, Title = $"{Units.Names[unit]} added to file", Note = $"Added by {actor.Name}", CreatedAt = DateTimeOffset.UtcNow });
        db.AddTimeline(id, unit, $"{Units.Names[unit]} added to file", $"Hand-over sent · {actor.Name}");
        JobRules.Recompute(job);
        db.Log("Job", id, actor, $"Added unit {unit}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> UpdateLeg(AppDb db, HttpContext ctx, string id, string unit, UpdateLegRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk, Roles.OpsManager, Roles.UnitHead);
        var job = await db.Jobs.Include(j => j.Legs).Include(j => j.Charges).FirstOrDefaultAsync(j => j.Id == id) ?? throw new DomainException("Job not found", 404);
        var leg = job.Legs.FirstOrDefault(l => l.Unit == unit) ?? throw new DomainException("This unit is not on the file.", 404);
        leg.Status = r.Status; leg.ProgressPct = Math.Clamp(r.ProgressPct, 0, 100);
        if (r.Summary is not null) leg.Summary = r.Summary;
        if (leg.ProgressPct == 100) db.AddTimeline(id, unit, $"{Units.Names[unit]} leg completed", leg.Summary);
        JobRules.Recompute(job);
        db.Log("Job", id, actor, $"Leg {unit} → {r.Status} ({leg.ProgressPct}%)");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> AcceptHandoff(AppDb db, HttpContext ctx, int id)
    {
        var actor = Actor.From(ctx);
        var h = await db.Handoffs.Get(id, "Hand-over");
        if (h.State != "Waiting") throw new DomainException("This hand-over has already been accepted.");
        h.State = "Accepted"; h.AcceptedBy = actor.Name; h.AcceptedAt = DateTimeOffset.UtcNow;
        var job = await db.Jobs.Include(j => j.Legs).Include(j => j.Charges).FirstAsync(j => j.Id == h.JobId);
        var leg = job.Legs.FirstOrDefault(l => l.Unit == h.Unit);
        if (leg is not null && leg.ProgressPct == 0) { leg.Status = "In progress"; leg.ProgressPct = 10; leg.Owner = actor.Name; }
        db.AddTimeline(h.JobId, h.Unit, $"Hand-over accepted by {Units.Names[h.Unit]}", $"{h.Title} · {actor.Name}");
        JobRules.Recompute(job);
        db.Log("Handoff", id.ToString(), actor, "Accepted");
        await db.SaveChangesAsync();
        return Results.Ok(h);
    }
}
