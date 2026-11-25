namespace BetaFleet.Api.Domain;

/// Business units. A job file uses one or more of them; B (billing) and F (file/finance) only appear on hand-overs and timeline events.
public static class Units
{
    public const string Clearing = "C", Warehousing = "W", Distribution = "D", Transit = "T", Billing = "B", File = "F";
    public static readonly string[] Ordered = [Clearing, Warehousing, Distribution, Transit];
    public static readonly Dictionary<string, string> Names = new()
    {
        [Clearing] = "Clearing & Forwarding", [Warehousing] = "Warehousing", [Distribution] = "Distribution & Courier",
        [Transit] = "Transit Transport", [Billing] = "Billing", [File] = "Job file"
    };
    public static readonly Dictionary<string, string> Stage = new()
    {
        [Clearing] = "Customs", [Warehousing] = "Warehouse", [Distribution] = "Distribution", [Transit] = "In transit"
    };
}

public class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal OpenBalance { get; set; }
}

public class Job
{
    public required string Id { get; set; }          // JOB-2026-00481
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string Reference { get; set; }   // BL / AWB / UCR
    public required string Cargo { get; set; }
    public string CargoType { get; set; } = "Containerised";
    public string Direction { get; set; } = "Import";
    public required string Units { get; set; }       // e.g. "CWDT"
    public string Stage { get; set; } = "Opened";
    public int ProgressPct { get; set; }
    public required string Owner { get; set; }
    public DateOnly OpenedOn { get; set; }
    public string Eta { get; set; } = "";
    public string Alert { get; set; } = "";
    public string? Quotation { get; set; }
    public List<JobLeg> Legs { get; set; } = [];
    public List<TimelineEvent> Timeline { get; set; } = [];
    public List<JobDocument> Documents { get; set; } = [];
    public List<Charge> Charges { get; set; } = [];
}

public class JobLeg
{
    public int Id { get; set; }
    public required string JobId { get; set; }
    public required string Unit { get; set; }
    public required string Number { get; set; }
    public string Status { get; set; } = "Not started";
    public int ProgressPct { get; set; }
    public string Owner { get; set; } = "";
    public string Summary { get; set; } = "";
}

public class TimelineEvent
{
    public int Id { get; set; }
    public required string JobId { get; set; }
    public DateOnly Date { get; set; }
    public required string Unit { get; set; }
    public required string Title { get; set; }
    public string Detail { get; set; } = "";
    public bool Done { get; set; } = true;
}

public class JobDocument
{
    public int Id { get; set; }
    public required string JobId { get; set; }
    public required string Unit { get; set; }
    public required string Name { get; set; }
    public string Reference { get; set; } = "";
    public string Status { get; set; } = "Pending";  // Received, Released, Generated, Signed, Missing, Pending
}

public enum ChargeKind { Revenue, Disbursement }

public class Charge
{
    public int Id { get; set; }
    public required string JobId { get; set; }
    public required string Unit { get; set; }
    public required string Description { get; set; }
    public ChargeKind Kind { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public string Status { get; set; } = "Ready"; // Ready, Accruing, Awaiting POD, Billed
    public string? InvoiceNo { get; set; }
}

public class Handoff
{
    public int Id { get; set; }
    public required string Unit { get; set; }      // receiving unit (C/W/D/T/B)
    public required string JobId { get; set; }
    public required string FromUnit { get; set; }
    public required string Title { get; set; }
    public string Note { get; set; } = "";
    public string State { get; set; } = "Waiting"; // Waiting, Accepted, Done
    public string? AcceptedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}

public class ClearingEntry
{
    public static readonly string[] Stages = ["Documents received", "Lodged", "Assessed", "Duty paid", "Released", "Gated out"];
    public const int ReleasedStage = 4;
    public required string Id { get; set; }        // CLR-2026-0912
    public required string JobId { get; set; }
    public string EntryNo { get; set; } = "Not yet lodged";
    public required string EntryType { get; set; } // Transit (T1), Home consumption, Export, Warehousing (bonded)
    public required string Containers { get; set; }
    public int Stage { get; set; }
    public required string NextUnit { get; set; }  // W, T, D or X (customer collects)
    public bool HandedOver { get; set; }
}

public class Container
{
    public const int FreeDays = 14;
    public int Id { get; set; }
    public required string JobId { get; set; }
    public required string ClearingId { get; set; }
    public required string Number { get; set; }    // ISO 6346, e.g. MSKU7741310
    public required string Size { get; set; }
    public required string Status { get; set; }
    public DateOnly DischargedOn { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public string? Depot { get; set; }
    public List<ContainerEvent> Events { get; set; } = [];
}

public class ContainerEvent
{
    public int Id { get; set; }
    public int ContainerId { get; set; }
    public DateOnly Date { get; set; }
    public required string Text { get; set; }
}

public class Route
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Via { get; set; }
    public int OutKm { get; set; }
    public int ReturnKm { get; set; }
    public int Days { get; set; }
    public required string Type { get; set; }
    public required string VehicleClass { get; set; }
    public decimal Toll { get; set; }
    public int Borders { get; set; }
    public bool Escort { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly ReviewOn { get; set; }
    public int Version { get; set; }
}

public class Vehicle
{
    public required string Reg { get; set; }       // T 928 E2E
    public required string FleetNo { get; set; }
    public required string Type { get; set; }
    public string Status { get; set; } = "Available"; // Available, On trip, In workshop, Assigned
    public string? Driver { get; set; }
    public string? Trailer { get; set; }
    public int? OdometerKm { get; set; }
    public int? NextServiceKm { get; set; }
    public bool Subcontracted { get; set; }
}

public class Driver
{
    public required string Name { get; set; }
    public string Note { get; set; } = "";
}

public class ComplianceDoc
{
    public int Id { get; set; }
    public required string SubjectType { get; set; } // Vehicle | Driver
    public required string SubjectId { get; set; }
    public required string DocType { get; set; }
    public DateOnly? ExpiresOn { get; set; }         // null = not applicable
    public string Reference { get; set; } = "";
}

public static class FmStatus
{
    public const string Draft = "Draft", Pending = "Pending approval", Approved = "Approved", Rejected = "Rejected",
        Disbursed = "Disbursed", Converted = "Converted to shipment order";
    public static readonly string[] Flow = [Draft, Pending, Approved, Disbursed, Converted];
}

/// Filling master: the per-truck trip budget built from the route's standard cost lines.
public class FillingMaster
{
    public const decimal TolerancePct = 5;
    public required string Id { get; set; }        // FM-2026-0311
    public required string JobId { get; set; }
    public required string TruckRequest { get; set; }
    public string? Truck { get; set; }
    public string? Trailer { get; set; }
    public string? Driver { get; set; }
    public required string RouteCode { get; set; }
    public string Status { get; set; } = FmStatus.Draft;
    public string PayMethod { get; set; } = "Fuel card + EFT to driver";
    public string? PayRef { get; set; }
    public string? Comment { get; set; }
    public string? ShipmentOrderId { get; set; }
    public List<FmLine> Lines { get; set; } = [];
}

public class FmLine
{
    public int Id { get; set; }
    public required string FillingMasterId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Basis { get; set; }
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public string UnitLabel { get; set; } = "";
    public decimal Standard { get; set; }
    public decimal Requested { get; set; }
    public string Reason { get; set; } = "";
}

public class ShipmentOrder
{
    public required string Id { get; set; }        // SO-E2E-0008
    public required string JobId { get; set; }
    public string? FillingMasterId { get; set; }
    public required string Truck { get; set; }
    public required string Driver { get; set; }
    public required string RouteCode { get; set; }
    public int ProgressPct { get; set; }
    public string Place { get; set; } = "Dar es Salaam";
    public int SpeedKmh { get; set; }
    public string Eta { get; set; } = "";
    public string Status { get; set; } = "Loading"; // Loading, On route, Stopped, Speeding, Delivered
    public int GpsKm { get; set; }
    public int StandardKm { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string? PodSignedBy { get; set; }
}

public class TrackingAlert
{
    public int Id { get; set; }
    public required string Severity { get; set; }  // bad, warn, info
    public required string Kind { get; set; }
    public required string Text { get; set; }
    public required string ShipmentOrderId { get; set; }
    public required string JobId { get; set; }
    public DateTimeOffset At { get; set; }
}

public static class ExpStatus
{
    public const string PendingHead = "Pending unit head", PendingFinance = "Pending finance", Approved = "Approved",
        Rejected = "Rejected", Paid = "Paid";
}

public class Expense
{
    public const decimal FinanceLimit = 1000;
    public required string Id { get; set; }        // EXP-2026-0309
    public required string JobId { get; set; }
    public required string Unit { get; set; }
    public required string Type { get; set; }
    public required string Payee { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Reference { get; set; } = "";
    public bool Rechargeable { get; set; }
    public string Notes { get; set; } = "";
    public string Status { get; set; } = ExpStatus.PendingHead;
    public string? PaymentVoucher { get; set; }
    public required string RequestedBy { get; set; }
    public DateOnly Date { get; set; }
}

public class Invoice
{
    public const decimal VatRate = 0.18m;
    public required string No { get; set; }
    public DateOnly Date { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string Sbu { get; set; }      // C/W/D/T or ALL (consolidated)
    public string Status { get; set; } = "Open";  // Open, Paid
    public string SapStatus { get; set; } = "Queued"; // Queued, Posted, Failed
    public string? SapDocNo { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];
}

public class InvoiceLine
{
    public int Id { get; set; }
    public required string InvoiceNo { get; set; }
    public int? ChargeId { get; set; }
    public required string Unit { get; set; }
    public required string JobId { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
    public bool IsDisbursement { get; set; }
}

public class Inspection
{
    public required string Id { get; set; }
    public required string Vehicle { get; set; }
    public required string Type { get; set; }      // Pre-trip, Post-trip
    public DateOnly Date { get; set; }
    public int OdometerKm { get; set; }
    public required string By { get; set; }
    public string Status { get; set; } = "Draft";  // Draft, Passed, Job card opened
    public string? JobCardId { get; set; }
    public List<InspectionDefect> Defects { get; set; } = [];
}

public class InspectionDefect
{
    public int Id { get; set; }
    public required string InspectionId { get; set; }
    public required string Item { get; set; }
    public required string Category { get; set; }
    public string Note { get; set; } = "";
}

public class JobCard
{
    public const decimal LabourRate = 15;
    public required string Id { get; set; }
    public required string Vehicle { get; set; }
    public required string Source { get; set; }
    public DateOnly OpenedOn { get; set; }
    public int OdometerKm { get; set; }
    public bool Closed { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public List<JobCardTask> Tasks { get; set; } = [];
}

public class JobCardTask
{
    public int Id { get; set; }
    public required string JobCardId { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public string Mechanic { get; set; } = "";
    public string Status { get; set; } = "Not started"; // Not started, In progress, Awaiting parts, Done
    public decimal Hours { get; set; }
    public string? PartCode { get; set; }
}

public class Part
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Bin { get; set; }
    public int Stock { get; set; }
    public decimal Cost { get; set; }
    public int Reorder { get; set; }
}

public class SparePartRequest
{
    public required string Id { get; set; }
    public required string JobCardId { get; set; }
    public int TaskId { get; set; }
    public required string PartCode { get; set; }
    public int Qty { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Issued, Rejected
    public required string RequestedBy { get; set; }
    public string? IssueNo { get; set; }
}

public class Tyre
{
    public const decimal TreadMinMm = 4;
    public required string Serial { get; set; }
    public required string Size { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }     // Fitted, In store · new, In store · retreaded, Awaiting retread, At retreader, Scrapped
    public string? Vehicle { get; set; }
    public string? Position { get; set; }
    public decimal TreadMm { get; set; }
    public int Km { get; set; }
    public decimal Cost { get; set; }
    public int Retreads { get; set; }
}

public class TransportRequest
{
    public static readonly string[] Steps = ["Submitted", "Under review", "Approved", "Job file opened", "In transit", "Delivered"];
    public required string Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string Services { get; set; }
    public required string Cargo { get; set; }
    public string Weight { get; set; } = "";
    public required string From { get; set; }
    public required string To { get; set; }
    public DateOnly Date { get; set; }
    public string Reference { get; set; } = "";
    public string Notes { get; set; } = "";
    public int Step { get; set; }
    public string? JobId { get; set; }
}

/// Audit trail shared by every module: who did what to which record.
public class ActivityLog
{
    public long Id { get; set; }
    public required string Entity { get; set; }
    public required string EntityId { get; set; }
    public DateTimeOffset At { get; set; }
    public required string Actor { get; set; }
    public required string Text { get; set; }
}

/// Document-number counters (JOB, FM, SO, EXP, INV …) so numbers never collide.
public class Sequence
{
    public required string Name { get; set; }
    public int Next { get; set; }
}
