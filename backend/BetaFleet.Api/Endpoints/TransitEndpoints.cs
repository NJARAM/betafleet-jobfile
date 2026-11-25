using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using BetaFleet.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Endpoints;

public record CreateFmRequest(string JobId, string RouteCode, string? Truck, string? Trailer, string? Driver);
public record FmLineEdit(int Id, decimal Requested, string? Reason);
public record UpdateFmRequest(string? Truck, string? Trailer, string? Driver, List<FmLineEdit>? Lines);
public record DecisionRequest(string? Comment);
public record DisburseRequest(string PayRef, string? PayMethod);
public record TripUpdateRequest(int ProgressPct, string Place, int SpeedKmh, string? Status, string? Eta);
public record PodRequest(string SignedBy);

public static class TransitEndpoints
{
    public static void MapTransit(this RouteGroupBuilder api)
    {
        var routes = api.MapGroup("/routes").WithTags("Route master");
        routes.MapGet("/", async (AppDb db) => (await db.Routes.OrderBy(r => r.Code).ToListAsync()).Select(r =>
        {
            var lines = RouteRules.StandardLines(r);
            return new { Route = r, Lines = lines.Select(l => new { l.Code, l.Name, l.Basis, l.Qty, l.Rate, l.Unit, l.Standard }), Total = lines.Sum(l => l.Standard) };
        }));

        var fm = api.MapGroup("/filling-masters").WithTags("Filling masters");
        fm.MapGet("/", async (AppDb db) =>
            (await db.FillingMasters.Include(f => f.Lines).OrderByDescending(f => f.Id).ToListAsync()).Select(f => Summary(f)));
        fm.MapGet("/{id}", Detail);
        fm.MapPost("/", Create);
        fm.MapPut("/{id}", Update);
        fm.MapPost("/{id}/submit", Submit);
        fm.MapPost("/{id}/approve", Approve);
        fm.MapPost("/{id}/reject", Reject);
        fm.MapPost("/{id}/disburse", Disburse);
        fm.MapPost("/{id}/convert", Convert);
        api.MapGet("/fit-check", async (AppDb db, string truck, string driver) => await FleetRules.FitChecks(db, truck, driver)).WithTags("Filling masters");

        var so = api.MapGroup("/shipments").WithTags("Shipment orders & tracking");
        so.MapGet("/", async (AppDb db, HttpContext ctx, string? driver) =>
        {
            var a = Actor.From(ctx);
            if (a.Role == Roles.Driver) driver = a.Name; // drivers only ever see their own trips
            return await db.ShipmentOrders.Where(s => driver == null || s.Driver == driver).OrderByDescending(s => s.Id).ToListAsync();
        });
        so.MapPut("/{id}/position", UpdatePosition);
        so.MapPost("/{id}/pod", Pod);
        api.MapGet("/tracking/alerts", async (AppDb db) => await db.TrackingAlerts.OrderByDescending(a => a.At).ToListAsync()).WithTags("Shipment orders & tracking");
    }

    static object Summary(FillingMaster f)
    {
        var std = f.Lines.Sum(l => l.Standard); var req = f.Lines.Sum(l => l.Requested);
        return new { f.Id, f.JobId, f.TruckRequest, f.Truck, f.Trailer, f.Driver, f.RouteCode, f.Status, f.PayRef, f.ShipmentOrderId, Standard = std, Requested = req, VariancePct = RouteRules.VariancePct(std, req) };
    }

    static async Task<IResult> Detail(AppDb db, string id)
    {
        var f = await db.FillingMasters.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (f is null) return Results.NotFound();
        var job = await db.Jobs.Include(j => j.Customer).FirstAsync(j => j.Id == f.JobId);
        return Results.Ok(new
        {
            Summary = Summary(f), Customer = job.Customer.Name, f.PayMethod, f.Comment, Flow = FmStatus.Flow, Tolerance = FillingMaster.TolerancePct,
            Lines = f.Lines.OrderBy(l => l.Id).Select(l =>
            {
                var v = RouteRules.VariancePct(l.Standard, l.Requested);
                return new { l.Id, l.Code, l.Name, l.Basis, l.Qty, l.Rate, l.UnitLabel, l.Standard, l.Requested, l.Reason, VariancePct = v, Over = Math.Abs(v) > FillingMaster.TolerancePct };
            }),
            FitChecks = await FleetRules.FitChecks(db, f.Truck, f.Driver),
            History = await db.History("FillingMaster", id)
        });
    }

    static async Task<IResult> Create(AppDb db, HttpContext ctx, CreateFmRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk);
        var job = await db.Jobs.Get(r.JobId, "Job");
        if (!job.Units.Contains(Units.Transit)) throw new DomainException("This job has no Transit Transport leg.");
        var route = await db.Routes.Get(r.RouteCode, "Route");
        var id = await db.NextNumber("FM", $"FM-{Clock.Today.Year}-", 4);
        var f = new FillingMaster { Id = id, JobId = job.Id, RouteCode = route.Code, TruckRequest = await db.NextNumber("TR", $"TR-{Clock.Today.Year}-", 4), Truck = r.Truck, Trailer = r.Trailer, Driver = r.Driver };
        f.Lines.AddRange(RouteRules.StandardLines(route).Select(l => new FmLine
        {
            FillingMasterId = id, Code = l.Code, Name = l.Name, Basis = l.Basis, Qty = l.Qty, Rate = l.Rate, UnitLabel = l.Unit, Standard = l.Standard, Requested = l.Standard
        }));
        db.FillingMasters.Add(f);
        db.Log("FillingMaster", id, actor, $"Created; expense lines loaded from route {route.Code} v{route.Version}");
        await db.SaveChangesAsync();
        return Results.Created($"/api/filling-masters/{id}", new { id });
    }

    static async Task<FillingMaster> Load(AppDb db, string id) =>
        await db.FillingMasters.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id) ?? throw new DomainException("Filling master not found", 404);

    static async Task<IResult> Update(AppDb db, HttpContext ctx, string id, UpdateFmRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk);
        var f = await Load(db, id);
        if (f.Status is not (FmStatus.Draft or FmStatus.Rejected)) throw new DomainException($"Only a draft or rejected filling master can be edited (this one is {f.Status}).");
        if (r.Truck is not null) f.Truck = r.Truck == "" ? null : r.Truck;
        if (r.Trailer is not null) f.Trailer = r.Trailer == "" ? null : r.Trailer;
        if (r.Driver is not null) f.Driver = r.Driver == "" ? null : r.Driver;
        foreach (var e in r.Lines ?? [])
        {
            var l = f.Lines.FirstOrDefault(x => x.Id == e.Id) ?? throw new DomainException($"Line {e.Id} not on this filling master.", 400);
            if (e.Requested < 0) throw new DomainException("Amounts cannot be negative.", 400);
            l.Requested = e.Requested; l.Reason = e.Reason?.Trim() ?? "";
        }
        db.Log("FillingMaster", id, actor, "Edited assignment / requested amounts");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> Submit(AppDb db, HttpContext ctx, string id)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk);
        var f = await Load(db, id);
        if (f.Status is not (FmStatus.Draft or FmStatus.Rejected)) throw new DomainException("Already submitted.");
        if (string.IsNullOrEmpty(f.Truck) || string.IsNullOrEmpty(f.Trailer) || string.IsNullOrEmpty(f.Driver)) throw new DomainException("Assign truck, trailer and driver before submitting.");
        var missing = f.Lines.Where(l => Math.Abs(RouteRules.VariancePct(l.Standard, l.Requested)) > FillingMaster.TolerancePct && string.IsNullOrWhiteSpace(l.Reason)).Select(l => l.Name).ToList();
        if (missing.Count > 0) throw new DomainException($"Give a reason for lines more than {FillingMaster.TolerancePct}% off standard: {string.Join(", ", missing)}.");
        await FleetRules.EnsureFit(db, f.Truck, f.Driver);
        f.Status = FmStatus.Pending;
        db.Log("FillingMaster", id, actor, $"Submitted for approval · requested ${f.Lines.Sum(l => l.Requested):N2}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> Approve(AppDb db, HttpContext ctx, string id, DecisionRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.OpsManager);
        var f = await Load(db, id);
        if (f.Status != FmStatus.Pending) throw new DomainException("Only a pending filling master can be approved.");
        await FleetRules.EnsureFit(db, f.Truck, f.Driver);
        f.Status = FmStatus.Approved; f.Comment = r.Comment;
        db.Log("FillingMaster", id, actor, "Approved — released to Finance" + (r.Comment is { Length: > 0 } c ? $" · {c}" : ""));
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> Reject(AppDb db, HttpContext ctx, string id, DecisionRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.OpsManager);
        if (string.IsNullOrWhiteSpace(r.Comment)) throw new DomainException("Say why it is rejected so the clerk can fix it.", 400);
        var f = await Load(db, id);
        if (f.Status != FmStatus.Pending) throw new DomainException("Only a pending filling master can be rejected.");
        f.Status = FmStatus.Rejected; f.Comment = r.Comment;
        db.Log("FillingMaster", id, actor, $"Rejected · {r.Comment}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> Disburse(AppDb db, HttpContext ctx, string id, DisburseRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Finance);
        if (string.IsNullOrWhiteSpace(r.PayRef)) throw new DomainException("Enter the EFT / fuel-card reference.", 400);
        var f = await Load(db, id);
        if (f.Status != FmStatus.Approved) throw new DomainException("Finance can only disburse an approved filling master.");
        f.Status = FmStatus.Disbursed; f.PayRef = r.PayRef.Trim(); if (r.PayMethod is { Length: > 0 }) f.PayMethod = r.PayMethod;
        db.Log("FillingMaster", id, actor, $"Disbursed ${f.Lines.Sum(l => l.Requested):N2} · {f.PayRef}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> Convert(AppDb db, HttpContext ctx, string id)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Clerk, Roles.OpsManager);
        var f = await Load(db, id);
        if (f.Status != FmStatus.Disbursed) throw new DomainException("Convert after Finance has disbursed the trip funds.");
        await FleetRules.EnsureFit(db, f.Truck, f.Driver);
        var route = await db.Routes.Get(f.RouteCode, "Route");
        var soId = await db.NextNumber("SO", $"SO-{Clock.Today.Year}-", 4);
        db.ShipmentOrders.Add(new ShipmentOrder
        {
            Id = soId, JobId = f.JobId, FillingMasterId = f.Id, Truck = f.Truck!, Driver = f.Driver!, RouteCode = f.RouteCode, StandardKm = route.OutKm,
            Status = "Loading", Eta = Clock.Today.AddDays(route.Days).ToString("dd MMM")
        });
        f.Status = FmStatus.Converted; f.ShipmentOrderId = soId;
        var veh = await db.Vehicles.Get(f.Truck!, "Vehicle");
        veh.Status = "On trip"; veh.Driver = f.Driver;
        var job = await db.Jobs.Include(j => j.Legs).Include(j => j.Charges).FirstAsync(j => j.Id == f.JobId);
        var leg = job.Legs.FirstOrDefault(l => l.Unit == Units.Transit);
        if (leg is not null) { leg.Status = "In transit"; leg.ProgressPct = Math.Max(leg.ProgressPct, 20); }
        db.AddTimeline(f.JobId, Units.Transit, $"Truck dispatched · {soId}", $"{f.Truck} · {f.Driver} · {route.Name}");
        JobRules.Recompute(job);
        db.Log("FillingMaster", id, actor, $"Converted to shipment order {soId}");
        await db.SaveChangesAsync();
        return Results.Ok(new { shipmentOrderId = soId });
    }

    static async Task<IResult> UpdatePosition(AppDb db, HttpContext ctx, string id, TripUpdateRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Driver, Roles.Clerk, Roles.OpsManager);
        var s = await db.ShipmentOrders.Get(id, "Shipment order");
        if (actor.Role == Roles.Driver && s.Driver != actor.Name) throw new DomainException("This trip is assigned to another driver.", 403);
        if (s.DeliveredAt is not null) throw new DomainException("Trip already delivered.");
        s.ProgressPct = Math.Clamp(r.ProgressPct, 0, 99); s.Place = r.Place; s.SpeedKmh = r.SpeedKmh; s.GpsKm = (int)(s.StandardKm * s.ProgressPct / 100.0);
        if (r.Eta is { Length: > 0 }) s.Eta = r.Eta;
        s.Status = r.Status ?? (r.SpeedKmh > 80 ? "Speeding" : r.SpeedKmh == 0 ? "Stopped" : "On route");
        if (s.Status == "Speeding")
            db.TrackingAlerts.Add(new TrackingAlert { Severity = "warn", Kind = "Speeding", Text = $"{s.Truck} at {r.SpeedKmh} km/h (limit 80) near {r.Place}", ShipmentOrderId = id, JobId = s.JobId, At = DateTimeOffset.UtcNow });
        db.Log("Shipment", id, actor, $"Position {r.Place} · {s.ProgressPct}% · {r.SpeedKmh} km/h");
        await db.SaveChangesAsync();
        return Results.Ok(s);
    }

    static async Task<IResult> Pod(AppDb db, HttpContext ctx, string id, PodRequest r)
    {
        var actor = Actor.From(ctx);
        actor.Require(Roles.Driver, Roles.Clerk, Roles.OpsManager);
        if (string.IsNullOrWhiteSpace(r.SignedBy)) throw new DomainException("Enter the name of the person who signed the POD.", 400);
        var s = await db.ShipmentOrders.Get(id, "Shipment order");
        if (actor.Role == Roles.Driver && s.Driver != actor.Name) throw new DomainException("This trip is assigned to another driver.", 403);
        if (s.DeliveredAt is not null) throw new DomainException("POD already captured.");
        s.DeliveredAt = DateTimeOffset.UtcNow; s.PodSignedBy = r.SignedBy.Trim(); s.ProgressPct = 100; s.Status = "Delivered"; s.SpeedKmh = 0; s.GpsKm = s.StandardKm;
        var veh = await db.Vehicles.FindAsync(s.Truck);
        if (veh is not null) veh.Status = "Available";

        var job = await db.Jobs.Include(j => j.Legs).Include(j => j.Charges).FirstAsync(j => j.Id == s.JobId);
        var allSo = await db.ShipmentOrders.Where(x => x.JobId == s.JobId).ToListAsync();
        var leg = job.Legs.FirstOrDefault(l => l.Unit == Units.Transit);
        var allDelivered = allSo.All(x => x.DeliveredAt is not null);
        if (leg is not null)
        {
            leg.ProgressPct = allDelivered ? 100 : Math.Max(leg.ProgressPct, (int)allSo.Average(x => x.ProgressPct));
            leg.Status = allDelivered ? "Completed" : $"{allSo.Count(x => x.DeliveredAt != null)} of {allSo.Count} delivered";
        }
        // Freight is billed on POD: once every truck is delivered, transit charges become ready to invoice.
        if (allDelivered)
            foreach (var c in job.Charges.Where(c => c.Unit == Units.Transit && c.Status == "Awaiting POD")) c.Status = "Ready";
        db.AddTimeline(s.JobId, Units.Transit, $"Delivered · POD captured · {s.Id}", $"{s.Truck} · signed by {s.PodSignedBy}");
        JobRules.Recompute(job);
        db.Log("Shipment", id, actor, $"POD captured · signed by {s.PodSignedBy}");
        await db.SaveChangesAsync();
        return Results.Ok(s);
    }
}
