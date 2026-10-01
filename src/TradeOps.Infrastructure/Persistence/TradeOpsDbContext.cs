using Microsoft.EntityFrameworkCore;
using TradeOps.Domain.Entities;

namespace TradeOps.Infrastructure.Persistence;

public sealed class TradeOpsDbContext(DbContextOptions<TradeOpsDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Fill> Fills => Set<Fill>();

    public DbSet<PositionSnapshot> PositionSnapshots => Set<PositionSnapshot>();

    public DbSet<RiskEvent> RiskEvents => Set<RiskEvent>();

    public DbSet<OperationalRiskState> OperationalRiskStates => Set<OperationalRiskState>();

    public DbSet<TradingSignal> TradingSignals => Set<TradingSignal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeOpsDbContext).Assembly);
    }
}
