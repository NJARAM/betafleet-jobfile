using BetaFleet.Api.Data;
using BetaFleet.Api.Domain;
using BetaFleet.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Endpoints;

public record RaiseInvoiceRequest(int CustomerId, int[] ChargeIds, string Mode); // Mode: consolidated | per-sbu
public record AccessorialRequest(string JobId, string Unit, string Description, decimal Amount, string Reason);
public record CreateExpenseRequest(string JobId, string Unit, string Type, string Payee, decimal Amount, string Currency, string? Reference, bool Rechargeable, string? Notes);
public record PayRequest(string? Voucher);

public static class FinanceEndpoints
{
    public static readonly Dictionary<string, string[]> ExpenseTypes = new()
    {
        ["C"] = ["Customs duty & taxes", "Port charges (TPA)", "Shipping line DO / container deposit", "Container storage at port", "Agency / documentation"],
        ["W"] = ["Casual labour", "Forklift hire", "Fumigation", "Packing materials"],
        ["D"] = ["Fuel top-up", "Driver allowance", "Parking & tolls", "Courier subcontractor"],
        ["T"] = ["Border clearance fees", "Road permits / carbon tax", "Weighbridge / police fines", "On-road repair", "Extra fuel", "Escort fees"]
    };

    public static void MapFinance(this RouteGroupBuilder api)
    {
        var b = api.MapGroup("/billing").WithTags("Billing");
        b.MapGet("/billable", Billable);
        b.MapPost("/accessorial", AddAccessorial);
        b.MapPost("/invoices", RaiseInvoice);
        b.MapGet("/invoices", async (AppDb db, int? customerId) =>
            (await db.Invoices.Include(i => i.Lines).Include(i => i.Customer).Where(i => customerId == null || i.CustomerId == customerId)
                .OrderByDescending(i => i.No).ToListAsync()).Select(InvoiceRow));
        b.MapPost("/invoices/{no}/paid", async (AppDb db, HttpContext ctx, string no) =>
        {
            var actor = Actor.From(ctx); actor.Require(Roles.Finance);
            var inv = await db.Invoices.Include(i => i.Lines).Include(i => i.Customer).FirstOrDefaultAsync(i => i.No == no) ?? throw new DomainException("Invoice not found", 404);
            if (inv.Status == "Paid") throw new DomainException("Already paid.");
            inv.Status = "Paid"; inv.Customer.OpenBalance = Math.Max(0, inv.Customer.OpenBalance - Totals(inv).Total);
            db.Log("Invoice", no, actor, "Marked paid");
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        var e = api.MapGroup("/expenses").WithTags("Expense wizard");
        e.MapGet("/types", () => ExpenseTypes);
        e.MapGet("/", async (AppDb db, string? status, string? unit) =>
            await db.Expenses.Where(x => (status == null || x.Status == status) && (unit == null || x.Unit == unit)).OrderByDescending(x => x.Id).ToListAsync());
        e.MapGet("/{id}", async (AppDb db, string id) =>
        {
            var x = await db.Expenses.FindAsync(id);
            return x is null ? Results.NotFound() : Results.Ok(new { Expense = x, History = await db.History("Expense", id) });
        });
        e.MapPost("/", CreateExpense);
        e.MapPost("/{id}/approve", ApproveExpense);
        e.MapPost("/{id}/reject", RejectExpense);
        e.MapPost("/{id}/pay", PayExpense);

        api.MapGet("/profitability", Profitability).WithTags("Profitability");
    }

    public static (decimal Services, decimal Disbursements, decimal Vat, decimal Total) Totals(Invoice inv)
    {
        var svc = inv.Lines.Where(l => !l.IsDisbursement).Sum(l => l.Amount);
        var disb = inv.Lines.Where(l => l.IsDisbursement).Sum(l => l.Amount);
        var vat = Math.Round(svc * Invoice.VatRate, 2); // VAT on services only; disbursements are passed through at cost
        return (svc, disb, vat, svc + disb + vat);
    }

    static object InvoiceRow(Invoice i)
    {
        var t = Totals(i);
        return new
        {
            i.No, i.Date, Customer = i.Customer.Name, i.CustomerId, i.Sbu, i.Status, i.SapStatus, i.SapDocNo,
            Jobs = i.Lines.Select(l => l.JobId).Distinct(), Lines = i.Lines, t.Services, t.Disbursements, t.Vat, t.Total
        };
    }

    static async Task<object> Billable(AppDb db, int? customerId)
    {
        var rows = await (from c in db.Charges
                          join j in db.Jobs on c.JobId equals j.Id
                          where c.InvoiceNo == null && (customerId == null || j.CustomerId == customerId) && (c.Revenue > 0 || c.Kind == ChargeKind.Disbursement)
                          orderby c.JobId, c.Unit
                          select new { c.Id, c.JobId, c.Unit, c.Description, Kind = c.Kind.ToString(), Amount = c.Kind == ChargeKind.Disbursement ? c.Cost : c.Revenue, c.Status, j.CustomerId, Customer = j.Customer.Name })
            .ToListAsync();
        var customers = await db.Customers.OrderBy(c => c.Name).ToListAsync();
        return new { Lines = rows, Customers = customers, VatRate = Invoice.VatRate };
    }

    static async Task<IResult> AddAccessorial(AppDb db, HttpContext ctx, AccessorialRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Finance, Roles.OpsManager);
        if (r.Amount <= 0 || string.IsNullOrWhiteSpace(r.Description) || string.IsNullOrWhiteSpace(r.Reason)) throw new DomainException("Description, amount and reason are required.", 400);
        await db.Jobs.Get(r.JobId, "Job");
        db.Charges.Add(new Charge { JobId = r.JobId, Unit = r.Unit, Description = r.Description.Trim(), Kind = ChargeKind.Revenue, Revenue = r.Amount, Status = "Ready" });
        db.AddTimeline(r.JobId, Units.Billing, $"Accessorial charge added · ${r.Amount:N2}", $"{r.Description} — {r.Reason}");
        db.Log("Job", r.JobId, actor, $"Accessorial {r.Description} ${r.Amount:N2} · {r.Reason}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> RaiseInvoice(AppDb db, HttpContext ctx, RaiseInvoiceRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Finance);
        if (r.ChargeIds.Length == 0) throw new DomainException("Pick at least one ready line.", 400);
        var customer = await db.Customers.Get(r.CustomerId, "Customer");
        var charges = await db.Charges.Where(c => r.ChargeIds.Contains(c.Id)).ToListAsync();
        var jobIds = charges.Select(c => c.JobId).Distinct().ToList();
        var jobs = await db.Jobs.Include(j => j.Legs).Include(j => j.Charges).Where(j => jobIds.Contains(j.Id)).ToListAsync();
        if (jobs.Any(j => j.CustomerId != customer.Id)) throw new DomainException("All lines must belong to the same customer.", 400);
        var notReady = charges.Where(c => c.Status != "Ready" || c.InvoiceNo != null).ToList();
        if (notReady.Count > 0) throw new DomainException("Only ready, unbilled lines can be invoiced: " + string.Join(", ", notReady.Select(c => c.Description)));

        var groups = r.Mode == "per-sbu" ? charges.GroupBy(c => c.Unit).Select(g => (Sbu: g.Key, Items: g.ToList())).ToList() : [("ALL", charges)];
        var created = new List<Invoice>();
        foreach (var (sbu, items) in groups)
        {
            var inv = new Invoice { No = await db.NextNumber("INV", "", 5), Date = Clock.Today, CustomerId = customer.Id, Customer = customer, Sbu = sbu };
            foreach (var c in items)
            {
                inv.Lines.Add(new InvoiceLine { InvoiceNo = inv.No, ChargeId = c.Id, Unit = c.Unit, JobId = c.JobId, Description = c.Description, IsDisbursement = c.Kind == ChargeKind.Disbursement, Amount = c.Kind == ChargeKind.Disbursement ? c.Cost : c.Revenue });
                c.Status = "Billed"; c.InvoiceNo = inv.No;
            }
            db.Invoices.Add(inv);
            created.Add(inv);
        }
        var total = created.Sum(i => Totals(i).Total);
        var warning = customer.OpenBalance + total > customer.CreditLimit ? $"Over credit limit: open ${customer.OpenBalance + total:N0} of ${customer.CreditLimit:N0}" : null;
        customer.OpenBalance += total;
        foreach (var j in jobs)
        {
            db.AddTimeline(j.Id, Units.Billing, $"Invoice raised · {string.Join(", ", created.Select(i => "INV " + i.No))}", "Queued for SAP Business One via integration bridge");
            JobRules.Recompute(j);
        }
        foreach (var i in created) db.Log("Invoice", i.No, actor, $"Raised ({i.Sbu}) · ${Totals(i).Total:N2} · queued for SAP");
        await db.SaveChangesAsync();
        return Results.Ok(new { Invoices = created.Select(InvoiceRow), Warning = warning });
    }

    static async Task<IResult> CreateExpense(AppDb db, HttpContext ctx, CreateExpenseRequest r)
    {
        var actor = Actor.From(ctx);
        if (r.Amount <= 0) throw new DomainException("Amount must be more than zero.", 400);
        if (!ExpenseTypes.TryGetValue(r.Unit, out var types) || !types.Contains(r.Type)) throw new DomainException("Pick an expense type that belongs to the unit.", 400);
        if (string.IsNullOrWhiteSpace(r.Payee)) throw new DomainException("Payee is required.", 400);
        var job = await db.Jobs.Get(r.JobId, "Job");
        if (!job.Units.Contains(r.Unit)) throw new DomainException($"{job.Id} has no {Units.Names[r.Unit]} leg.", 400);
        var id = await db.NextNumber("EXP", $"EXP-{Clock.Today.Year}-", 4);
        db.Expenses.Add(new Expense
        {
            Id = id, JobId = r.JobId, Unit = r.Unit, Type = r.Type, Payee = r.Payee.Trim(), Amount = r.Amount, Currency = r.Currency, Reference = r.Reference ?? "",
            Rechargeable = r.Rechargeable, Notes = r.Notes ?? "", RequestedBy = actor.Name, Date = Clock.Today
        });
        db.Log("Expense", id, actor, $"Submitted · {r.Currency} {r.Amount:N2}");
        await db.SaveChangesAsync();
        return Results.Created($"/api/expenses/{id}", new { id });
    }

    static async Task<IResult> ApproveExpense(AppDb db, HttpContext ctx, string id)
    {
        var actor = Actor.From(ctx);
        var x = await db.Expenses.Get(id, "Expense");
        switch (x.Status)
        {
            case ExpStatus.PendingHead:
                actor.Require(Roles.UnitHead);
                if (x.RequestedBy == actor.Name) throw new DomainException("You cannot approve your own expense.");
                // Above the limit a second approval from Finance is needed.
                x.Status = x.Amount > Expense.FinanceLimit ? ExpStatus.PendingFinance : ExpStatus.Approved;
                db.Log("Expense", id, actor, x.Status == ExpStatus.PendingFinance ? $"Approved by unit head · over ${Expense.FinanceLimit:N0}, sent to Finance" : "Approved by unit head");
                break;
            case ExpStatus.PendingFinance:
                actor.Require(Roles.Finance);
                x.Status = ExpStatus.Approved;
                db.Log("Expense", id, actor, $"Approved by Finance (over ${Expense.FinanceLimit:N0})");
                break;
            default: throw new DomainException($"Nothing to approve: expense is {x.Status}.");
        }
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> RejectExpense(AppDb db, HttpContext ctx, string id, DecisionRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.UnitHead, Roles.Finance);
        if (string.IsNullOrWhiteSpace(r.Comment)) throw new DomainException("Give a reason.", 400);
        var x = await db.Expenses.Get(id, "Expense");
        if (x.Status is not (ExpStatus.PendingHead or ExpStatus.PendingFinance)) throw new DomainException($"Expense is {x.Status}; it can no longer be rejected.");
        x.Status = ExpStatus.Rejected;
        db.Log("Expense", id, actor, $"Rejected · {r.Comment}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<IResult> PayExpense(AppDb db, HttpContext ctx, string id, PayRequest r)
    {
        var actor = Actor.From(ctx); actor.Require(Roles.Finance);
        var x = await db.Expenses.Get(id, "Expense");
        if (x.Status != ExpStatus.Approved) throw new DomainException("Only approved expenses can be paid.");
        x.Status = ExpStatus.Paid; x.PaymentVoucher = string.IsNullOrWhiteSpace(r.Voucher) ? await db.NextNumber("PV", $"PV-{Clock.Today.Year}-", 4) : r.Voucher.Trim();
        // A rechargeable expense becomes a disbursement line to bill the customer at cost.
        if (x.Rechargeable)
            db.Charges.Add(new Charge { JobId = x.JobId, Unit = x.Unit, Description = $"{x.Type} — {x.Payee} (paid on behalf)", Kind = ChargeKind.Disbursement, Cost = x.Amount, Status = "Ready" });
        db.AddTimeline(x.JobId, x.Unit, $"Expense paid · {x.Id}", $"{x.Type} · {x.Currency} {x.Amount:N2} · {x.PaymentVoucher}");
        db.Log("Expense", id, actor, $"Paid · {x.PaymentVoucher}");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    static async Task<object> Profitability(AppDb db)
    {
        var jobs = await db.Jobs.Include(j => j.Customer).Include(j => j.Charges).ToListAsync();
        var expenses = await db.Expenses.Where(e => !e.Rechargeable && (e.Status == ExpStatus.Approved || e.Status == ExpStatus.Paid)).ToListAsync();
        var fmCost = await db.FillingMasters.Where(f => f.Status == FmStatus.Disbursed || f.Status == FmStatus.Converted)
            .Select(f => new { f.JobId, Cost = f.Lines.Sum(l => l.Requested) }).ToListAsync();

        var byJob = jobs.Select(j =>
        {
            var units = Units.Ordered.Where(u => j.Units.Contains(u)).Select(u =>
            {
                var rev = j.Charges.Where(c => c.Unit == u && c.Kind == ChargeKind.Revenue).Sum(c => c.Revenue);
                var cost = j.Charges.Where(c => c.Unit == u && c.Kind == ChargeKind.Revenue).Sum(c => c.Cost)
                           + expenses.Where(e => e.JobId == j.Id && e.Unit == u).Sum(e => e.Amount)
                           + (u == Units.Transit ? fmCost.Where(f => f.JobId == j.Id).Sum(f => f.Cost) : 0);
                return new { Unit = u, Revenue = rev, Cost = cost, Margin = rev - cost };
            }).ToList();
            var r = units.Sum(x => x.Revenue); var c = units.Sum(x => x.Cost);
            return new { j.Id, Customer = j.Customer.Name, j.Stage, Revenue = r, Cost = c, Margin = r - c, MarginPct = r == 0 ? 0 : Math.Round((r - c) / r * 100, 1), Units = units };
        }).OrderByDescending(x => x.Revenue).ToList();

        var byUnit = Units.Ordered.Select(u =>
        {
            var rows = byJob.SelectMany(j => j.Units).Where(x => x.Unit == u).ToList();
            var r = rows.Sum(x => x.Revenue); var c = rows.Sum(x => x.Cost);
            return new { Unit = u, Name = Units.Names[u], Revenue = r, Cost = c, Margin = r - c, MarginPct = r == 0 ? 0 : Math.Round((r - c) / r * 100, 1) };
        });
        var byCustomer = byJob.GroupBy(j => j.Customer).Select(g => new { Customer = g.Key, Jobs = g.Count(), Revenue = g.Sum(x => x.Revenue), Cost = g.Sum(x => x.Cost), Margin = g.Sum(x => x.Margin) }).OrderByDescending(x => x.Revenue);
        return new { Jobs = byJob, Units = byUnit, Customers = byCustomer };
    }
}
