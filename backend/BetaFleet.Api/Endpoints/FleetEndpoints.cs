using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using BetaFleet.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Endpoints;

public record RenewDocRequest(DateOnly ExpiresOn, string Reference);
public record DefectInput(string Item, string Category, string? Note);
public record CreateInspectionRequest(string Vehicle, string Type, int OdometerKm, List<DefectInput> Defects);
public record TaskUpdateRequest(string? Status, decimal? Hours, string? Mechanic);
public record AddTaskRequest(string Name, string Category, string? Mechanic, string? PartCode);
public record PartRequest(int TaskId, string PartCode, int Qty);
public record TyreActionRequest(string Action, string? Vehicle, string? Position, decimal? TreadMm, string? Reason); // fit | remove | retread | receive-retread | scrap | measure

public static class FleetEndpoints
{
    static readonly Dictionary<string, string> MechanicFor = new()
    {
        ["Engine & fluids"] = "P. Mrisho", ["Brakes & air system"] = "M. Ally", ["Electrical & lights"] = "R. Ngowi",
        ["Steering & suspension"] = "P. Mrisho", ["Tyres & wheels"] = "K. Swai", ["Cab & safety"] = "F. Hamisi"
    };

    public static void MapFleet(this RouteGroupBuilder api)
    {
        var f = api.MapGroup("/fleet").WithTags("Fleet");
        f.MapGet("/vehicles", Vehicles);
        f.MapGet("/drivers", async (AppDb db) =>
        {
            var docs = await db.ComplianceDocs.Where(d => d.SubjectType == "Driver").ToListAsync();
            return (await db.Drivers.OrderBy(d => d.Name).ToListAsync()).Select(d => new
            {
                d.Name, d.Note, Expired = docs.Where(x => x.SubjectId == d.Name).Count(FleetRules.Expired), Expiring = docs.Where(x => x.SubjectId == d.Name).Count(FleetRules.ExpiringSoon)
            });
        });
        f.MapGet("/trailers", async (AppDb db) => await db.Vehicles.Where(v => v.Trailer != null).Select(v => v.Trailer).Distinct().ToListAsync());

        var c = api.MapGroup("/compliance").WithTags("Compliance");
        c.MapGet("/", async (AppDb db) => (await db.ComplianceDocs.OrderBy(d => d.SubjectType).ThenBy(d => d.SubjectId).ThenBy(d => d.DocType).ToListAsync()).Select(d => new
        {
            d.Id, d.SubjectType, d.SubjectId, d.DocType, d.ExpiresOn, d.Reference,
            DaysLeft = d.ExpiresOn is { } e ? e.DayNumber - Clock.Today.DayNumber : (int?)null,
            State = d.ExpiresOn is null ? "n/a" : FleetRules.Expired(d) ? "Expired" : FleetRules.ExpiringSoon(d) ? "Expiring" : "Valid"
        }));
        c.MapPost("/{id:int}/renew", async (AppDb db, HttpContext ctx, int id, RenewDocRequest r) =>
        {
            var actor = Actor.From(ctx); actor.Require(Roles.OpsManager, Roles.Clerk);
            var d = await db.ComplianceDocs.Get(id, "Document");
            if (r.ExpiresOn <= Clock.Today) throw new DomainException("New expiry must be in the future.", 400);
            if (string.IsNullOrWhiteSpace(r.Reference)) throw new DomainException("Enter the certificate / policy reference.", 400);
            d.ExpiresOn = r.ExpiresOn; d.Reference = r.Reference.Trim();
            db.Log("Compliance", $"{d.SubjectId}/{d.DocType}", actor, $"Renewed to {r.ExpiresOn:dd MMM yyyy} · {d.Reference}");
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        c.MapGet("/history", async (AppDb db) => await db.Activity.Where(a => a.Entity == "Compliance").OrderByDescending(a => a.At).Take(30).ToListAsync());

        var w = api.MapGroup("/workshop").WithTags("Workshop");
        w.MapGet("/inspections", async (AppDb db) => await db.Inspections.Include(i => i.Defects).OrderByDescending(i => i.Id).ToListAsync());
        w.MapPost("/inspections", CreateInspection);
        w.MapGet("/job-cards", async (AppDb db) =>
        {
            var cards = await db.JobCards.Include(j => j.Tasks).OrderBy(j => j.Closed).ThenByDescending(j => j.Id).ToListAsync();
            var parts = await db.Parts.ToDictionaryAsync(p => p.Code);
            var reqs = await db.SparePartRequests.ToListAsync();
            return cards.Select(j => new
            {
                j.Id, j.Vehicle, j.Source, j.OpenedOn, j.OdometerKm, j.Closed, j.ClosedAt, Tasks = j.Tasks.OrderBy(t => t.Id),
                Requests = reqs.Where(r => r.JobCardId == j.Id),
                LabourCost = j.Tasks.Sum(t => t.Hours) * JobCard.LabourRate,
                PartsCost = reqs.Where(r => r.JobCardId == j.Id && r.Status == "Issued").Sum(r => parts.TryGetValue(r.PartCode, out var p) ? p.Cost * r.Qty : 0)
            });
        });
        w.MapPost("/job-cards/{id}/tasks", AddTask);
        w.MapPut("/job-cards/{id}/tasks/{taskId:int}", UpdateTask);
        w.MapPost("/job-cards/{id}/part-requests", RequestPart);
        w.MapPost("/part-requests/{id}/issue", IssuePart);
        w.MapPost("/job-cards/{id}/close", CloseJobCard);
        w.MapGet("/parts", async (AppDb db) => await db.Parts.OrderBy(p => p.Code).ToListAsync());
        w.MapGet("/mechanics", () => MechanicFor.Values.Distinct().Append("T. Mbwambo"));

        var t = api.MapGroup("/tyres").WithTags("Tyre management");
        t.MapGet("/", async (AppDb db) => await db.Tyres.OrderBy(x => x.Vehicle == null).ThenBy(x => x.Vehicle).ThenBy(x => x.Position).ThenBy(x => x.Serial).ToListAsync());
        t.MapGet("/{serial}/history", async (AppDb db, string serial) => await db.History("Tyre", serial));
        t.MapPost("/{serial}/action", TyreAction);
    }

    static async Task<object> Vehicles(AppDb db)
    {
        var vehicles = await db.Vehicles.OrderBy(v => v.Subcontracted).ThenBy(v => v.Reg).ToListAsync();
        var docs = await db.ComplianceDocs.Where(d => d.SubjectType == "Vehicle").ToListAsync();
        var cards = await db.JobCards.Where(j => !j.Closed).Select(j => j.Vehicle).ToListAsync();
        var trips = await db.ShipmentOrders.Where(s => s.DeliveredAt == null).ToListAsync();
        var tyres = await db.Tyres.Where(x => x.Status == "Fitted").ToListAsync();
        return vehicles.Select(v => new
        {
            v.Reg, v.FleetNo, v.Type, v.Status, v.Driver, v.Trailer, v.OdometerKm, v.NextServiceKm, v.Subcontracted,
            Trip = trips.FirstOrDefault(s => s.Truck == v.Reg) is { } s ? new { s.Id, s.JobId } : null,
            OpenJobCard = cards.Contains(v.Reg),
            ExpiredDocs = docs.Where(d => d.SubjectId == v.Reg).Count(FleetRules.Expired),
            LowTyres = tyres.Count(x => x.Vehicle == v.Reg && x.TreadMm < Tyre.TreadMinMm)
        });
    }

    static async Task<IResult> CreateInspection(AppDb db, HttpContext ctx, CreateInspectionRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Workshop, Roles.Clerk);
        var veh = await db.Vehicles.Get(r.Vehicle, "Vehicle");
        var id = await db.NextNumber("INS", $"INS-{Clock.Today.Year}-", 4);
        var ins = new Inspection { Id = id, Vehicle = veh.Reg, Type = r.Type, Date = Clock.Today, OdometerKm = r.OdometerKm, By = actor.Name };
        ins.Defects.AddRange(r.Defects.Select(d => new InspectionDefect { InspectionId = id, Item = d.Item, Category = d.Category, Note = d.Note ?? "" }));
        if (veh.OdometerKm is null || r.OdometerKm > veh.OdometerKm) veh.OdometerKm = r.OdometerKm;

        if (ins.Defects.Count == 0) ins.Status = "Passed";
        else
        {
            // Any defect opens a job card with one task per defect, routed to the right mechanic; the truck is held.
            var jcId = await db.NextNumber("JC", $"JC-{Clock.Today.Year}-", 4);
            var jc = new JobCard { Id = jcId, Vehicle = veh.Reg, Source = id, OpenedOn = Clock.Today, OdometerKm = r.OdometerKm };
            jc.Tasks.AddRange(ins.Defects.Select(d => new JobCardTask { JobCardId = jcId, Name = $"{d.Item}" + (d.Note.Length > 0 ? $" — {d.Note}" : ""), Category = d.Category, Mechanic = MechanicFor.GetValueOrDefault(d.Category, "") }));
            db.JobCards.Add(jc);
            ins.Status = "Job card opened"; ins.JobCardId = jcId;
            if (veh.Status == "Available") veh.Status = "In workshop";
            db.Log("JobCard", jcId, actor, $"Opened from inspection {id} · {ins.Defects.Count} defect(s)");
        }
        db.Inspections.Add(ins);
        db.Log("Inspection", id, actor, $"{r.Type} inspection · {ins.Status}");
        await db.SaveChangesAsync();
        return Results.Ok(ins);
    }

    static async Task<JobCard> OpenCard(AppDb db, string id)
    {
        var jc = await db.JobCards.Include(j => j.Tasks).FirstOrDefaultAsync(j => j.Id == id) ?? throw new DomainException("Job card not found", 404);
        if (jc.Closed) throw new DomainException("Job card is closed.");
        return jc;
    }

    static async Task<IResult> AddTask(AppDb db, HttpContext ctx, string id, AddTaskRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Workshop);
        var jc = await OpenCard(db, id);
        if (string.IsNullOrWhiteSpace(r.Name)) throw new DomainException("Task name is required.", 400);
        jc.Tasks.Add(new JobCardTask { JobCardId = id, Name = r.Name.Trim(), Category = r.Category, Mechanic = r.Mechanic ?? MechanicFor.GetValueOrDefault(r.Category, ""), PartCode = r.PartCode });
        db.Log("JobCard", id, actor, $"Task added · {r.Name}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> UpdateTask(AppDb db, HttpContext ctx, string id, int taskId, TaskUpdateRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Workshop);
        var jc = await OpenCard(db, id);
        var t = jc.Tasks.FirstOrDefault(x => x.Id == taskId) ?? throw new DomainException("Task not found", 404);
        if (r.Status is not null)
        {
            if (r.Status == "Done")
            {
                var pending = await db.SparePartRequests.AnyAsync(p => p.JobCardId == id && p.TaskId == taskId && p.Status == "Pending");
                if (pending) throw new DomainException("A spare part for this task is still waiting at the store.");
            }
            t.Status = r.Status;
        }
        if (r.Hours is { } h) { if (h < 0) throw new DomainException("Hours cannot be negative.", 400); t.Hours = h; }
        if (r.Mechanic is not null) t.Mechanic = r.Mechanic;
        db.Log("JobCard", id, actor, $"Task {taskId} · {t.Status} · {t.Hours} h · {t.Mechanic}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> RequestPart(AppDb db, HttpContext ctx, string id, PartRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Workshop);
        var jc = await OpenCard(db, id);
        var task = jc.Tasks.FirstOrDefault(x => x.Id == r.TaskId) ?? throw new DomainException("Task not found", 404);
        await db.Parts.Get(r.PartCode, "Part");
        if (r.Qty <= 0) throw new DomainException("Quantity must be at least 1.", 400);
        var sprId = await db.NextNumber("SPR", $"SPR-{Clock.Today.Year}-", 4);
        db.SparePartRequests.Add(new SparePartRequest { Id = sprId, JobCardId = id, TaskId = r.TaskId, PartCode = r.PartCode, Qty = r.Qty, RequestedBy = actor.Name });
        task.Status = "Awaiting parts"; task.PartCode = r.PartCode;
        db.Log("JobCard", id, actor, $"Requested {r.Qty} × {r.PartCode} ({sprId})");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> IssuePart(AppDb db, HttpContext ctx, string id)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Storekeeper);
        var spr = await db.SparePartRequests.Get(id, "Spare part request");
        if (spr.Status != "Pending") throw new DomainException("Already handled.");
        var part = await db.Parts.Get(spr.PartCode, "Part");
        if (part.Stock < spr.Qty) throw new DomainException($"Only {part.Stock} × {part.Code} in stock; raise a purchase request.");
        part.Stock -= spr.Qty;
        spr.Status = "Issued"; spr.IssueNo = await db.NextNumber("SI", $"SI-{Clock.Today.Year}-", 4);
        var task = await db.JobCardTasks.FirstAsync(t => t.Id == spr.TaskId);
        if (task.Status == "Awaiting parts") task.Status = "In progress";
        db.Log("JobCard", spr.JobCardId, actor, $"Issued {spr.Qty} × {part.Code} · {spr.IssueNo}" + (part.Stock <= part.Reorder ? " · below reorder level" : ""));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> CloseJobCard(AppDb db, HttpContext ctx, string id)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Workshop);
        var jc = await OpenCard(db, id);
        var open = jc.Tasks.Where(t => t.Status != "Done").ToList();
        if (open.Count > 0) throw new DomainException($"Finish every task first: {string.Join(", ", open.Select(t => t.Name))}.");
        jc.Closed = true; jc.ClosedAt = DateTimeOffset.UtcNow;
        var veh = await db.Vehicles.FindAsync(jc.Vehicle);
        if (veh is { Status: "In workshop" } && !await db.JobCards.AnyAsync(j => j.Vehicle == jc.Vehicle && !j.Closed && j.Id != id)) veh.Status = "Available";
        db.Log("JobCard", id, actor, "Closed · vehicle released");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> TyreAction(AppDb db, HttpContext ctx, string serial, TyreActionRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Workshop);
        var t = await db.Tyres.Get(serial, "Tyre");
        string text;
        switch (r.Action)
        {
            case "fit":
                if (!t.Status.StartsWith("In store")) throw new DomainException("Only a tyre in store can be fitted.");
                if (string.IsNullOrEmpty(r.Vehicle) || string.IsNullOrEmpty(r.Position)) throw new DomainException("Pick vehicle and position.", 400);
                if (await db.Tyres.AnyAsync(x => x.Vehicle == r.Vehicle && x.Position == r.Position && x.Status == "Fitted")) throw new DomainException($"{r.Vehicle} {r.Position} already has a tyre; remove it first.");
                t.Status = "Fitted"; t.Vehicle = r.Vehicle; t.Position = r.Position; text = $"Fitted to {r.Vehicle} {r.Position}"; break;
            case "remove":
                if (t.Status != "Fitted") throw new DomainException("Tyre is not fitted.");
                text = $"Removed from {t.Vehicle} {t.Position} at {t.TreadMm} mm" + (r.Reason is { Length: > 0 } ? $" — {r.Reason}" : "");
                t.Status = t.TreadMm < Tyre.TreadMinMm ? "Awaiting retread" : "In store · used"; t.Vehicle = null; t.Position = null; break;
            case "retread":
                if (t.Status is not ("Awaiting retread" or "In store · used")) throw new DomainException("Only a removed tyre can go to the retreader.");
                if (t.Retreads >= 2) throw new DomainException("Casing already retreaded twice; scrap it.");
                t.Status = "At retreader"; text = "Sent to retreader"; break;
            case "receive-retread":
                if (t.Status != "At retreader") throw new DomainException("Tyre is not at the retreader.");
                t.Status = "In store · retreaded"; t.Retreads++; t.TreadMm = 12; t.Cost += 140; text = "Received retreaded · cost $140"; break;
            case "scrap":
                if (t.Status == "Fitted") throw new DomainException("Remove the tyre before scrapping it.");
                t.Status = "Scrapped"; text = "Scrapped" + (r.Reason is { Length: > 0 } ? $" · {r.Reason}" : ""); break;
            case "measure":
                if (r.TreadMm is not { } mm || mm < 0 || mm > 25) throw new DomainException("Enter tread depth in mm.", 400);
                t.TreadMm = mm; text = $"Tread measured {mm} mm" + (mm < Tyre.TreadMinMm ? " · below legal minimum" : ""); break;
            default: throw new DomainException("Unknown tyre action.", 400);
        }
        db.Log("Tyre", serial, actor, text);
        await db.SaveChangesAsync();
        return Results.Ok(t);
    }
}
