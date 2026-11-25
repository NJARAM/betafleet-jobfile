using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BetaFleet.Api.Data.Migrations;

/// <inheritdoc />
public partial class _20261125073803_Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Activity",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Entity = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                EntityId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Actor = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Text = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Activity", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ComplianceDocs",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                SubjectType = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                SubjectId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                DocType = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                Reference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComplianceDocs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                CreditLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                OpenBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Drivers",
            columns: table => new
            {
                Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Drivers", x => x.Name);
            });

        migrationBuilder.CreateTable(
            name: "Inspections",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Vehicle = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Type = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                OdometerKm = table.Column<int>(type: "integer", nullable: false),
                By = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobCardId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Inspections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "JobCards",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Vehicle = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Source = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                OpenedOn = table.Column<DateOnly>(type: "date", nullable: false),
                OdometerKm = table.Column<int>(type: "integer", nullable: false),
                Closed = table.Column<bool>(type: "boolean", nullable: false),
                ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobCards", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Parts",
            columns: table => new
            {
                Code = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Bin = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Stock = table.Column<int>(type: "integer", nullable: false),
                Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Reorder = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Parts", x => x.Code);
            });

        migrationBuilder.CreateTable(
            name: "Routes",
            columns: table => new
            {
                Code = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Via = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                OutKm = table.Column<int>(type: "integer", nullable: false),
                ReturnKm = table.Column<int>(type: "integer", nullable: false),
                Days = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                VehicleClass = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Toll = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Borders = table.Column<int>(type: "integer", nullable: false),
                Escort = table.Column<bool>(type: "boolean", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                ReviewOn = table.Column<DateOnly>(type: "date", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Routes", x => x.Code);
            });

        migrationBuilder.CreateTable(
            name: "Sequences",
            columns: table => new
            {
                Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Next = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Sequences", x => x.Name);
            });

        migrationBuilder.CreateTable(
            name: "SparePartRequests",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobCardId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                TaskId = table.Column<int>(type: "integer", nullable: false),
                PartCode = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Qty = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                RequestedBy = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                IssueNo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SparePartRequests", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TrackingAlerts",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Severity = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Kind = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Text = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                ShipmentOrderId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackingAlerts", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Tyres",
            columns: table => new
            {
                Serial = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Size = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Type = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Vehicle = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                Position = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                TreadMm = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Km = table.Column<int>(type: "integer", nullable: false),
                Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Retreads = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tyres", x => x.Serial);
            });

        migrationBuilder.CreateTable(
            name: "Vehicles",
            columns: table => new
            {
                Reg = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                FleetNo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Type = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Driver = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                Trailer = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                OdometerKm = table.Column<int>(type: "integer", nullable: true),
                NextServiceKm = table.Column<int>(type: "integer", nullable: true),
                Subcontracted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Vehicles", x => x.Reg);
            });

        migrationBuilder.CreateTable(
            name: "Invoices",
            columns: table => new
            {
                No = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                CustomerId = table.Column<int>(type: "integer", nullable: false),
                Sbu = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                SapStatus = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                SapDocNo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.No);
                table.ForeignKey(
                    name: "FK_Invoices_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Jobs",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                CustomerId = table.Column<int>(type: "integer", nullable: false),
                Reference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Cargo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                CargoType = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Direction = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Units = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Stage = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                ProgressPct = table.Column<int>(type: "integer", nullable: false),
                Owner = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                OpenedOn = table.Column<DateOnly>(type: "date", nullable: false),
                Eta = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Alert = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Quotation = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Jobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_Jobs_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TransportRequests",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                CustomerId = table.Column<int>(type: "integer", nullable: false),
                Services = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Cargo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Weight = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                From = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                To = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Reference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Step = table.Column<int>(type: "integer", nullable: false),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TransportRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_TransportRequests_Customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "Customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InspectionDefects",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                InspectionId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Item = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Category = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InspectionDefects", x => x.Id);
                table.ForeignKey(
                    name: "FK_InspectionDefects_Inspections_InspectionId",
                    column: x => x.InspectionId,
                    principalTable: "Inspections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "JobCardTasks",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JobCardId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Category = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Mechanic = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Hours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                PartCode = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobCardTasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_JobCardTasks_JobCards_JobCardId",
                    column: x => x.JobCardId,
                    principalTable: "JobCards",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceLines",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                InvoiceNo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                ChargeId = table.Column<int>(type: "integer", nullable: true),
                Unit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                IsDisbursement = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_InvoiceLines_Invoices_InvoiceNo",
                    column: x => x.InvoiceNo,
                    principalTable: "Invoices",
                    principalColumn: "No",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Charges",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Unit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Revenue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                InvoiceNo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Charges", x => x.Id);
                table.ForeignKey(
                    name: "FK_Charges_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ClearingEntries",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                EntryNo = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                EntryType = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Containers = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Stage = table.Column<int>(type: "integer", nullable: false),
                NextUnit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                HandedOver = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClearingEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClearingEntries_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Containers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                ClearingId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Number = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Size = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                DischargedOn = table.Column<DateOnly>(type: "date", nullable: false),
                ReturnedOn = table.Column<DateOnly>(type: "date", nullable: true),
                Depot = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Containers", x => x.Id);
                table.ForeignKey(
                    name: "FK_Containers_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Expenses",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Unit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Type = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Payee = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Currency = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Reference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Rechargeable = table.Column<bool>(type: "boolean", nullable: false),
                Notes = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                PaymentVoucher = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                RequestedBy = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Expenses", x => x.Id);
                table.ForeignKey(
                    name: "FK_Expenses_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FillingMasters",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                TruckRequest = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Truck = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                Trailer = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                Driver = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                RouteCode = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                PayMethod = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                PayRef = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                Comment = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                ShipmentOrderId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FillingMasters", x => x.Id);
                table.ForeignKey(
                    name: "FK_FillingMasters_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_FillingMasters_Routes_RouteCode",
                    column: x => x.RouteCode,
                    principalTable: "Routes",
                    principalColumn: "Code",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Handoffs",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Unit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                FromUnit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                State = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                AcceptedBy = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Handoffs", x => x.Id);
                table.ForeignKey(
                    name: "FK_Handoffs_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "JobDocuments",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Unit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Reference = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobDocuments", x => x.Id);
                table.ForeignKey(
                    name: "FK_JobDocuments_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "JobLegs",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Unit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Number = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                ProgressPct = table.Column<int>(type: "integer", nullable: false),
                Owner = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobLegs", x => x.Id);
                table.ForeignKey(
                    name: "FK_JobLegs_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ShipmentOrders",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                FillingMasterId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                Truck = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Driver = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                RouteCode = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                ProgressPct = table.Column<int>(type: "integer", nullable: false),
                Place = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                SpeedKmh = table.Column<int>(type: "integer", nullable: false),
                Eta = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                GpsKm = table.Column<int>(type: "integer", nullable: false),
                StandardKm = table.Column<int>(type: "integer", nullable: false),
                DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                PodSignedBy = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ShipmentOrders", x => x.Id);
                table.ForeignKey(
                    name: "FK_ShipmentOrders_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Timeline",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JobId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Unit = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Detail = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Done = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Timeline", x => x.Id);
                table.ForeignKey(
                    name: "FK_Timeline_Jobs_JobId",
                    column: x => x.JobId,
                    principalTable: "Jobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ContainerEvents",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ContainerId = table.Column<int>(type: "integer", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Text = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContainerEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContainerEvents_Containers_ContainerId",
                    column: x => x.ContainerId,
                    principalTable: "Containers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "FmLines",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                FillingMasterId = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Code = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Basis = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Qty = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                UnitLabel = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Standard = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Requested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FmLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_FmLines_FillingMasters_FillingMasterId",
                    column: x => x.FillingMasterId,
                    principalTable: "FillingMasters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Activity_Entity_EntityId",
            table: "Activity",
            columns: new[] { "Entity", "EntityId" });

        migrationBuilder.CreateIndex(
            name: "IX_Charges_JobId",
            table: "Charges",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_ClearingEntries_JobId",
            table: "ClearingEntries",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDocs_SubjectType_SubjectId_DocType",
            table: "ComplianceDocs",
            columns: new[] { "SubjectType", "SubjectId", "DocType" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ContainerEvents_ContainerId",
            table: "ContainerEvents",
            column: "ContainerId");

        migrationBuilder.CreateIndex(
            name: "IX_Containers_JobId",
            table: "Containers",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_Containers_Number",
            table: "Containers",
            column: "Number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Customers_Name",
            table: "Customers",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_JobId",
            table: "Expenses",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_FillingMasters_JobId",
            table: "FillingMasters",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_FillingMasters_RouteCode",
            table: "FillingMasters",
            column: "RouteCode");

        migrationBuilder.CreateIndex(
            name: "IX_FmLines_FillingMasterId",
            table: "FmLines",
            column: "FillingMasterId");

        migrationBuilder.CreateIndex(
            name: "IX_Handoffs_JobId",
            table: "Handoffs",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_Handoffs_Unit_State",
            table: "Handoffs",
            columns: new[] { "Unit", "State" });

        migrationBuilder.CreateIndex(
            name: "IX_InspectionDefects_InspectionId",
            table: "InspectionDefects",
            column: "InspectionId");

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLines_InvoiceNo",
            table: "InvoiceLines",
            column: "InvoiceNo");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_CustomerId",
            table: "Invoices",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_JobCardTasks_JobCardId",
            table: "JobCardTasks",
            column: "JobCardId");

        migrationBuilder.CreateIndex(
            name: "IX_JobDocuments_JobId",
            table: "JobDocuments",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_JobLegs_JobId_Unit",
            table: "JobLegs",
            columns: new[] { "JobId", "Unit" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Jobs_CustomerId",
            table: "Jobs",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_Jobs_Stage",
            table: "Jobs",
            column: "Stage");

        migrationBuilder.CreateIndex(
            name: "IX_ShipmentOrders_JobId",
            table: "ShipmentOrders",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_Timeline_JobId",
            table: "Timeline",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_TransportRequests_CustomerId",
            table: "TransportRequests",
            column: "CustomerId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Activity");

        migrationBuilder.DropTable(
            name: "Charges");

        migrationBuilder.DropTable(
            name: "ClearingEntries");

        migrationBuilder.DropTable(
            name: "ComplianceDocs");

        migrationBuilder.DropTable(
            name: "ContainerEvents");

        migrationBuilder.DropTable(
            name: "Drivers");

        migrationBuilder.DropTable(
            name: "Expenses");

        migrationBuilder.DropTable(
            name: "FmLines");

        migrationBuilder.DropTable(
            name: "Handoffs");

        migrationBuilder.DropTable(
            name: "InspectionDefects");

        migrationBuilder.DropTable(
            name: "InvoiceLines");

        migrationBuilder.DropTable(
            name: "JobCardTasks");

        migrationBuilder.DropTable(
            name: "JobDocuments");

        migrationBuilder.DropTable(
            name: "JobLegs");

        migrationBuilder.DropTable(
            name: "Parts");

        migrationBuilder.DropTable(
            name: "Sequences");

        migrationBuilder.DropTable(
            name: "ShipmentOrders");

        migrationBuilder.DropTable(
            name: "SparePartRequests");

        migrationBuilder.DropTable(
            name: "Timeline");

        migrationBuilder.DropTable(
            name: "TrackingAlerts");

        migrationBuilder.DropTable(
            name: "TransportRequests");

        migrationBuilder.DropTable(
            name: "Tyres");

        migrationBuilder.DropTable(
            name: "Vehicles");

        migrationBuilder.DropTable(
            name: "Containers");

        migrationBuilder.DropTable(
            name: "FillingMasters");

        migrationBuilder.DropTable(
            name: "Inspections");

        migrationBuilder.DropTable(
            name: "Invoices");

        migrationBuilder.DropTable(
            name: "JobCards");

        migrationBuilder.DropTable(
            name: "Jobs");

        migrationBuilder.DropTable(
            name: "Routes");

        migrationBuilder.DropTable(
            name: "Customers");
    }
}
