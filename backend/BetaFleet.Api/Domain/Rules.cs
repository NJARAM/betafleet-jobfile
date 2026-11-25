using BetaFleet.Api.Data;
using BetaFleet.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Domain;

public record CostLine(string Code, string Name, string Basis, decimal Qty, decimal Rate, string Unit)
{
    public decimal Standard => Math.Round(Qty * Rate, 2);
}

public static class RouteRules
{
    /// Standard trip budget for one truck on a route. Filling masters start from these lines.
    public static List<CostLine> StandardLines(Route r) =>
    [
        new("MIL", "Mileage allowance", "Per km, outbound", r.OutKm, 0.12m, "km"),
        new("RET", "Return mileage", "Per km, return leg", r.ReturnKm, 0.08m, "km"),
        new("FUEL", "Fuel allocation", "Litres = km ÷ 2.0 km/l", Math.Round(r.OutKm / 2m), 1.35m, "l"),
        new("TOLL", "Toll fees", "Fixed per route", r.Toll > 0 ? 1 : 0, r.Toll, ""),
        new("BORD", "Border / clearing fees", "Fixed per crossing", r.Borders, 105, "crossings"),
        new("SUB", "Driver subsistence", "Per day of transit", r.Days, 30, "days"),
        new("WB", "Weighbridge fees", "Fixed per route", 1, 40, ""),
        new("PARK", "Parking / yard fees", "Per stop", 3, 10, "stops"),
        new("ESC", "Escort fees", "Fixed per route, where mandatory", r.Escort ? 1 : 0, 250, ""),
        new("INS", "Cargo insurance", "0.15% of cargo value", 1, 180, ""),
        new("CONT", "Contingency", "Fixed allowance (cap)", 1, 100, "")
    ];

    public static decimal VariancePct(decimal standard, decimal requested) =>
        standard == 0 ? (requested == 0 ? 0 : 100) : Math.Round((requested - standard) / standard * 100, 1);
}

public record FitCheck(string Key, string Label, bool Ok, string Detail);

public static class FleetRules
{
    public static bool Expired(ComplianceDoc d) => d.ExpiresOn is { } e && e < Clock.Today;
    public static bool ExpiringSoon(ComplianceDoc d) => d.ExpiresOn is { } e && e >= Clock.Today && e <= Clock.Today.AddDays(30);

    /// Is this truck + driver allowed on the road? Used before approving or dispatching a filling master.
    public static async Task<List<FitCheck>> FitChecks(AppDb db, string? truck, string? driver)
    {
        var checks = new List<FitCheck>();
        if (string.IsNullOrEmpty(truck) || string.IsNullOrEmpty(driver))
        {
            checks.Add(new("assign", "Truck and driver assigned", false, "Assign a truck, trailer and driver first"));
            return checks;
        }
        var veh = await db.Vehicles.FindAsync(truck);
        if (veh is null) { checks.Add(new("veh", "Vehicle on fleet register", false, $"{truck} is not registered")); return checks; }

        var openCards = await db.JobCards.CountAsync(j => j.Vehicle == truck && !j.Closed);
        checks.Add(new("jc", "No open job cards", openCards == 0, openCards == 0 ? "Workshop clear" : $"{openCards} open job card(s)"));

        if (!veh.Subcontracted && veh.OdometerKm is { } odo && veh.NextServiceKm is { } svc)
            checks.Add(new("svc", "Service not overdue", odo < svc, $"Odometer {odo:N0} km · next service {svc:N0} km"));

        var tyres = await db.Tyres.Where(t => t.Vehicle == truck && t.Status == "Fitted").ToListAsync();
        if (tyres.Count > 0)
        {
            var low = tyres.MinBy(t => t.TreadMm)!;
            checks.Add(new("tyre", "Tyres above minimum tread", low.TreadMm >= Tyre.TreadMinMm, $"Lowest tread {low.TreadMm} mm at {low.Position} (minimum {Tyre.TreadMinMm} mm)"));
        }

        var docs = await db.ComplianceDocs.Where(d => (d.SubjectType == "Vehicle" && d.SubjectId == truck) || (d.SubjectType == "Driver" && d.SubjectId == driver)).ToListAsync();
        var expired = docs.Where(Expired).ToList();
        checks.Add(new("docs", "Truck and driver documents valid", expired.Count == 0,
            expired.Count == 0 ? "All licences, insurance and permits valid" : "Expired: " + string.Join(", ", expired.Select(d => $"{d.SubjectId} {d.DocType}"))));
        return checks;
    }

    public static async Task EnsureFit(AppDb db, string? truck, string? driver)
    {
        var failed = (await FitChecks(db, truck, driver)).Where(c => !c.Ok).ToList();
        if (failed.Count > 0) throw new DomainException("Truck is not fit for the trip: " + string.Join("; ", failed.Select(f => f.Detail)));
    }
}

public static class JobRules
{
    /// Stage and progress follow the legs: the first unfinished unit is where the file sits.
    public static void Recompute(Job job)
    {
        if (job.Legs.Count == 0) return;
        job.ProgressPct = (int)Math.Round(job.Legs.Average(l => l.ProgressPct));
        var open = Units.Ordered.Select(u => job.Legs.FirstOrDefault(l => l.Unit == u)).FirstOrDefault(l => l is { ProgressPct: < 100 });
        if (open is not null) { job.Stage = open.ProgressPct == 0 && job.Legs.All(l => l.ProgressPct == 0) ? "Opened" : Units.Stage[open.Unit]; return; }
        // Work is done: the file stays in Billing until it has charges and every billable one is invoiced.
        var billable = job.Charges.Where(c => c.Revenue > 0 || c.Kind == ChargeKind.Disbursement).ToList();
        job.Stage = billable.Count == 0 || billable.Any(c => c.Status != "Billed") ? "Billing" : "Closed";
        if (job.Stage == "Closed") job.ProgressPct = 100;
    }

    public static string LegPrefix(string unit) => unit switch { "C" => "CLR", "W" => "WHR", "D" => "DPL", _ => "TRQ" };

    public static string FirstTask(string unit, string route, int trucks, string wh, int drops, string entry) => unit switch
    {
        "C" => $"Lodge {entry} entry",
        "W" => $"Inbound advice to {wh}",
        "D" => $"Plan {drops} delivery point(s)",
        _ => $"Truck request · {trucks} truck(s) · {route}"
    };
}
