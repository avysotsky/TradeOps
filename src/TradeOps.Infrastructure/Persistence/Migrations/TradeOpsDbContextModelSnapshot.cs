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
#pragma warning restore 612, 618
    }
}
