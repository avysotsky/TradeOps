using Microsoft.EntityFrameworkCore;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Infrastructure.Persistence.Repositories;

public sealed class EfSignalTransitionMetricsRepository(TradeOpsDbContext dbContext)
    : ISignalTransitionMetricsRepository
{
    public async Task<SignalTransitionMetricsWindowSnapshot> GetWindowAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TradingSignalOutcomeEvent> query = dbContext.TradingSignalOutcomeEvents
            .AsNoTracking()
            .Where(item =>
                item.OccurredAt >= fromInclusive &&
                item.OccurredAt < toExclusive);

        if (symbol is not null)
        {
            query =
                from item in query
                join signal in dbContext.TradingSignals.AsNoTracking()
                    on item.TradingSignalId equals signal.Id
                where signal.Symbol == symbol
                select item;
        }

        var counts = await query
            .GroupBy(item => item.Outcome)
            .Select(group => new
            {
                Outcome = group.Key,
                Count = group.LongCount()
            })
            .ToDictionaryAsync(
                item => item.Outcome,
                item => item.Count,
                cancellationToken);

        return new SignalTransitionMetricsWindowSnapshot(
            DateTimeOffset.UtcNow,
            fromInclusive,
            toExclusive,
            symbol,
            counts.GetValueOrDefault(SignalOutcome.Received),
            counts.GetValueOrDefault(SignalOutcome.Accepted),
            counts.GetValueOrDefault(SignalOutcome.Rejected));
    }
}
