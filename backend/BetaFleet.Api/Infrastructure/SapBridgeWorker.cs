using System.Net.Http.Json;
using BetaFleet.Api.Data;
using BetaFleet.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

namespace BetaFleet.Api.Infrastructure;

public class SapOptions
{
    /// Base URL of the SAP Business One integration bridge (Windows service). The API never talks to SAP directly.
    public string? BridgeUrl { get; set; }
    /// With no bridge configured, demo mode marks queued invoices as posted with a fake A/R number.
    public bool DemoMode { get; set; } = true;
    public int PollSeconds { get; set; } = 15;
}

/// Outbox worker: picks up invoices queued for SAP and hands them to the integration bridge.
public class SapBridgeWorker(IServiceScopeFactory scopes, IHttpClientFactory http, Microsoft.Extensions.Options.IOptions<SapOptions> opts, ILogger<SapBridgeWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        var o = opts.Value;
        while (!stop.IsCancellationRequested)
        {
            try { await Tick(o, stop); }
            catch (Exception ex) when (!stop.IsCancellationRequested) { log.LogWarning(ex, "SAP bridge tick failed"); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, o.PollSeconds)), stop);
        }
    }

    async Task Tick(SapOptions o, CancellationToken stop)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var queued = await db.Invoices.Include(i => i.Lines).Include(i => i.Customer).Where(i => i.SapStatus == "Queued" || i.SapStatus == "Failed").OrderBy(i => i.No).Take(20).ToListAsync(stop);
        if (queued.Count == 0) return;
        foreach (var inv in queued)
        {
            if (string.IsNullOrWhiteSpace(o.BridgeUrl))
            {
                if (!o.DemoMode) continue;
                inv.SapStatus = "Posted"; inv.SapDocNo = $"A/R {inv.No} (demo)";
                continue;
            }
            try
            {
                var t = FinanceEndpoints.Totals(inv);
                var payload = new
                {
                    inv.No, inv.Date, CardName = inv.Customer.Name, inv.Sbu, t.Vat, t.Total,
                    // Each line carries the job file and unit as cost-centre dimensions so profitability survives in SAP.
                    Lines = inv.Lines.Select(l => new { l.Description, l.Amount, l.IsDisbursement, JobFile = l.JobId, CostCentre = l.Unit })
                };
                var res = await http.CreateClient("sap").PostAsJsonAsync(o.BridgeUrl.TrimEnd('/') + "/api/ar-invoices", payload, stop);
                res.EnsureSuccessStatusCode();
                var body = await res.Content.ReadFromJsonAsync<Dictionary<string, object>>(stop);
                inv.SapStatus = "Posted"; inv.SapDocNo = body?.GetValueOrDefault("docNum")?.ToString() ?? "posted";
            }
            catch (Exception ex)
            {
                inv.SapStatus = "Failed";
                log.LogWarning(ex, "Posting invoice {No} to SAP bridge failed", inv.No);
            }
        }
        await db.SaveChangesAsync(stop);
    }
}
