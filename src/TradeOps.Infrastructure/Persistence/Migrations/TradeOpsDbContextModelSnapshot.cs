using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TradeOps.Domain.Enums;

#nullable disable

namespace TradeOps.Infrastructure.Persistence.Migrations;

[DbContext(typeof(TradeOpsDbContext))]
partial class TradeOpsDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("TradeOps.Domain.Entities.Fill", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uuid");

            b.Property<string>("ExchangeFillId")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<decimal?>("Fee")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<string>("FeeCurrency")
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<DateTimeOffset>("FilledAt")
                .HasColumnType("timestamp with time zone");

            b.Property<Guid>("OrderId")
                .HasColumnType("uuid");

            b.Property<decimal>("Price")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<decimal>("Quantity")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.HasKey("Id");

            b.HasIndex("ExchangeFillId")
                .IsUnique();

            b.HasIndex("OrderId");

            b.ToTable("Fills");
        });

        modelBuilder.Entity("TradeOps.Domain.Entities.OperationalRiskState", b =>
        {
            b.Property<string>("Id")
                .ValueGeneratedNever()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)");

            b.Property<bool>("EmergencyStop")
                .HasColumnType("boolean");

            b.Property<string>("EmergencyStopReason")
                .HasMaxLength(500)
                .HasColumnType("character varying(500)");

            b.Property<bool>("TradingEnabled")
                .HasColumnType("boolean");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("timestamp with time zone");

            b.HasKey("Id");

            b.ToTable("OperationalRiskStates");
        });

        modelBuilder.Entity("TradeOps.Domain.Entities.Order", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uuid");

            b.Property<decimal?>("AverageFillPrice")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<string>("ClientOrderId")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("ExchangeOrderId")
                .HasMaxLength(100)
                .HasColumnType("character varying(100)");

            b.Property<decimal>("FilledQuantity")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<OrderType>("OrderType")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<decimal?>("Price")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<decimal>("RequestedQuantity")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<OrderSide>("Side")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<OrderStatus>("Status")
                .HasConversion<string>()
                .HasMaxLength(30)
                .HasColumnType("character varying(30)");

            b.Property<string>("Symbol")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)");

            b.Property<DateTimeOffset>("UpdatedAt")
                .HasColumnType("timestamp with time zone");

            b.HasKey("Id");

            b.HasIndex("ClientOrderId")
                .IsUnique();

            b.ToTable("Orders");
        });

        modelBuilder.Entity("TradeOps.Domain.Entities.PositionSnapshot", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uuid");

            b.Property<decimal>("AverageEntryPrice")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<DateTimeOffset>("CapturedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<decimal?>("MarkPrice")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<decimal>("Quantity")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<decimal>("RealizedPnL")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<OrderSide?>("Side")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<string>("Symbol")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)");

            b.Property<decimal?>("UnrealizedPnL")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.HasKey("Id");

            b.HasIndex("Symbol", "CapturedAt");

            b.ToTable("PositionSnapshots");
        });

        modelBuilder.Entity("TradeOps.Domain.Entities.RiskEvent", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedNever()
                .HasColumnType("uuid");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<string>("EventKey")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("character varying(120)");

            b.Property<string>("EventType")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("character varying(50)");

            b.Property<decimal?>("ExchangeQuantity")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<OrderSide?>("ExchangeSide")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<DateTimeOffset>("LastObservedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<decimal?>("LocalQuantity")
                .HasPrecision(28, 12)
                .HasColumnType("numeric(28,12)");

            b.Property<OrderSide?>("LocalSide")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<string>("Message")
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnType("character varying(1000)");

            b.Property<DateTimeOffset?>("ResolvedAt")
                .HasColumnType("timestamp with time zone");

            b.Property<RiskEventSeverity>("Severity")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasColumnType("character varying(20)");

            b.Property<string>("Symbol")
                .HasMaxLength(50)
                .HasColumnType("character varying(50)");

            b.HasKey("Id");

            b.HasIndex("LastObservedAt");

            b.HasIndex("EventType", "EventKey")
                .IsUnique()
                .HasFilter("\"ResolvedAt\" IS NULL");

            b.ToTable("RiskEvents");
        });

        modelBuilder.Entity("TradeOps.Domain.Entities.Fill", b =>
        {
            b.HasOne("TradeOps.Domain.Entities.Order", null)
                .WithMany()
                .HasForeignKey("OrderId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
#pragma warning restore 612, 618
    }
}
