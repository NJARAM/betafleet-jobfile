using BetaFleet.Api.Domain;
using BetaFleet.Api.Endpoints;
using BetaFleet.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Route = BetaFleet.Api.Domain.Route;

namespace BetaFleet.Api.Data;

/// Sample data carried over from the BetaFleet Job File prototype. Runs once, on an empty database.
public static class Seed
{
    static DateOnly D(int month, int day) => new(2026, month, day);
    // Times are written in East Africa Time and stored as UTC (Npgsql only accepts UTC for timestamptz).
    static DateTimeOffset T(int month, int day, int h, int m) => new DateTimeOffset(2026, month, day, h, m, 0, TimeSpan.FromHours(3)).ToUniversalTime();

    public static async Task RunAsync(AppDb db)
    {
        if (await db.Jobs.AnyAsync()) return;

        // ---------- customers ----------
        var cust = new[]
        {
            new Customer { Name = "Kilimanjaro Mining Company", CreditLimit = 60000, OpenBalance = 41200 },
            new Customer { Name = "Everdigitio Ltd", CreditLimit = 30000, OpenBalance = 6200 },
            new Customer { Name = "Mbeya Agro Supplies", CreditLimit = 25000, OpenBalance = 472 },
            new Customer { Name = "Copperbelt Traders", CreditLimit = 50000, OpenBalance = 12800 },
            new Customer { Name = "Bahari Retail Ltd", CreditLimit = 20000, OpenBalance = 3100 },
            new Customer { Name = "Zanzi Beverages", CreditLimit = 15000, OpenBalance = 0 },
            new Customer { Name = "Afya Health Distributors", CreditLimit = 10000, OpenBalance = 0 },
            new Customer { Name = "Jua Power Ltd", CreditLimit = 20000, OpenBalance = 0 }
        };
        db.Customers.AddRange(cust);
        await db.SaveChangesAsync();
        int C(string name) => cust.First(c => c.Name == name).Id;

        // ---------- jobs ----------
        Job J(string id, string c, string reference, string cargo, string units, string stage, int pct, string owner, DateOnly opened, string eta, string alert = "", string cargoType = "Containerised", string dir = "Import") =>
            new() { Id = id, CustomerId = C(c), Reference = reference, Cargo = cargo, Units = units, Stage = stage, ProgressPct = pct, Owner = owner, OpenedOn = opened, Eta = eta, Alert = alert, CargoType = cargoType, Direction = dir };

        var jobs = new[]
        {
            J("JOB-2026-00483", "Zanzi Beverages", "BL ONEU-5531288", "1 × 20′ bottling spares", "C", "Customs", 5, "A. Said", D(11, 10), "Customer collects", "Import permit pending"),
            J("JOB-2026-00481", "Kilimanjaro Mining Company", "BL MSKU-DAR-2611042", "5 × 40′ mining spares", "CWDT", "In transit", 72, "R. Mollel", D(10, 28), "14 Nov", "1 truck held at Tunduma"),
            J("JOB-2026-00480", "Afya Health Distributors", "Courier · 12 parcels", "12 parcels medical supplies", "D", "Distribution", 70, "H. Mushi", D(11, 9), "11 Nov", cargoType: "Loose cargo", dir: "Local"),
            J("JOB-2026-00479", "Everdigitio Ltd", "BL COSU-6402188", "2 × 20′ IT equipment", "CT", "In transit", 58, "J. Kimaro", D(10, 30), "15 Nov"),
            J("JOB-2026-00478", "Copperbelt Traders", "BL MAEU-2395140", "2 × 40′ copper cathode returns", "CT", "In transit", 40, "J. Kimaro", D(11, 1), "18 Nov", "Waiting for trucks", dir: "Transit"),
            J("JOB-2026-00476", "Mbeya Agro Supplies", "BL MAEU-23950771", "1,200 bags fertiliser", "CWD", "Distribution", 80, "R. Mollel", D(10, 24), "13 Nov"),
            J("JOB-2026-00474", "Jua Power Ltd", "UCR 26-DAR-04388", "72 pallets solar panels", "W", "Warehouse", 50, "M. Juma", D(10, 22), "Ongoing", cargoType: "Loose cargo", dir: "Local"),
            J("JOB-2026-00472", "Copperbelt Traders", "BL CMDU-8812044", "3 × 40′ steel coils", "CT", "Customs", 25, "A. Said", D(11, 3), "20 Nov", "T1 transit bond pending", dir: "Transit"),
            J("JOB-2026-00468", "Bahari Retail Ltd", "UCR 26-DAR-04412", "140 pallets FMCG", "WD", "Warehouse", 45, "M. Juma", D(10, 21), "Ongoing", cargoType: "Loose cargo", dir: "Local"),
            J("JOB-2026-00465", "Zanzi Beverages", "BL ONEU-5530917", "1 × 20′ aluminium cans", "C", "Billing", 95, "A. Said", D(10, 19), "Delivered", "Cost line not billed"),
            J("JOB-2026-00463", "Kilimanjaro Mining Company", "Local load", "30 t cement, Dar → Lusaka", "T", "Closed", 100, "J. Kimaro", D(10, 12), "Delivered", cargoType: "Loose cargo", dir: "Export")
        };
        db.Jobs.AddRange(jobs);

        // ---------- legs ----------
        void Leg(string job, string unit, string no, string status, int pct, string who, string summary) =>
            db.JobLegs.Add(new JobLeg { JobId = job, Unit = unit, Number = no, Status = status, ProgressPct = pct, Owner = who, Summary = summary });
        Leg("JOB-2026-00481", "C", "CLR-2026-0912", "Completed", 100, "A. Said", "2 entries released");
        Leg("JOB-2026-00481", "W", "WHR-2026-0377", "In storage", 60, "M. Juma", "52 of 86 pallets released");
        Leg("JOB-2026-00481", "D", "DPL-2026-0218", "2 of 3 delivered", 67, "H. Mushi", "Next drop DSP-2026-1152");
        Leg("JOB-2026-00481", "T", "TRQ-2026-0144", "In transit", 55, "J. Kimaro", "1 truck held at Tunduma");
        Leg("JOB-2026-00483", "C", "CLR-2026-0931", "Documents received", 5, "A. Said", "Entry not yet lodged");
        Leg("JOB-2026-00480", "D", "DPL-2026-0222", "1 of 2 delivered", 70, "H. Mushi", "Courier van T 118 DNR");
        Leg("JOB-2026-00479", "C", "CLR-2026-0920", "Completed", 100, "A. Said", "T1 released 10 Nov");
        Leg("JOB-2026-00479", "T", "TRQ-2026-0187", "In transit", 15, "J. Kimaro", "1 of 2 trucks dispatched");
        Leg("JOB-2026-00478", "C", "CLR-2026-0925", "Completed", 100, "A. Said", "T1 released 09 Nov");
        Leg("JOB-2026-00478", "T", "TRQ-2026-0186", "Planning", 0, "J. Kimaro", "2 trucks to assign");
        Leg("JOB-2026-00476", "C", "CLR-2026-0924", "Completed", 100, "A. Said", "Duty paid on behalf, released");
        Leg("JOB-2026-00476", "W", "WHR-2026-0381", "Completed", 100, "M. Juma", "1,200 bags received and released");
        Leg("JOB-2026-00476", "D", "DPL-2026-0220", "Out for delivery", 40, "H. Mushi", "3 drops in Mbeya region");
        Leg("JOB-2026-00474", "W", "WHR-2026-0366", "In storage", 50, "M. Juma", "48 pallets in stock, 24 expected");
        Leg("JOB-2026-00472", "C", "CLR-2026-0927", "Documents received", 25, "A. Said", "T1 bond pending");
        Leg("JOB-2026-00472", "T", "TRQ-2026-0189", "Planning", 0, "J. Kimaro", "FM-2026-0310 draft");
        Leg("JOB-2026-00468", "W", "WHR-2026-0352", "In storage", 45, "M. Juma", "78 of 140 pallets in stock");
        Leg("JOB-2026-00468", "D", "DPL-2026-0219", "Picking", 30, "H. Mushi", "26 pallets for 4 stores");
        Leg("JOB-2026-00465", "C", "CLR-2026-0918", "Completed", 100, "A. Said", "Collected by consignee");
        Leg("JOB-2026-00463", "T", "TRQ-2026-0131", "Completed", 100, "J. Kimaro", "Delivered Lusaka 17 Oct");

        // ---------- timeline ----------
        void Ev(string job, DateOnly d, string unit, string title, string detail, bool done = true) =>
            db.Timeline.Add(new TimelineEvent { JobId = job, Date = d, Unit = unit, Title = title, Detail = detail, Done = done });
        Ev("JOB-2026-00481", D(10, 28), "F", "File opened from quotation QT-2026-0312", "Kilimanjaro Mining Company · 5 × 40′ · split: 3 transit to Lusaka, 2 local");
        Ev("JOB-2026-00481", D(11, 2), "C", "Vessel arrived Dar es Salaam (ATA)", "MV Kilwa Star · BL MSKU-DAR-2611042");
        Ev("JOB-2026-00481", D(11, 4), "C", "Entries lodged", "Home consumption 2026-DAR-118402 (2 cont.) · Transit T1 2026-DAR-T-55120 (3 cont.)");
        Ev("JOB-2026-00481", D(11, 6), "C", "Home consumption entry released → hand-over to Warehousing", "Automatic inbound advice to WH-DAR-01");
        Ev("JOB-2026-00481", D(11, 7), "W", "2 × 40′ received and destuffed", "GRN WHR-2026-0377 · 86 pallets · bins A-03 to A-07");
        Ev("JOB-2026-00481", D(11, 7), "C", "Transit entry released → hand-over to Transport", "Truck request TRQ-2026-0144 for 3 × 40′");
        Ev("JOB-2026-00481", D(11, 7), "T", "Filling masters FM-2026-0306 → 0308 approved and disbursed", "Route DSM-LUN-TUN standard per truck · Ops manager S. Massawe · Finance L. Mrema");
        Ev("JOB-2026-00481", D(11, 8), "T", "3 trucks dispatched", "SO-E2E-0008 T 928 E2E · SO-E2E-0009 T 929 E2E · SO-E2E-0010 T 930 E2E");
        Ev("JOB-2026-00481", D(11, 9), "D", "Delivery 1 delivered · POD captured", "DSP-2026-1150 · 16 pallets · Kurasini depot");
        Ev("JOB-2026-00481", D(11, 10), "D", "Delivery 2 delivered · POD captured", "DSP-2026-1151 · 18 pallets · Mikocheni site");
        Ev("JOB-2026-00481", D(11, 10), "T", "T 928 E2E crossed Tunduma → Nakonde", "GPS event · border time 6 h 40 min");
        Ev("JOB-2026-00481", D(11, 11), "T", "T 929 E2E held at Tunduma — missing ZRA entry copy", "Exception raised · owner J. Kimaro");
        Ev("JOB-2026-00481", D(11, 12), "D", "Delivery 3 scheduled", "DSP-2026-1152 · 18 pallets", false);
        Ev("JOB-2026-00481", D(11, 14), "T", "Expected delivery Lusaka (ETA)", "Final POD closes transport leg", false);
        Ev("JOB-2026-00481", D(11, 15), "F", "Consolidated invoice to SAP Business One", "After last POD; storage billed to date", false);
        foreach (var j in jobs.Where(j => j.Id != "JOB-2026-00481"))
            Ev(j.Id, j.OpenedOn, "F", "File opened", $"{cust.First(c => c.Id == j.CustomerId).Name} · {j.Cargo}");
        Ev("JOB-2026-00479", D(11, 10), "C", "Transit entry released → hand-over to Transport", "CLR-2026-0920 · T1 2026-DAR-T-55388");
        Ev("JOB-2026-00479", D(11, 10), "T", "Truck dispatched · SO-2026-0015", "T 903 DEM · E. Mwakyusa");
        Ev("JOB-2026-00476", D(11, 8), "C", "Duty paid on behalf · released", "EXP-2026-0309 · PV-2026-0608");
        Ev("JOB-2026-00465", D(10, 24), "C", "Gated out · collected by consignee", "CLR-2026-0918");
        Ev("JOB-2026-00463", D(10, 17), "T", "Delivered Lusaka · POD captured", "SO-2026-0009");
        Ev("JOB-2026-00463", D(10, 20), "B", "Invoice raised · INV 30377", "Posted to SAP Business One");

        // ---------- documents ----------
        void Doc(string job, string unit, string name, string reference, string status) =>
            db.JobDocuments.Add(new JobDocument { JobId = job, Unit = unit, Name = name, Reference = reference, Status = status });
        Doc("JOB-2026-00481", "C", "Bill of lading", "MSKU-DAR-2611042", "Received");
        Doc("JOB-2026-00481", "C", "Commercial invoice & packing list", "KMC-PO-77120", "Received");
        Doc("JOB-2026-00481", "C", "Home consumption entry", "2026-DAR-118402", "Released");
        Doc("JOB-2026-00481", "C", "Transit entry (T1)", "2026-DAR-T-55120", "Released");
        Doc("JOB-2026-00481", "C", "Port release order", "TPA-RO-4410876", "Received");
        Doc("JOB-2026-00481", "W", "Goods received note", "WHR-2026-0377", "Generated");
        Doc("JOB-2026-00481", "D", "Proof of delivery ×2", "DSP-2026-1150 / 1151", "Signed");
        Doc("JOB-2026-00481", "T", "Waybills ×3", "SO-E2E-0008 / 0009 / 0010", "Generated");
        Doc("JOB-2026-00481", "T", "ZRA entry copy — T 929 E2E", "Border", "Missing");
        Doc("JOB-2026-00481", "T", "Proof of delivery Lusaka ×3", "—", "Pending");
        Doc("JOB-2026-00483", "C", "Bill of lading", "ONEU-5531288", "Received");
        Doc("JOB-2026-00483", "C", "Import permit", "TBS / TFDA", "Missing");
        Doc("JOB-2026-00472", "C", "Transit bond (T1)", "Bond pending with insurer", "Missing");

        // ---------- charges ----------
        void Ch(string job, string unit, string desc, ChargeKind kind, decimal rev, decimal cost, string status, string? inv = null) =>
            db.Charges.Add(new Charge { JobId = job, Unit = unit, Description = desc, Kind = kind, Revenue = rev, Cost = cost, Status = status, InvoiceNo = inv });
        var R = ChargeKind.Revenue; var X = ChargeKind.Disbursement;
        Ch("JOB-2026-00481", "C", "Agency fee — 2 customs entries", R, 1500, 0, "Ready");
        Ch("JOB-2026-00481", "C", "Customs documentation — advance", R, 250, 0, "Billed", "30395");
        Ch("JOB-2026-00481", "C", "Port & shipping line charges (paid on behalf)", X, 0, 3850, "Ready");
        Ch("JOB-2026-00481", "W", "Destuffing & handling 2 × 40′", R, 360, 180, "Ready");
        Ch("JOB-2026-00481", "W", "Storage 86 pallets, to date", R, 465, 0, "Accruing");
        Ch("JOB-2026-00481", "D", "Local delivery DSP-2026-1150, 1151", R, 360, 140, "Ready");
        Ch("JOB-2026-00481", "D", "Local delivery DSP-2026-1152", R, 180, 70, "Accruing");
        Ch("JOB-2026-00481", "T", "Transit freight Dar → Lusaka × 3", R, 10200, 0, "Awaiting POD");
        Ch("JOB-2026-00481", "T", "Border agency & escort × 3", R, 450, 630, "Awaiting POD");
        Ch("JOB-2026-00481", "T", "Tyre & maintenance allocation × 3", R, 0, 1200, "—");
        Ch("JOB-2026-00479", "C", "Agency fee — 1 transit entry", R, 750, 0, "Ready");
        Ch("JOB-2026-00479", "C", "ICD charges KICD (paid by client)", R, 0, 0, "—");
        Ch("JOB-2026-00479", "T", "Transit freight Dar → Lusaka × 2 (20′)", R, 5200, 0, "Awaiting POD");
        Ch("JOB-2026-00478", "C", "Agency fee — 1 transit entry", R, 750, 0, "Ready");
        Ch("JOB-2026-00478", "T", "Transit freight Dar → Kasumbalesa × 2", R, 7200, 0, "Awaiting POD");
        Ch("JOB-2026-00476", "C", "Customs documentation", R, 250, 0, "Billed", "30402");
        Ch("JOB-2026-00476", "C", "Agency fee — home consumption entry", R, 750, 0, "Ready");
        Ch("JOB-2026-00476", "W", "Storage booking deposit — 1,200 bags", R, 150, 0, "Billed", "30402");
        Ch("JOB-2026-00476", "W", "Handling 1,200 bags in/out", R, 480, 160, "Ready");
        Ch("JOB-2026-00476", "D", "Distribution Mbeya region × 3 drops", R, 1350, 520, "Accruing");
        Ch("JOB-2026-00474", "W", "Storage 48 pallets, to date", R, 216, 0, "Accruing");
        Ch("JOB-2026-00468", "W", "Storage 140 pallets, to date", R, 1323, 0, "Accruing");
        Ch("JOB-2026-00468", "W", "Picking & handling", R, 420, 120, "Ready");
        Ch("JOB-2026-00468", "D", "Store deliveries × 4", R, 720, 260, "Accruing");
        Ch("JOB-2026-00465", "C", "Agency fee — home consumption entry", R, 750, 0, "Ready");
        Ch("JOB-2026-00465", "C", "Port storage (TZS, paid on behalf)", X, 0, 285, "Ready");
        Ch("JOB-2026-00480", "D", "Courier deliveries × 12 parcels", R, 240, 45, "Accruing");
        Ch("JOB-2026-00463", "T", "Freight Dar → Lusaka 30 t", R, 3400, 0, "Billed", "30377");

        // ---------- hand-overs ----------
        void Ho(string unit, string job, string from, string title, string note, string state, DateTimeOffset at) =>
            db.Handoffs.Add(new Handoff { Unit = unit, JobId = job, FromUnit = from, Title = title, Note = note, State = state, CreatedAt = at, AcceptedAt = state == "Waiting" ? null : at.AddHours(1), AcceptedBy = state == "Waiting" ? null : "M. Juma" });
        Ho("W", "JOB-2026-00481", "C", "Receive 2 × 40′ into WH-DAR-01", "Released from customs 06 Nov · home consumption entry", "Done", T(11, 6, 10, 0));
        Ho("W", "JOB-2026-00476", "C", "Receive 1,200 bags fertiliser", "Release order issued 10 Nov · 1 × 40′ + 1 × 20′", "Waiting", T(11, 10, 11, 0));
        Ho("D", "JOB-2026-00481", "W", "Deliver 18 pallets to Kurasini depot", "Warehouse release WRL-2026-0611 · due 12 Nov", "Waiting", T(11, 11, 8, 0));
        Ho("D", "JOB-2026-00468", "W", "Deliver 26 pallets to 4 Bahari stores", "Picking complete · due 11 Nov", "Waiting", T(11, 10, 16, 0));
        Ho("T", "JOB-2026-00479", "C", "Assign truck — Dar → Lusaka", "T1 released 10 Nov · 2 × 20′ · 24 t", "Waiting", T(11, 10, 15, 0));
        Ho("T", "JOB-2026-00472", "C", "Plan 3 trucks — Dar → Lusaka", "Expected release 13 Nov · plan only", "Waiting", T(11, 9, 9, 0));
        Ho("C", "JOB-2026-00483", "F", "Lodge entry — clearing-only file", "Customer collects at port · permit pending", "Waiting", T(11, 10, 12, 0));
        Ho("B", "JOB-2026-00465", "C", "Raise invoice — 1 cost line unbilled", "Port storage TZS cost has no revenue line", "Waiting", T(11, 9, 14, 0));

        // ---------- clearing ----------
        void Clr(string id, string job, string entry, string type, string cont, int stage, string next) =>
            db.ClearingEntries.Add(new ClearingEntry { Id = id, JobId = job, EntryNo = entry, EntryType = type, Containers = cont, Stage = stage, NextUnit = next, HandedOver = stage >= ClearingEntry.ReleasedStage });
        Clr("CLR-2026-0912", "JOB-2026-00481", "2026-DAR-118402", "Home consumption", "2 × 40′", 5, "W");
        Clr("CLR-2026-0913", "JOB-2026-00481", "2026-DAR-T-55120", "Transit (T1)", "3 × 40′", 5, "T");
        Clr("CLR-2026-0920", "JOB-2026-00479", "2026-DAR-T-55388", "Transit (T1)", "2 × 20′", 5, "T");
        Clr("CLR-2026-0924", "JOB-2026-00476", "2026-DAR-120117", "Home consumption", "1 × 40′ + 1 × 20′", 4, "W");
        Clr("CLR-2026-0925", "JOB-2026-00478", "2026-DAR-T-55412", "Transit (T1)", "2 × 40′", 5, "T");
        Clr("CLR-2026-0927", "JOB-2026-00472", "2026-DAR-T-55601", "Transit (T1)", "3 × 40′", 1, "T");
        Clr("CLR-2026-0918", "JOB-2026-00465", "2026-DAR-119560", "Home consumption", "1 × 20′", 5, "X");
        Clr("CLR-2026-0931", "JOB-2026-00483", "Not yet lodged", "Home consumption", "1 × 20′", 0, "X");

        // ---------- containers ----------
        void Box(string job, string clr, string owner, string serial, string size, string status, DateOnly disch, (DateOnly, string)[] evs, DateOnly? returned = null, string? depot = null)
        {
            var no = owner + serial + Iso6346.CheckDigit(owner + serial);
            var c = new Container { JobId = job, ClearingId = clr, Number = no, Size = size, Status = status, DischargedOn = disch, ReturnedOn = returned, Depot = depot };
            c.Events.AddRange(evs.Select(e => new ContainerEvent { Date = e.Item1, Text = e.Item2 }));
            db.Containers.Add(c);
        }
        var port = "Discharged at Dar es Salaam port";
        Box("JOB-2026-00481", "CLR-2026-0912", "MSKU", "774131", "40′ GP", "Empty returned", D(11, 2), [(D(11, 2), port), (D(11, 6), "Gated out of port (released)"), (D(11, 7), "Delivered to warehouse WH-DAR-01 · destuffed"), (D(11, 9), "Empty returned to Kurasini empty depot · EIR 77120")], D(11, 9), "Kurasini empty depot");
        Box("JOB-2026-00481", "CLR-2026-0912", "MSKU", "774144", "40′ GP", "Destuffed · awaiting empty return", D(11, 2), [(D(11, 2), port), (D(11, 6), "Gated out of port (released)"), (D(11, 7), "Delivered to warehouse WH-DAR-01 · destuffed")]);
        Box("JOB-2026-00481", "CLR-2026-0913", "MSKU", "774102", "40′ GP", "On truck · SO-E2E-0008", D(11, 2), [(D(11, 2), port), (D(11, 7), "Gated out of port (released, transit)"), (D(11, 8), "Loaded on T 928 E2E · SO-E2E-0008")]);
        Box("JOB-2026-00481", "CLR-2026-0913", "MSKU", "774115", "40′ GP", "On truck · SO-E2E-0009", D(11, 2), [(D(11, 2), port), (D(11, 7), "Gated out of port (released, transit)"), (D(11, 8), "Loaded on T 929 E2E · SO-E2E-0009")]);
        Box("JOB-2026-00481", "CLR-2026-0913", "MSKU", "774128", "40′ GP", "On truck · SO-E2E-0010", D(11, 2), [(D(11, 2), port), (D(11, 7), "Gated out of port (released, transit)"), (D(11, 8), "Loaded on T 930 E2E · SO-E2E-0010")]);
        Box("JOB-2026-00479", "CLR-2026-0920", "COSU", "640218", "20′ GP", "On truck · SO-2026-0015", D(11, 3), [(D(11, 3), port), (D(11, 10), "Released to Kilimanjaro ICD (KICD)"), (D(11, 10), "ICD charges paid · loaded on T 903 DEM · SO-2026-0015")]);
        Box("JOB-2026-00479", "CLR-2026-0920", "COSU", "640225", "20′ GP", "Awaiting truck", D(11, 3), [(D(11, 3), port), (D(11, 10), "Released to Kilimanjaro ICD (KICD)"), (D(11, 10), "ICD charges paid · waiting for FM-2026-0311")]);
        Box("JOB-2026-00476", "CLR-2026-0924", "MAEU", "239507", "40′ GP", "Gated out of port", D(11, 5), [(D(11, 5), port), (D(11, 10), "Gated out of port (released direct)")]);
        Box("JOB-2026-00476", "CLR-2026-0924", "MAEU", "239512", "20′ GP", "Gated out of port", D(11, 5), [(D(11, 5), port), (D(11, 10), "Gated out of port (released direct)")]);
        Box("JOB-2026-00472", "CLR-2026-0927", "CMDU", "881204", "40′ GP", "At port", D(11, 6), [(D(11, 6), port)]);
        Box("JOB-2026-00472", "CLR-2026-0927", "CMDU", "881211", "40′ GP", "At port", D(11, 6), [(D(11, 6), port)]);
        Box("JOB-2026-00472", "CLR-2026-0927", "CMDU", "881226", "40′ GP", "At port", D(11, 6), [(D(11, 6), port)]);
        Box("JOB-2026-00465", "CLR-2026-0918", "ONEU", "553091", "20′ GP", "Empty returned", D(10, 20), [(D(10, 20), port), (D(10, 24), "Gated out · collected by consignee"), (D(10, 26), "Empty returned to Ubungo empty depot · EIR 76410")], D(10, 26), "Ubungo empty depot");
        Box("JOB-2026-00478", "CLR-2026-0925", "MAEU", "239514", "40′ GP", "Awaiting truck", D(11, 5), [(D(11, 5), port), (D(11, 9), "Gated out of port (released, transit)")]);
        Box("JOB-2026-00478", "CLR-2026-0925", "MAEU", "239521", "40′ GP", "Awaiting truck", D(11, 5), [(D(11, 5), port), (D(11, 9), "Gated out of port (released, transit)")]);
        Box("JOB-2026-00483", "CLR-2026-0931", "ONEU", "553140", "20′ GP", "At port", D(11, 9), [(D(11, 9), port)]);

        // ---------- routes ----------
        db.Routes.AddRange(
            new Route { Code = "DSM-LUN-TUN", Name = "Dar es Salaam → Lusaka", Via = "Morogoro · Iringa · Mbeya · Tunduma/Nakonde border", OutKm = 1950, ReturnKm = 1950, Days = 5, Type = "Transit", VehicleClass = "Horse 6×4 + 40′ trailer", Toll = 85, Borders = 2, Escort = false, EffectiveFrom = D(10, 1), ReviewOn = D(12, 31), Version = 3 },
            new Route { Code = "DSM-KAS-MOK", Name = "Dar es Salaam → Kasumbalesa", Via = "Tunduma · Mokambo border", OutKm = 2010, ReturnKm = 2010, Days = 6, Type = "Cross-border", VehicleClass = "Horse 6×4 + 40′ trailer", Toll = 95, Borders = 2, Escort = true, EffectiveFrom = D(10, 1), ReviewOn = D(12, 31), Version = 2 },
            new Route { Code = "DSM-KGL-RUS", Name = "Dar es Salaam → Kigali", Via = "Dodoma · Rusumo border", OutKm = 1460, ReturnKm = 1460, Days = 4, Type = "Transit", VehicleClass = "Horse 6×4 + 20′/40′ trailer", Toll = 70, Borders = 2, Escort = false, EffectiveFrom = D(9, 1), ReviewOn = D(11, 30), Version = 4 },
            new Route { Code = "DSM-LOC-10T", Name = "Dar es Salaam local", Via = "Port · Kurasini · Mikocheni · Mbezi", OutKm = 60, ReturnKm = 60, Days = 1, Type = "Local", VehicleClass = "Rigid 10 t", Toll = 0, Borders = 0, Escort = false, EffectiveFrom = D(10, 1), ReviewOn = new DateOnly(2027, 3, 31), Version = 1 });

        // ---------- fleet ----------
        Vehicle V(string reg, string fleet, string type, string status, string? driver, string? trailer, int? odo, int? svc, bool sub = false) =>
            new() { Reg = reg, FleetNo = fleet, Type = type, Status = status, Driver = driver, Trailer = trailer, OdometerKm = odo, NextServiceKm = svc, Subcontracted = sub };
        var vehicles = new[]
        {
            V("T 928 E2E", "FL-028", "Horse 6×4", "On trip", "P. Banda", "T 441 DLB", 412880, 420000),
            V("T 929 E2E", "FL-029", "Horse 6×4", "On trip", "S. Ngowi", "T 442 DLB", 388140, 390000),
            V("T 930 E2E", "FL-030", "Horse 6×4", "On trip", "K. Phiri", "T 451 DLB", 301560, 310000),
            V("T 931 E2E", "FL-031", "Horse 6×4", "In workshop", null, "T 448 DLB", 356900, 360000),
            V("T 932 E2E", "FL-032", "Horse 6×4", "Available", "D. Lyimo", "T 455 DLB", 244310, 250000),
            V("T 933 E2E", "FL-033", "Horse 6×4", "Available", "M. Tembo", "T 445 DLB", 238410, 250000),
            V("T 934 E2E", "FL-034", "Horse 6×4", "Available", "B. Shirima", "T 452 DLB", 248800, 260000),
            V("T 903 DEM", "SUB-03", "Horse 6×4 (subcontracted)", "On trip", "E. Mwakyusa", "T 461 DLB", null, null, true),
            V("T 905 DEM", "SUB-05", "Horse 6×4 (subcontracted)", "Available", "J. Nyirenda", "T 463 DLB", null, null, true),
            V("T 412 DLX", "FL-112", "Rigid 10 t", "Available", "H. Mushi", null, 98420, 100000),
            V("T 207 DMK", "FL-107", "Rigid 7 t", "Assigned", "A. Kombo", null, 61200, 70000),
            V("T 356 DKQ", "FL-115", "Rigid 10 t", "Available", "S. Mrema", null, 142060, 150000),
            V("T 118 DNR", "FL-118", "Van 1.5 t (courier)", "On trip", "J. Mallya", null, 54310, 60000)
        };
        db.Vehicles.AddRange(vehicles);
        var drivers = new (string, string)[]
        {
            ("D. Lyimo", "Licence valid to Mar 2028"), ("M. Tembo", "Licence valid to Aug 2027"), ("A. Mfinanga", "Licence valid to Jan 2027"),
            ("E. Mwakyusa", "Subcontractor driver"), ("J. Nyirenda", "Subcontractor driver"), ("B. Shirima", "Licence valid to Jun 2028"),
            ("P. Banda", ""), ("S. Ngowi", ""), ("K. Phiri", ""), ("H. Mushi", "Rigid only"), ("A. Kombo", "Rigid only"), ("S. Mrema", "Rigid only"), ("J. Mallya", "Courier van")
        };
        db.Drivers.AddRange(drivers.Select(d => new Driver { Name = d.Item1, Note = d.Item2 }));

        // Compliance dates are relative to today so the expired / expiring states stay meaningful whenever the stack is started.
        var today = Clock.Today;
        string[] vdocs = ["Road licence", "Insurance", "COMESA yellow card", "LATRA transport licence", "Inspection certificate", "Speed governor cert"];
        string[] ddocs = ["Driving licence (Class E)", "Passport", "Medical certificate", "Defensive driving cert"];
        for (var i = 0; i < vehicles.Length; i++)
        {
            var v = vehicles[i]; var rigid = v.Type.StartsWith("Rigid") || v.Type.StartsWith("Van");
            for (var k = 0; k < vdocs.Length; k++)
                db.ComplianceDocs.Add(new ComplianceDoc
                {
                    SubjectType = "Vehicle", SubjectId = v.Reg, DocType = vdocs[k], Reference = $"{vdocs[k][..3].ToUpperInvariant()}-{4400 + i * 10 + k}",
                    ExpiresOn = rigid && vdocs[k] == "COMESA yellow card" ? null : today.AddDays(60 + (i * 53 + k * 97) % 420)
                });
        }
        for (var i = 0; i < drivers.Length; i++)
            for (var k = 0; k < ddocs.Length; k++)
                db.ComplianceDocs.Add(new ComplianceDoc { SubjectType = "Driver", SubjectId = drivers[i].Item1, DocType = ddocs[k], Reference = $"DRV-{7100 + i * 10 + k}", ExpiresOn = today.AddDays(90 + (i * 71 + k * 113) % 600) });
        await db.SaveChangesAsync();
        async Task Expiry(string subject, string doc, int days) => (await db.ComplianceDocs.FirstAsync(d => d.SubjectId == subject && d.DocType == doc)).ExpiresOn = today.AddDays(days);
        await Expiry("T 929 E2E", "Insurance", 14);
        await Expiry("T 931 E2E", "Inspection certificate", -11);
        await Expiry("T 934 E2E", "COMESA yellow card", 24);
        await Expiry("T 903 DEM", "COMESA yellow card", 9);
        await Expiry("J. Nyirenda", "Driving licence (Class E)", 19);
        await Expiry("A. Mfinanga", "Medical certificate", -27);
        await Expiry("S. Ngowi", "Passport", 28);

        // ---------- filling masters & trips ----------
        var lun = await db.Routes.FindAsync("DSM-LUN-TUN");
        FillingMaster Fm(string id, string job, string tr, string truck, string trailer, string driver, string status, string? payRef, string? so, Dictionary<string, (decimal, string)>? over = null)
        {
            var f = new FillingMaster { Id = id, JobId = job, TruckRequest = tr, Truck = truck, Trailer = trailer, Driver = driver, RouteCode = "DSM-LUN-TUN", Status = status, PayRef = payRef, ShipmentOrderId = so };
            f.Lines.AddRange(RouteRules.StandardLines(lun!).Select(l => new FmLine
            {
                FillingMasterId = id, Code = l.Code, Name = l.Name, Basis = l.Basis, Qty = l.Qty, Rate = l.Rate, UnitLabel = l.Unit, Standard = l.Standard,
                Requested = over != null && over.TryGetValue(l.Code, out var o) ? o.Item1 : l.Standard, Reason = over != null && over.TryGetValue(l.Code, out var o2) ? o2.Item2 : ""
            }));
            return f;
        }
        db.FillingMasters.AddRange(
            Fm("FM-2026-0311", "JOB-2026-00479", "TR-2026-0187", "T 905 DEM", "T 463 DLB", "J. Nyirenda", FmStatus.Pending, null, null,
                new() { ["FUEL"] = (1370, "Diesel price review 01 Nov; 20′ pair runs heavier"), ["CONT"] = (180, "Two border crossings in one week") }),
            Fm("FM-2026-0310", "JOB-2026-00472", "TR-2026-0189", "T 932 E2E", "T 455 DLB", "D. Lyimo", FmStatus.Draft, null, null),
            Fm("FM-2026-0308", "JOB-2026-00481", "TR-2026-0144", "T 930 E2E", "T 451 DLB", "K. Phiri", FmStatus.Converted, "EFT-77120 / FC-5531", "SO-E2E-0010"),
            Fm("FM-2026-0307", "JOB-2026-00481", "TR-2026-0144", "T 929 E2E", "T 442 DLB", "S. Ngowi", FmStatus.Converted, "EFT-77119 / FC-5530", "SO-E2E-0009"),
            Fm("FM-2026-0306", "JOB-2026-00481", "TR-2026-0144", "T 928 E2E", "T 441 DLB", "P. Banda", FmStatus.Converted, "EFT-77118 / FC-5529", "SO-E2E-0008"));
        void Audit(string entity, string id, DateTimeOffset at, string actor, string text) => db.Activity.Add(new ActivityLog { Entity = entity, EntityId = id, At = at, Actor = actor, Text = text });
        Audit("FillingMaster", "FM-2026-0311", T(11, 10, 15, 40), "J. Kimaro (clerk)", "Created from truck request TR-2026-0187; expense lines loaded from route DSM-LUN-TUN v3");
        Audit("FillingMaster", "FM-2026-0311", T(11, 10, 15, 52), "J. Kimaro (clerk)", "Fuel raised to $1,370 and contingency to $180; submitted for approval");
        Audit("FillingMaster", "FM-2026-0310", T(11, 9, 10, 12), "J. Kimaro (clerk)", "Created from hand-over; expense lines loaded from route DSM-LUN-TUN v3");
        foreach (var (id, so) in new[] { ("FM-2026-0306", "SO-E2E-0008"), ("FM-2026-0307", "SO-E2E-0009"), ("FM-2026-0308", "SO-E2E-0010") })
        {
            Audit("FillingMaster", id, T(11, 7, 11, 5), "J. Kimaro (clerk)", "Created from hand-over (T1 released)");
            Audit("FillingMaster", id, T(11, 7, 12, 20), "S. Massawe (ops-manager)", "Approved — no variance");
            Audit("FillingMaster", id, T(11, 7, 14, 2), "L. Mrema (finance)", "Disbursed");
            Audit("FillingMaster", id, T(11, 8, 6, 30), "J. Kimaro (clerk)", $"Converted to shipment order {so}");
        }

        ShipmentOrder So(string id, string job, string? fm, string truck, string driver, int pct, string place, int speed, string eta, string st, int gps, int std) =>
            new() { Id = id, JobId = job, FillingMasterId = fm, Truck = truck, Driver = driver, RouteCode = "DSM-LUN-TUN", ProgressPct = pct, Place = place, SpeedKmh = speed, Eta = eta, Status = st, GpsKm = gps, StandardKm = std };
        db.ShipmentOrders.AddRange(
            So("SO-E2E-0008", "JOB-2026-00481", "FM-2026-0306", "T 928 E2E", "P. Banda", 70, "Mpika, Zambia", 64, "13 Nov 16:00", "On route", 1371, 1950),
            So("SO-E2E-0010", "JOB-2026-00481", "FM-2026-0308", "T 930 E2E", "K. Phiri", 43, "Mbeya", 71, "14 Nov 10:00", "On route", 846, 1950),
            So("SO-E2E-0009", "JOB-2026-00481", "FM-2026-0307", "T 929 E2E", "S. Ngowi", 48, "Tunduma border", 0, "14 Nov 20:00 (+1 day)", "Stopped", 941, 1950),
            So("SO-2026-0015", "JOB-2026-00479", null, "T 903 DEM", "E. Mwakyusa", 9, "Near Morogoro", 92, "16 Nov 12:00", "Speeding", 181, 1950),
            new ShipmentOrder { Id = "SO-2026-0009", JobId = "JOB-2026-00463", Truck = "T 932 E2E", Driver = "D. Lyimo", RouteCode = "DSM-LUN-TUN", ProgressPct = 100, Place = "Lusaka", Status = "Delivered", GpsKm = 1950, StandardKm = 1950, Eta = "17 Oct", DeliveredAt = T(10, 17, 14, 0), PodSignedBy = "C. Mwale (KMC Lusaka)" });
        void Alert(string sev, string kind, string text, string so, string job, DateTimeOffset at) =>
            db.TrackingAlerts.Add(new TrackingAlert { Severity = sev, Kind = kind, Text = text, ShipmentOrderId = so, JobId = job, At = at });
        Alert("bad", "Extended stop", "T 929 E2E stopped 9 h inside Tunduma border geofence", "SO-E2E-0009", "JOB-2026-00481", T(11, 11, 5, 10));
        Alert("warn", "Speeding", "T 903 DEM at 92 km/h (limit 80) near Morogoro", "SO-2026-0015", "JOB-2026-00479", T(11, 11, 13, 48));
        Alert("warn", "Route deviation", "T 930 E2E left corridor 6 km into Mbeya town, 45 min unscheduled stop", "SO-E2E-0010", "JOB-2026-00481", T(11, 11, 9, 22));
        Alert("info", "Geofence", "T 928 E2E entered Zambia at Nakonde", "SO-E2E-0008", "JOB-2026-00481", T(11, 10, 18, 5));

        // ---------- expenses ----------
        Expense E(string id, string job, string unit, string type, string payee, decimal amt, string reference, bool rech, string notes, string st, string? pv, string by, DateOnly d) =>
            new() { Id = id, JobId = job, Unit = unit, Type = type, Payee = payee, Amount = amt, Reference = reference, Rechargeable = rech, Notes = notes, Status = st, PaymentVoucher = pv, RequestedBy = by, Date = d };
        db.Expenses.AddRange(
            E("EXP-2026-0309", "JOB-2026-00476", "C", "Customs duty & taxes", "Tanzania Revenue Authority", 6420, "Assessment 2026-DAR-120117", true, "Duty and VAT on fertiliser · paid on behalf from CLR-2026-0924", ExpStatus.Paid, "PV-2026-0608", "R. Mollel", D(11, 8)),
            E("EXP-2026-0308", "JOB-2026-00468", "W", "Casual labour", "Kurasini casual labour team", 120, "Muster roll 10-11 Nov", false, "Picking 26 pallets for 4 stores", ExpStatus.PendingHead, null, "Warehouse clerk", D(11, 11)),
            E("EXP-2026-0307", "JOB-2026-00479", "C", "Shipping line DO / container deposit", "Bahari Shipping Agencies", 180, "DO fee COSU-6402188", true, "Delivery order for 2 × 20′", ExpStatus.Approved, null, "R. Mollel", D(11, 10)),
            E("EXP-2026-0306", "JOB-2026-00481", "T", "Border clearance fees", "Nakonde clearing agent", 1240, "NCA-2211", false, "Re-lodging ZRA entry copy for T 929 E2E", ExpStatus.PendingFinance, null, "J. Kimaro", D(11, 11)),
            E("EXP-2026-0305", "JOB-2026-00481", "W", "Forklift hire", "Mzizima Equipment Hire", 90, "INV MEH-2210", false, "Destuffing 2 × 40′", ExpStatus.Paid, "PV-2026-0614", "M. Juma", D(11, 7)),
            E("EXP-2026-0303", "JOB-2026-00480", "D", "Fuel top-up", "Kurasini service station", 45, "Receipt 88120", false, "T 118 DNR", ExpStatus.Paid, "PV-2026-0621", "H. Mushi", D(11, 10)),
            E("EXP-2026-0301", "JOB-2026-00481", "C", "Port charges (TPA)", "Tanzania Ports Authority", 3850, "TPA-RO-4410876", true, "Wharfage and handling 5 × 40′ (billed via disbursement line)", ExpStatus.Paid, "PV-2026-0611", "R. Mollel", D(11, 5)));
        Audit("Expense", "EXP-2026-0308", T(11, 11, 8, 15), "Warehouse clerk (clerk)", "Submitted · USD 120.00");
        Audit("Expense", "EXP-2026-0306", T(11, 11, 9, 40), "J. Kimaro (clerk)", "Submitted · USD 1,240.00");
        Audit("Expense", "EXP-2026-0306", T(11, 11, 10, 5), "S. Massawe (unit-head)", "Approved by unit head · over $1,000, sent to Finance");
        Audit("Expense", "EXP-2026-0307", T(11, 10, 16, 30), "A. Said (unit-head)", "Approved by unit head");

        // ---------- invoices ----------
        Invoice Inv(string no, DateOnly d, string c, string sbu, string status, string sap, (string Unit, string Job, string Desc, decimal Amt)[] lines)
        {
            var i = new Invoice { No = no, Date = d, CustomerId = C(c), Sbu = sbu, Status = status, SapStatus = "Posted", SapDocNo = sap };
            i.Lines.AddRange(lines.Select(l => new InvoiceLine { InvoiceNo = no, Unit = l.Unit, JobId = l.Job, Description = l.Desc, Amount = l.Amt }));
            return i;
        }
        db.Invoices.AddRange(
            Inv("30377", D(10, 20), "Kilimanjaro Mining Company", "T", "Paid", "A/R 30377", [("T", "JOB-2026-00463", "Freight Dar → Lusaka 30 t", 3400)]),
            Inv("30395", D(10, 31), "Kilimanjaro Mining Company", "C", "Paid", "A/R 30395", [("C", "JOB-2026-00481", "Customs documentation — advance", 250)]),
            Inv("30402", D(11, 5), "Mbeya Agro Supplies", "ALL", "Open", "A/R 30402", [("C", "JOB-2026-00476", "Customs documentation", 250), ("W", "JOB-2026-00476", "Storage booking deposit — 1,200 bags", 150)]));

        // ---------- workshop ----------
        db.Parts.AddRange(new (string, string, string, int, decimal, int)[]
        {
            ("BRK-CH-24", "Brake chamber type 24", "S-01-03", 2, 85, 3), ("AIR-RLV", "Air relay valve", "S-01-05", 4, 64, 2), ("BRK-LIN", "Brake lining kit (per axle)", "S-01-08", 6, 120, 4),
            ("HSE-AIR", "Air hose 1 m", "S-02-01", 15, 12, 6), ("BLT-FAN", "Fan belt", "S-02-04", 4, 27, 3), ("FLT-OIL", "Oil filter", "S-03-01", 12, 18, 6), ("FLT-AIR", "Air filter element", "S-03-02", 2, 46, 4),
            ("LMP-H7", "Headlamp bulb H7 24V", "S-04-02", 20, 4, 10), ("LMP-IND", "Indicator lamp unit", "S-04-05", 6, 15, 4), ("WPR-24", "Wiper blade 24\"", "S-05-01", 10, 9, 6), ("MIR-GL", "Mirror glass", "S-05-03", 5, 22, 3),
            ("SHK-ABS", "Shock absorber", "S-06-02", 4, 95, 2), ("TYR-315", "Tyre 315/80R22.5", "T-01", 8, 310, 6), ("TYR-385", "Tyre 385/65R22.5", "T-02", 3, 345, 4), ("TYR-VLV", "Tyre valve & cap", "T-05", 40, 2, 20)
        }.Select(p => new Part { Code = p.Item1, Name = p.Item2, Bin = p.Item3, Stock = p.Item4, Cost = p.Item5, Reorder = p.Item6 }));

        db.Inspections.AddRange(
            new Inspection { Id = "INS-2026-0223", Vehicle = "T 932 E2E", Type = "Pre-trip", Date = D(11, 11), OdometerKm = 244310, By = "S. Kweka", Status = "Draft" },
            new Inspection { Id = "INS-2026-0222", Vehicle = "T 934 E2E", Type = "Pre-trip", Date = D(11, 11), OdometerKm = 248800, By = "S. Kweka", Status = "Passed" },
            new Inspection { Id = "INS-2026-0221", Vehicle = "T 933 E2E", Type = "Pre-trip", Date = D(11, 11), OdometerKm = 238410, By = "S. Kweka", Status = "Passed" },
            new Inspection
            {
                Id = "INS-2026-0218", Vehicle = "T 931 E2E", Type = "Post-trip", Date = D(11, 9), OdometerKm = 356900, By = "S. Kweka", Status = "Job card opened", JobCardId = "JC-2026-0412",
                Defects =
                [
                    new() { InspectionId = "INS-2026-0218", Item = "Air leaks / hoses", Category = "Brakes & air system", Note = "Air leak at drive axle 2 hose" },
                    new() { InspectionId = "INS-2026-0218", Item = "Brake chambers & slack adjusters", Category = "Brakes & air system", Note = "Brake chamber axle 2 left leaking" },
                    new() { InspectionId = "INS-2026-0218", Item = "Headlights", Category = "Electrical & lights", Note = "Left headlamp out" }
                ]
            });
        db.JobCards.AddRange(
            new JobCard
            {
                Id = "JC-2026-0412", Vehicle = "T 931 E2E", Source = "INS-2026-0218", OpenedOn = D(11, 9), OdometerKm = 356900,
                Tasks =
                [
                    new() { JobCardId = "JC-2026-0412", Name = "Replace brake chamber — axle 2 left leaking", Category = "Brakes & air system", Mechanic = "M. Ally", Status = "In progress", Hours = 2.5m, PartCode = "BRK-CH-24" },
                    new() { JobCardId = "JC-2026-0412", Name = "Replace air hose — leak at drive axle 2", Category = "Brakes & air system", Mechanic = "M. Ally", Status = "Awaiting parts", Hours = 0, PartCode = "HSE-AIR" },
                    new() { JobCardId = "JC-2026-0412", Name = "Replace left headlamp bulb", Category = "Electrical & lights", Mechanic = "R. Ngowi", Status = "Done", Hours = 0.5m, PartCode = "LMP-H7" }
                ]
            },
            new JobCard
            {
                Id = "JC-2026-0411", Vehicle = "T 207 DMK", Source = "70,000 km service schedule", OpenedOn = D(11, 8), OdometerKm = 61200, Closed = true, ClosedAt = T(11, 8, 15, 10),
                Tasks = [new() { JobCardId = "JC-2026-0411", Name = "70,000 km service — oil, filters, greasing", Category = "Engine & fluids", Mechanic = "P. Mrisho", Status = "Done", Hours = 2, PartCode = "FLT-OIL" }]
            },
            new JobCard
            {
                Id = "JC-2026-0410", Vehicle = "T 929 E2E", Source = "INS-2026-0214", OpenedOn = D(11, 7), OdometerKm = 388140, Closed = true, ClosedAt = T(11, 7, 13, 30),
                Tasks =
                [
                    new() { JobCardId = "JC-2026-0410", Name = "Replace worn wiper blades", Category = "Cab & safety", Mechanic = "F. Hamisi", Status = "Done", Hours = 0.3m, PartCode = "WPR-24" },
                    new() { JobCardId = "JC-2026-0410", Name = "Replace cracked mirror glass", Category = "Cab & safety", Mechanic = "F. Hamisi", Status = "Done", Hours = 0.5m, PartCode = "MIR-GL" }
                ]
            });
        await db.SaveChangesAsync();
        var t412 = await db.JobCardTasks.Where(t => t.JobCardId == "JC-2026-0412").OrderBy(t => t.Id).Select(t => t.Id).ToListAsync();
        var t411 = await db.JobCardTasks.Where(t => t.JobCardId == "JC-2026-0411").Select(t => t.Id).FirstAsync();
        var t410 = await db.JobCardTasks.Where(t => t.JobCardId == "JC-2026-0410").OrderBy(t => t.Id).Select(t => t.Id).ToListAsync();
        db.SparePartRequests.AddRange(
            new SparePartRequest { Id = "SPR-2026-0142", JobCardId = "JC-2026-0412", TaskId = t412[1], PartCode = "HSE-AIR", Qty = 2, Status = "Pending", RequestedBy = "M. Ally" },
            new SparePartRequest { Id = "SPR-2026-0141", JobCardId = "JC-2026-0412", TaskId = t412[2], PartCode = "LMP-H7", Qty = 1, Status = "Issued", RequestedBy = "R. Ngowi", IssueNo = "SI-2026-0882" },
            new SparePartRequest { Id = "SPR-2026-0140", JobCardId = "JC-2026-0412", TaskId = t412[0], PartCode = "BRK-CH-24", Qty = 1, Status = "Issued", RequestedBy = "M. Ally", IssueNo = "SI-2026-0881" },
            new SparePartRequest { Id = "SPR-2026-0138", JobCardId = "JC-2026-0411", TaskId = t411, PartCode = "FLT-OIL", Qty = 2, Status = "Issued", RequestedBy = "P. Mrisho", IssueNo = "SI-2026-0874" },
            new SparePartRequest { Id = "SPR-2026-0136", JobCardId = "JC-2026-0410", TaskId = t410[0], PartCode = "WPR-24", Qty = 2, Status = "Issued", RequestedBy = "F. Hamisi", IssueNo = "SI-2026-0869" },
            new SparePartRequest { Id = "SPR-2026-0137", JobCardId = "JC-2026-0410", TaskId = t410[1], PartCode = "MIR-GL", Qty = 1, Status = "Issued", RequestedBy = "F. Hamisi", IssueNo = "SI-2026-0870" });
        Audit("JobCard", "JC-2026-0412", T(11, 9, 16, 20), "S. Kweka (workshop)", "Opened from inspection INS-2026-0218 · 3 defects");

        // ---------- tyres ----------
        string[] positions = ["A1-L", "A1-R", "A2-LO", "A2-LI", "A2-RI", "A2-RO", "A3-LO", "A3-LI", "A3-RI", "A3-RO", "T1-L", "T1-R", "T2-L", "T2-R", "T3-L", "T3-R"];
        var used = new HashSet<string> { "TY-25-4471" };
        foreach (var v in vehicles.Where(v => v.Type == "Horse 6×4"))
        {
            var h = v.Reg.Sum(ch => ch);
            for (var i = 0; i < positions.Length; i++)
            {
                var p = positions[i]; var steer = p.StartsWith("A1");
                var tread = Math.Round(6 + (h + i * 37) % 40 / 10m, 1);
                var n = 1000 + (h * 7 + i * 131) % 8999;
                var serial = $"TY-{25 + i % 2}-{n:D4}";
                while (used.Contains(serial) || serial.StartsWith("TY-26-51") || serial.StartsWith("TY-26-52")) serial = $"TY-{25 + i % 2}-{++n % 9000 + 1000:D4}";
                if (v.Reg == "T 932 E2E" && p == "A2-RO") { serial = "TY-25-4471"; tread = 3.6m; }
                used.Add(serial);
                db.Tyres.Add(new Tyre
                {
                    Serial = serial, Size = steer ? "385/65R22.5" : "315/80R22.5", Type = steer ? "Steer · highway" : p.StartsWith('T') ? "Trailer · highway" : "Drive · mixed",
                    Status = "Fitted", Vehicle = v.Reg, Position = p, TreadMm = tread, Km = (int)Math.Round((13 - tread) * 85) * 100, Cost = steer ? 345 : 310, Retreads = serial[^1] % 3 == 0 ? 1 : 0
                });
            }
        }
        for (var k = 1; k <= 8; k++) db.Tyres.Add(new Tyre { Serial = $"TY-26-51{k:D2}", Size = "315/80R22.5", Type = "Drive · mixed", Status = "In store · new", TreadMm = 16, Cost = 310 });
        for (var k = 1; k <= 3; k++) db.Tyres.Add(new Tyre { Serial = $"TY-26-52{k:D2}", Size = "385/65R22.5", Type = "Steer · highway", Status = "In store · new", TreadMm = 16, Cost = 345 });
        db.Tyres.AddRange(
            new Tyre { Serial = "TY-25-3310", Size = "315/80R22.5", Type = "Drive · mixed", Status = "In store · retreaded", TreadMm = 12, Km = 96400, Cost = 450, Retreads = 1 },
            new Tyre { Serial = "TY-25-3388", Size = "315/80R22.5", Type = "Drive · mixed", Status = "In store · retreaded", TreadMm = 12, Km = 101200, Cost = 450, Retreads = 1 },
            new Tyre { Serial = "TY-25-2981", Size = "315/80R22.5", Type = "Drive · mixed", Status = "Awaiting retread", TreadMm = 3.2m, Km = 104800, Cost = 310 },
            new Tyre { Serial = "TY-25-2774", Size = "385/65R22.5", Type = "Steer · highway", Status = "At retreader", TreadMm = 3.8m, Km = 88300, Cost = 345 },
            new Tyre { Serial = "TY-24-1904", Size = "315/80R22.5", Type = "Drive · mixed", Status = "Scrapped", TreadMm = 5.1m, Km = 61200, Cost = 310 });
        Audit("Tyre", "TY-25-2981", T(11, 6, 9, 0), "K. Swai (workshop)", "Removed from T 930 E2E A2-LI at 3.2 mm — worn");
        Audit("Tyre", "TY-25-2774", T(11, 1, 9, 0), "K. Swai (workshop)", "Sent to retreader · RT-0331");
        Audit("Tyre", "TY-24-1904", T(11, 3, 9, 0), "K. Swai (workshop)", "Scrapped · sidewall cut, not repairable");

        // ---------- portal requests ----------
        db.TransportRequests.AddRange(
            new TransportRequest { Id = "PR-2026-0090", CustomerId = C("Kilimanjaro Mining Company"), Services = "T", Cargo = "30 t cement bags", From = "Dar es Salaam", To = "Lusaka", Date = D(10, 12), Step = 5, JobId = "JOB-2026-00463" },
            new TransportRequest { Id = "PR-2026-0091", CustomerId = C("Everdigitio Ltd"), Services = "CT", Cargo = "2 × 20′ IT equipment", From = "Dar es Salaam port", To = "Lusaka", Date = D(10, 30), Step = 4, JobId = "JOB-2026-00479" },
            new TransportRequest { Id = "PR-2026-0092", CustomerId = C("Kilimanjaro Mining Company"), Services = "CT", Cargo = "1 × 40′ drilling consumables", Weight = "22 t", From = "Dar es Salaam port", To = "Lusaka, Zambia", Date = today.AddDays(5), Reference = "MSCU 4471028", Notes = "Fragile crates on top, no double stacking", Step = 0 });

        // ---------- number series ----------
        db.Sequences.AddRange(new Sequence[]
        {
            new() { Name = "JOB", Next = 485 }, new() { Name = "FM", Next = 312 }, new() { Name = "TR", Next = 190 }, new() { Name = "SO", Next = 16 },
            new() { Name = "EXP", Next = 310 }, new() { Name = "INV", Next = 30418 }, new() { Name = "PV", Next = 622 }, new() { Name = "INS", Next = 224 },
            new() { Name = "JC", Next = 413 }, new() { Name = "SPR", Next = 143 }, new() { Name = "SI", Next = 883 }, new() { Name = "CLR", Next = 932 },
            new() { Name = "PR", Next = 93 }, new() { Name = "LEG-C", Next = 932 }, new() { Name = "LEG-W", Next = 382 }, new() { Name = "LEG-D", Next = 223 }, new() { Name = "LEG-T", Next = 190 }
        });
        await db.SaveChangesAsync();
    }
}
