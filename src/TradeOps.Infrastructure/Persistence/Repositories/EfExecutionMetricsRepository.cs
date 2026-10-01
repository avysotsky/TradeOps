using Microsoft.EntityFrameworkCore;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Infrastructure.Persistence.Repositories;

public sealed class EfExecutionMetricsRepository(
    TradeOpsDbContext dbContext,
    RiskSettings riskSettings) : IExecutionMetricsRepository
{
    public async Task<ExecutionMetricsSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var signalCounts = await dbContext.TradingSignals
            .AsNoTracking()
            .GroupBy(signal => signal.Outcome)
            .Select(group => new
            {
                Outcome = group.Key,
                Count = group.LongCount()
            })
            .ToDictionaryAsync(
                item => item.Outcome,
                item => item.Count,
                cancellationToken);

        var acceptedSignals = signalCounts.GetValueOrDefault(SignalOutcome.Accepted);
        var rejectedSignals = signalCounts.GetValueOrDefault(SignalOutcome.Rejected);
        var pendingSignals = signalCounts.GetValueOrDefault(SignalOutcome.Received);
        var receivedSignals = acceptedSignals + rejectedSignals + pendingSignals;

        var orderCounts = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.LongCount()
            })
            .ToDictionaryAsync(
                item => item.Status,
                item => item.Count,
                cancellationToken);

        var totalOrders = orderCounts.Values.Sum();
        var fillsReceived = await dbContext.Fills
            .AsNoTracking()
            .LongCountAsync(cancellationToken);

        var persistedRiskState = await dbContext.OperationalRiskStates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                state => state.Id == OperationalRiskState.DefaultId,
                cancellationToken);

        var runStatuses = await dbContext.OperationalRunStatuses
            .AsNoTracking()
            .Where(status =>
                status.RunType == OperationalRunTypes.OrderReconciliation ||
                status.RunType == OperationalRunTypes.RecoveryCycle)
            .ToArrayAsync(cancellationToken);

        var reconciliation = runStatuses.FirstOrDefault(
            status => status.RunType == OperationalRunTypes.OrderReconciliation);
        var recovery = runStatuses.FirstOrDefault(
            status => status.RunType == OperationalRunTypes.RecoveryCycle);

        return new ExecutionMetricsSnapshot(
            DateTimeOffset.UtcNow,
            new SignalExecutionMetrics(
                receivedSignals,
                acceptedSignals,
                rejectedSignals,
                pendingSignals),
            new CurrentOrderStatusMetrics(
                totalOrders,
                orderCounts.GetValueOrDefault(OrderStatus.Created),
                orderCounts.GetValueOrDefault(OrderStatus.Submitted),
                orderCounts.GetValueOrDefault(OrderStatus.Accepted),
                orderCounts.GetValueOrDefault(OrderStatus.PartiallyFilled),
                orderCounts.GetValueOrDefault(OrderStatus.Filled),
                orderCounts.GetValueOrDefault(OrderStatus.Cancelled),
                orderCounts.GetValueOrDefault(OrderStatus.Rejected),
                orderCounts.GetValueOrDefault(OrderStatus.Unknown)),
            fillsReceived,
            new ExecutionRiskMetrics(
                persistedRiskState?.TradingEnabled ?? riskSettings.TradingEnabled,
                persistedRiskState?.EmergencyStop ?? riskSettings.EmergencyStop),
            new OperationalExecutionMetrics(
                reconciliation?.Succeeded,
                recovery?.Succeeded,
                recovery?.PositionMismatches));
    }

    public async Task<ExecutionMetricsWindowSnapshot> GetWindowAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TradingSignal> signalQuery = dbContext.TradingSignals
            .AsNoTracking()
            .Where(signal =>
                signal.CreatedAt >= fromInclusive &&
                signal.CreatedAt < toExclusive);

        if (symbol is not null)
        {
            signalQuery = signalQuery.Where(signal => signal.Symbol == symbol);
        }

        var signalCounts = await signalQuery
            .GroupBy(signal => signal.Outcome)
            .Select(group => new
            {
                Outcome = group.Key,
                Count = group.LongCount()
            })
            .ToDictionaryAsync(
                item => item.Outcome,
                item => item.Count,
                cancellationToken);

        var acceptedSignals = signalCounts.GetValueOrDefault(SignalOutcome.Accepted);
        var rejectedSignals = signalCounts.GetValueOrDefault(SignalOutcome.Rejected);
        var pendingSignals = signalCounts.GetValueOrDefault(SignalOutcome.Received);
        var receivedSignals = acceptedSignals + rejectedSignals + pendingSignals;

        IQueryable<OrderLifecycleEvent> lifecycleQuery = dbContext.OrderLifecycleEvents
            .AsNoTracking()
            .Where(item =>
                item.OccurredAt >= fromInclusive &&
                item.OccurredAt < toExclusive);

        if (symbol is not null)
        {
            lifecycleQuery =
                from item in lifecycleQuery
                join order in dbContext.Orders.AsNoTracking()
                    on item.OrderId equals order.Id
                where order.Symbol == symbol
                select item;
        }

        var lifecycleCounts = await lifecycleQuery
            .GroupBy(item => item.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.LongCount()
            })
            .ToDictionaryAsync(
                item => item.Status,
                item => item.Count,
                cancellationToken);

        var lifecycleEvents = lifecycleCounts.Values.Sum();
        var ordersTouched = await lifecycleQuery
            .Select(item => item.OrderId)
            .Distinct()
            .LongCountAsync(cancellationToken);

        IQueryable<Fill> fillQuery = dbContext.Fills
            .AsNoTracking()
            .Where(fill =>
                fill.FilledAt >= fromInclusive &&
                fill.FilledAt < toExclusive);

        if (symbol is not null)
        {
            fillQuery =
                from fill in fillQuery
                join order in dbContext.Orders.AsNoTracking()
                    on fill.OrderId equals order.Id
                where order.Symbol == symbol
                select fill;
        }

        var fillsReceived = await fillQuery.LongCountAsync(cancellationToken);

        return new ExecutionMetricsWindowSnapshot(
            DateTimeOffset.UtcNow,
            fromInclusive,
            toExclusive,
            symbol,
            new WindowSignalExecutionMetrics(
                receivedSignals,
                acceptedSignals,
                rejectedSignals,
                pendingSignals),
            new WindowOrderLifecycleMetrics(
                lifecycleEvents,
                ordersTouched,
                lifecycleCounts.GetValueOrDefault(OrderStatus.Created),
                lifecycleCounts.GetValueOrDefault(OrderStatus.Submitted),
                lifecycleCounts.GetValueOrDefault(OrderStatus.Accepted),
                lifecycleCounts.GetValueOrDefault(OrderStatus.PartiallyFilled),
                lifecycleCounts.GetValueOrDefault(OrderStatus.Filled),
                lifecycleCounts.GetValueOrDefault(OrderStatus.Cancelled),
                lifecycleCounts.GetValueOrDefault(OrderStatus.Rejected),
                lifecycleCounts.GetValueOrDefault(OrderStatus.Unknown)),
            fillsReceived);
    }
}
