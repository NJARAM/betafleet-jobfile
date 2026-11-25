using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using BetaFleet.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Endpoints;

public record TransportRequestInput(int CustomerId, string[] Units, string Cargo, string? Weight, string From, string To, DateOnly Date, string? Reference, string? Notes);

public static class PortalEndpoints
{
    public static void MapPortal(this RouteGroupBuilder api)
    {
        api.MapGet("/customers", async (AppDb db) => await db.Customers.OrderBy(c => c.Name).ToListAsync()).WithTags("Lookups");
        api.MapGet("/lookups", async (AppDb db) => new
        {
            Units = Units.Names,
            Customers = await db.Customers.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name }).ToListAsync(),
            Routes = await db.Routes.OrderBy(r => r.Code).Select(r => new { r.Code, r.Name }).ToListAsync(),
            Vehicles = await db.Vehicles.OrderBy(v => v.Reg).Select(v => new { v.Reg, v.Type, v.Status, v.Subcontracted }).ToListAsync(),
            Drivers = await db.Drivers.OrderBy(d => d.Name).Select(d => d.Name).ToListAsync(),
            Trailers = await db.Vehicles.Where(v => v.Trailer != null).OrderBy(v => v.Trailer).Select(v => v.Trailer!).ToListAsync(),
            Warehouses = new[] { "WH-DAR-01 Kurasini", "WH-DAR-02 Mbezi", "Bonded WH-DAR-B1" },
            EntryTypes = new[] { "Transit (T1)", "Home consumption", "Export", "Warehousing (bonded)" },
            Depots = new[] { "Kurasini empty depot", "Ubungo empty depot", "Mbezi empty depot", "Lusaka empty depot (Zambia)" },
            ContainerSizes = new[] { "20′ GP", "40′ GP", "40′ HC", "20′ reefer", "40′ reefer" },
            ExpenseTypes = FinanceEndpoints.ExpenseTypes,
            ClearingStages = ClearingEntry.Stages,
            RequestSteps = TransportRequest.Steps
        }).WithTags("Lookups");

        var p = api.MapGroup("/portal").WithTags("Customer portal");
        p.MapGet("/{customerId:int}", Overview);
        p.MapPost("/requests", Submit);
        p.MapGet("/requests", async (AppDb db) => await db.TransportRequests.Include(r => r.Customer).OrderByDescending(r => r.Id)
            .Select(r => new { r.Id, Customer = r.Customer.Name, r.CustomerId, r.Services, r.Cargo, r.Weight, r.From, r.To, r.Date, r.Reference, r.Notes, r.Step, r.JobId }).ToListAsync());
        p.MapPost("/requests/{id}/review", Review);
        p.MapPost("/requests/{id}/open-job", OpenJob);
    }

    static async Task<IResult> Overview(AppDb db, HttpContext ctx, int customerId)
    {
        var customer = await db.Customers.Get(customerId, "Customer");
        var jobs = await db.Jobs.Include(j => j.Timeline).Include(j => j.Documents).Where(j => j.CustomerId == customerId).OrderByDescending(j => j.Id).AsSplitQuery().ToListAsync();
        var ids = jobs.Select(j => j.Id).ToList();
        var trips = await db.ShipmentOrders.Where(s => ids.Contains(s.JobId)).ToListAsync();
        var invoices = await db.Invoices.Include(i => i.Lines).Where(i => i.CustomerId == customerId).OrderByDescending(i => i.No).ToListAsync();
        var requests = await db.TransportRequests.Where(r => r.CustomerId == customerId).OrderByDescending(r => r.Id).ToListAsync();
        var containers = await db.Containers.Where(c => ids.Contains(c.JobId)).Include(c => c.Events).ToListAsync();
        return Results.Ok(new
        {
            Customer = customer,
            Jobs = jobs.Select(j => new
            {
                j.Id, j.Reference, j.Cargo, j.Stage, j.ProgressPct, j.Eta, Units = j.Units.Select(c => c.ToString()),
                // Customers see milestones and shared documents, not internal costs.
                Milestones = j.Timeline.OrderBy(t => t.Done ? 0 : 1).ThenBy(t => t.Date).Select(t => new { t.Date, t.Unit, t.Title, t.Done }),
                Documents = j.Documents.Where(d => d.Status is "Received" or "Released" or "Signed" or "Generated").Select(d => new { d.Name, d.Reference, d.Status }),
                Trips = trips.Where(s => s.JobId == j.Id).Select(s => new { s.Id, s.Truck, s.Place, s.ProgressPct, s.Eta, s.Status, s.DeliveredAt }),
                Containers = containers.Where(c => c.JobId == j.Id).Select(ClearingEndpoints.ContainerRow)
            }),
            Invoices = invoices.Select(i => { var t = FinanceEndpoints.Totals(i); return new { i.No, i.Date, i.Sbu, i.Status, Jobs = i.Lines.Select(l => l.JobId).Distinct(), t.Total }; }),
            Requests = requests,
            Steps = TransportRequest.Steps
        });
    }

    static async Task<IResult> Submit(AppDb db, HttpContext ctx, TransportRequestInput r)
    {
        var actor = Actor.From(ctx);
        // A customer account can only request for its own company, whatever the body says.
        var customerId = actor.Role == Roles.Customer ? actor.CustomerId!.Value : r.CustomerId;
        var customer = await db.Customers.Get(customerId, "Customer");
        var units = Units.Ordered.Where(r.Units.Contains).ToArray();
        if (units.Length == 0) throw new DomainException("Pick at least one service.", 400);
        if (string.IsNullOrWhiteSpace(r.Cargo) || string.IsNullOrWhiteSpace(r.From) || string.IsNullOrWhiteSpace(r.To)) throw new DomainException("Cargo, from and to are required.", 400);
        if (r.Date < Clock.Today) throw new DomainException("Pick-up date cannot be in the past.", 400);
        var id = await db.NextNumber("PR", $"PR-{Clock.Today.Year}-", 4);
        db.TransportRequests.Add(new TransportRequest
        {
            Id = id, CustomerId = customer.Id, Services = string.Concat(units), Cargo = r.Cargo.Trim(), Weight = r.Weight ?? "", From = r.From.Trim(), To = r.To.Trim(),
            Date = r.Date, Reference = r.Reference ?? "", Notes = r.Notes ?? ""
        });
        db.Log("TransportRequest", id, actor, $"Submitted by {customer.Name}");
        await db.SaveChangesAsync();
        return Results.Created($"/api/portal/requests/{id}", new { id });
    }

    static async Task<IResult> Review(AppDb db, HttpContext ctx, string id)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.OpsManager, Roles.Clerk);
        var r = await db.TransportRequests.Get(id, "Request");
        if (r.Step >= 2) throw new DomainException("Already approved.");
        r.Step++;
        db.Log("TransportRequest", id, actor, TransportRequest.Steps[r.Step]);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> OpenJob(AppDb db, HttpContext ctx, string id)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.OpsManager, Roles.Clerk);
        var r = await db.TransportRequests.Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Request not found", 404);
        if (r.Step != 2) throw new DomainException("Approve the request before opening a job file.");
        var jobId = await db.NextNumber("JOB", $"JOB-{Clock.Today.Year}-", 5);
        var job = new Job
        {
            Id = jobId, CustomerId = r.CustomerId, Reference = r.Reference.Length > 0 ? r.Reference : r.Id, Cargo = r.Cargo, Units = r.Services, Owner = actor.Name,
            OpenedOn = Clock.Today, Eta = r.Date.ToString("dd MMM"), Direction = r.Services.Contains('C') ? "Import" : "Local"
        };
        foreach (var u in r.Services.Select(c => c.ToString()))
        {
            var legNo = await db.NextNumber("LEG-" + u, $"{JobRules.LegPrefix(u)}-{Clock.Today.Year}-", 4);
            job.Legs.Add(new JobLeg { JobId = jobId, Unit = u, Number = legNo, Status = "Hand-over sent" });
            if (u == Units.Clearing)
                db.ClearingEntries.Add(new ClearingEntry { Id = legNo, JobId = jobId, EntryType = "Transit (T1)", Containers = r.Cargo, NextUnit = r.Services.Contains('W') ? "W" : r.Services.Contains('T') ? "T" : r.Services.Contains('D') ? "D" : "X" });
            db.Handoffs.Add(new Handoff { Unit = u, JobId = jobId, FromUnit = Units.File, Title = $"New file from portal request {r.Id}", Note = $"{r.Cargo} · {r.From} → {r.To}", CreatedAt = DateTimeOffset.UtcNow });
        }
        db.Jobs.Add(job);
        db.AddTimeline(jobId, Units.File, $"File opened from portal request {r.Id}", $"{r.Customer.Name} · {r.Cargo}");
        r.Step = 3; r.JobId = jobId;
        db.Log("TransportRequest", id, actor, $"Job file {jobId} opened");
        await db.SaveChangesAsync();
        return Results.Ok(new { jobId });
    }
}
