using Microsoft.EntityFrameworkCore;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Infrastructure.Persistence.Repositories;

public sealed class EfOperatorReadRepository(TradeOpsDbContext dbContext)
    : IOperatorReadRepository
{
    public Task<TradingSignal?> GetSignalByIdAsync(
        Guid signalId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.TradingSignals
            .AsNoTracking()
            .FirstOrDefaultAsync(
                signal => signal.Id == signalId,
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<TradingSignal>> GetSignalsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);

        return await dbContext.TradingSignals
            .AsNoTracking()
            .OrderByDescending(signal => signal.CreatedAt)
            .ThenByDescending(signal => signal.Id)
            .Take(safeLimit)
            .ToArrayAsync(cancellationToken);
    }

    public Task<Order?> GetLocalOrderAsync(
        string idOrClientOrderId,
        CancellationToken cancellationToken = default)
    {
        var key = idOrClientOrderId.Trim();
        var query = dbContext.Orders.AsNoTracking();

        return Guid.TryParse(key, out var orderId)
            ? query.FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken)
            : query.FirstOrDefaultAsync(order => order.ClientOrderId == key, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OrderLifecycleEvent>> GetOrderHistoryAsync(
        string idOrClientOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = await GetLocalOrderAsync(idOrClientOrderId, cancellationToken);
        if (order is null)
        {
            return Array.Empty<OrderLifecycleEvent>();
        }

        var events = await dbContext.OrderLifecycleEvents
            .AsNoTracking()
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

        if (events.Length > 0)
        {
            return events;
        }

        return
        [
            new OrderLifecycleEvent
            {
                Id = Guid.Empty,
                OrderId = order.Id,
                ClientOrderId = order.ClientOrderId,
                PreviousStatus = null,
                Status = order.Status,
                FilledQuantity = order.FilledQuantity,
                AverageFillPrice = order.AverageFillPrice,
                ExchangeOrderId = order.ExchangeOrderId,
                Source = "LegacySnapshot",
                OccurredAt = order.UpdatedAt
            }
        ];
    }

    public async Task<IReadOnlyCollection<FillAuditRecord>> GetFillsAsync(
        string? symbol,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);

        var query =
            from fill in dbContext.Fills.AsNoTracking()
            join order in dbContext.Orders.AsNoTracking()
                on fill.OrderId equals order.Id
            select new { Fill = fill, Order = order };

        if (!string.IsNullOrWhiteSpace(symbol))
        {
            var normalizedSymbol = symbol.Trim().ToUpperInvariant();
            query = query.Where(item => item.Order.Symbol == normalizedSymbol);
        }

        return await query
            .OrderByDescending(item => item.Fill.FilledAt)
            .ThenByDescending(item => item.Fill.ExchangeFillId)
            .Take(safeLimit)
            .Select(item => new FillAuditRecord(
                item.Fill.Id,
                item.Fill.OrderId,
                item.Fill.ExchangeFillId,
                item.Order.ClientOrderId,
                item.Order.Symbol,
                item.Order.Side,
                item.Fill.Quantity,
                item.Fill.Price,
                item.Fill.Fee,
                item.Fill.FeeCurrency,
                item.Fill.FilledAt))
            .ToArrayAsync(cancellationToken);
    }
}
