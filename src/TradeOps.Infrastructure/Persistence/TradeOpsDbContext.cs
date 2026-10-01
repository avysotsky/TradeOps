using Microsoft.EntityFrameworkCore;
using TradeOps.Domain.Entities;

namespace TradeOps.Infrastructure.Persistence;

public sealed class TradeOpsDbContext(DbContextOptions<TradeOpsDbContext> options)
    : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Fill> Fills => Set<Fill>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TradeOpsDbContext).Assembly);
    }
}
