using BetaFleet.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Route = BetaFleet.Api.Domain.Route;

namespace BetaFleet.Api.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobLeg> JobLegs => Set<JobLeg>();
    public DbSet<TimelineEvent> Timeline => Set<TimelineEvent>();
    public DbSet<JobDocument> JobDocuments => Set<JobDocument>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<Handoff> Handoffs => Set<Handoff>();
    public DbSet<ClearingEntry> ClearingEntries => Set<ClearingEntry>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<ContainerEvent> ContainerEvents => Set<ContainerEvent>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<ComplianceDoc> ComplianceDocs => Set<ComplianceDoc>();
    public DbSet<FillingMaster> FillingMasters => Set<FillingMaster>();
    public DbSet<FmLine> FmLines => Set<FmLine>();
    public DbSet<ShipmentOrder> ShipmentOrders => Set<ShipmentOrder>();
    public DbSet<TrackingAlert> TrackingAlerts => Set<TrackingAlert>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<InspectionDefect> InspectionDefects => Set<InspectionDefect>();
    public DbSet<JobCard> JobCards => Set<JobCard>();
    public DbSet<JobCardTask> JobCardTasks => Set<JobCardTask>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<SparePartRequest> SparePartRequests => Set<SparePartRequest>();
    public DbSet<Tyre> Tyres => Set<Tyre>();
    public DbSet<TransportRequest> TransportRequests => Set<TransportRequest>();
    public DbSet<ActivityLog> Activity => Set<ActivityLog>();
    public DbSet<Sequence> Sequences => Set<Sequence>();
    public DbSet<User> Users => Set<User>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(18, 2);
        b.Properties<string>().HaveMaxLength(400);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Customer>().HasIndex(x => x.Name).IsUnique();
        m.Entity<Job>(e =>
        {
            e.HasMany(x => x.Legs).WithOne().HasForeignKey(x => x.JobId);
            e.HasMany(x => x.Timeline).WithOne().HasForeignKey(x => x.JobId);
            e.HasMany(x => x.Documents).WithOne().HasForeignKey(x => x.JobId);
            e.HasMany(x => x.Charges).WithOne().HasForeignKey(x => x.JobId);
            e.HasIndex(x => x.Stage);
        });
        m.Entity<JobLeg>().HasIndex(x => new { x.JobId, x.Unit }).IsUnique();
        m.Entity<Charge>().Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        m.Entity<Handoff>(e =>
        {
            e.HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
            e.HasIndex(x => new { x.Unit, x.State });
        });
        m.Entity<ClearingEntry>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        m.Entity<Container>(e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
            e.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.ContainerId);
        });
        m.Entity<Route>().HasKey(x => x.Code);
        m.Entity<Vehicle>().HasKey(x => x.Reg);
        m.Entity<Driver>().HasKey(x => x.Name);
        m.Entity<ComplianceDoc>().HasIndex(x => new { x.SubjectType, x.SubjectId, x.DocType }).IsUnique();
        m.Entity<FillingMaster>(e =>
        {
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.FillingMasterId);
            e.HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
            e.HasOne<Route>().WithMany().HasForeignKey(x => x.RouteCode);
        });
        m.Entity<ShipmentOrder>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        m.Entity<Expense>().HasOne<Job>().WithMany().HasForeignKey(x => x.JobId);
        m.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.No);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceNo);
        });
        m.Entity<Inspection>().HasMany(x => x.Defects).WithOne().HasForeignKey(x => x.InspectionId);
        m.Entity<JobCard>().HasMany(x => x.Tasks).WithOne().HasForeignKey(x => x.JobCardId);
        m.Entity<Part>().HasKey(x => x.Code);
        m.Entity<Tyre>().HasKey(x => x.Serial);
        m.Entity<ActivityLog>().HasIndex(x => new { x.Entity, x.EntityId });
        m.Entity<Sequence>().HasKey(x => x.Name);
        m.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            e.Property(x => x.Username).HasMaxLength(60);
        });
    }
}
