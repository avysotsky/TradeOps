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
        var signalQuery = BuildSignalQuery(fromInclusive, toExclusive, symbol);
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

        var lifecycleQuery = BuildLifecycleQuery(fromInclusive, toExclusive, symbol);
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

        var fillQuery = BuildFillQuery(fromInclusive, toExclusive, symbol);
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

    public async Task<ExecutionMetricsSeriesSnapshot> GetSeriesAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string bucket,
        TimeSpan bucketSize,
        string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        var firstBucketStart = AlignToBucketStart(fromInclusive, bucketSize);
        var buckets = new SortedDictionary<DateTimeOffset, SeriesBucketAccumulator>();

        for (var bucketStart = firstBucketStart;
             bucketStart < toExclusive;
             bucketStart = bucketStart.Add(bucketSize))
        {
            var bucketEnd = bucketStart.Add(bucketSize);
            buckets[bucketStart] = new SeriesBucketAccumulator(
                bucketStart < fromInclusive ? fromInclusive : bucketStart,
                bucketEnd > toExclusive ? toExclusive : bucketEnd);
        }

        var signalFacts = await BuildSignalQuery(fromInclusive, toExclusive, symbol)
            .Select(signal => new
            {
                signal.CreatedAt,
                signal.Outcome
            })
            .ToArrayAsync(cancellationToken);

        foreach (var signal in signalFacts)
        {
            var accumulator = buckets[AlignToBucketStart(signal.CreatedAt, bucketSize)];
            switch (signal.Outcome)
            {
                case SignalOutcome.Accepted:
                    accumulator.AcceptedSignals++;
                    break;
                case SignalOutcome.Rejected:
                    accumulator.RejectedSignals++;
                    break;
                case SignalOutcome.Received:
                    accumulator.PendingSignals++;
                    break;
            }
        }

        var lifecycleFacts = await BuildLifecycleQuery(fromInclusive, toExclusive, symbol)
            .Select(item => new
            {
                item.OccurredAt,
                item.OrderId,
                item.Status
            })
            .ToArrayAsync(cancellationToken);

        foreach (var lifecycle in lifecycleFacts)
        {
            var accumulator = buckets[AlignToBucketStart(lifecycle.OccurredAt, bucketSize)];
            accumulator.LifecycleEvents++;
            accumulator.OrderIds.Add(lifecycle.OrderId);

            switch (lifecycle.Status)
            {
                case OrderStatus.Created:
                    accumulator.Created++;
                    break;
                case OrderStatus.Submitted:
                    accumulator.Submitted++;
                    break;
                case OrderStatus.Accepted:
                    accumulator.Accepted++;
                    break;
                case OrderStatus.PartiallyFilled:
                    accumulator.PartiallyFilled++;
                    break;
                case OrderStatus.Filled:
                    accumulator.Filled++;
                    break;
                case OrderStatus.Cancelled:
                    accumulator.Cancelled++;
                    break;
                case OrderStatus.Rejected:
                    accumulator.Rejected++;
                    break;
                case OrderStatus.Unknown:
                    accumulator.Unknown++;
                    break;
            }
        }

        var fillFacts = await BuildFillQuery(fromInclusive, toExclusive, symbol)
            .Select(fill => fill.FilledAt)
            .ToArrayAsync(cancellationToken);

        foreach (var filledAt in fillFacts)
        {
            buckets[AlignToBucketStart(filledAt, bucketSize)].FillsReceived++;
        }

        var series = buckets.Values
            .Select(item => item.ToSnapshot())
            .ToArray();

        return new ExecutionMetricsSeriesSnapshot(
            DateTimeOffset.UtcNow,
            fromInclusive,
            toExclusive,
            bucket,
            symbol,
            series);
    }

    private IQueryable<TradingSignal> BuildSignalQuery(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string? symbol)
    {
        IQueryable<TradingSignal> query = dbContext.TradingSignals
            .AsNoTracking()
            .Where(signal =>
                signal.CreatedAt >= fromInclusive &&
                signal.CreatedAt < toExclusive);

        return symbol is null
            ? query
            : query.Where(signal => signal.Symbol == symbol);
    }

    private IQueryable<OrderLifecycleEvent> BuildLifecycleQuery(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string? symbol)
    {
        IQueryable<OrderLifecycleEvent> query = dbContext.OrderLifecycleEvents
            .AsNoTracking()
            .Where(item =>
                item.OccurredAt >= fromInclusive &&
                item.OccurredAt < toExclusive);

        if (symbol is null)
        {
            return query;
        }

        return
            from item in query
            join order in dbContext.Orders.AsNoTracking()
                on item.OrderId equals order.Id
            where order.Symbol == symbol
            select item;
    }

    private IQueryable<Fill> BuildFillQuery(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string? symbol)
    {
        IQueryable<Fill> query = dbContext.Fills
            .AsNoTracking()
            .Where(fill =>
                fill.FilledAt >= fromInclusive &&
                fill.FilledAt < toExclusive);

        if (symbol is null)
        {
            return query;
        }

        return
            from fill in query
            join order in dbContext.Orders.AsNoTracking()
                on fill.OrderId equals order.Id
            where order.Symbol == symbol
            select fill;
    }

    private static DateTimeOffset AlignToBucketStart(DateTimeOffset timestamp, TimeSpan bucketSize)
    {
        var utcTicksSinceEpoch = timestamp.ToUniversalTime().UtcDateTime.Ticks - DateTime.UnixEpoch.Ticks;
        var quotient = Math.DivRem(utcTicksSinceEpoch, bucketSize.Ticks, out var remainder);
        if (remainder < 0)
        {
            quotient--;
        }

        return DateTimeOffset.UnixEpoch.AddTicks(quotient * bucketSize.Ticks);
    }

    private sealed class SeriesBucketAccumulator(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive)
    {
        public DateTimeOffset FromInclusive { get; } = fromInclusive;
        public DateTimeOffset ToExclusive { get; } = toExclusive;
        public long AcceptedSignals { get; set; }
        public long RejectedSignals { get; set; }
        public long PendingSignals { get; set; }
        public long LifecycleEvents { get; set; }
        public HashSet<Guid> OrderIds { get; } = [];
        public long Created { get; set; }
        public long Submitted { get; set; }
        public long Accepted { get; set; }
        public long PartiallyFilled { get; set; }
        public long Filled { get; set; }
        public long Cancelled { get; set; }
        public long Rejected { get; set; }
        public long Unknown { get; set; }
        public long FillsReceived { get; set; }

        public ExecutionMetricsSeriesBucket ToSnapshot()
        {
            var receivedSignals = AcceptedSignals + RejectedSignals + PendingSignals;

            return new ExecutionMetricsSeriesBucket(
                FromInclusive,
                ToExclusive,
                new WindowSignalExecutionMetrics(
                    receivedSignals,
                    AcceptedSignals,
                    RejectedSignals,
                    PendingSignals),
                new WindowOrderLifecycleMetrics(
                    LifecycleEvents,
                    OrderIds.Count,
                    Created,
                    Submitted,
                    Accepted,
                    PartiallyFilled,
                    Filled,
                    Cancelled,
                    Rejected,
                    Unknown),
                FillsReceived);
        }
    }
}
