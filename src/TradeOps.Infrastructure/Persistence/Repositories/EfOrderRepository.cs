using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeOps.Application.Interfaces;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Infrastructure.Persistence.Repositories;

public sealed class EfOrderRepository(TradeOpsDbContext dbContext) : IOrderRepository
{
    public Task<Order?> GetByClientOrderIdAsync(
        string clientOrderId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(
                order => order.ClientOrderId == clientOrderId,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<Order>> GetReconciliationCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Submitted
                || order.Status == OrderStatus.Accepted
                || order.Status == OrderStatus.PartiallyFilled
                || order.Status == OrderStatus.Unknown)
            .OrderBy(order => order.CreatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<bool> TryAddAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        dbContext.Orders.Add(order);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.Entry(order).State = EntityState.Detached;
            return false;
        }
    }

    public async Task UpdateAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        dbContext.Orders.Update(order);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
