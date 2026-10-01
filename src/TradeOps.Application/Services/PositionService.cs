using Microsoft.Extensions.Logging;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Application.Services;

public sealed class PositionService(
    IFillRepository fillRepository,
    IPositionSnapshotRepository snapshotRepository,
    IExchangeClient exchangeClient,
    ILogger<PositionService> logger) : IPositionService
{
    public async Task<IReadOnlyCollection<PositionState>> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var fills = await fillRepository.GetPositionFillsAsync(
            cancellationToken: cancellationToken);

        if (fills.Count == 0)
        {
            return Array.Empty<PositionState>();
        }

        IReadOnlyCollection<Position> exchangePositions;
        try
        {
            exchangePositions = await exchangeClient.GetPositionsAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not obtain exchange mark prices. Local positions will be calculated without unrealized PnL.");
            exchangePositions = Array.Empty<Position>();
        }

        var markPrices = exchangePositions
            .Where(position => position.MarkPrice > 0m)
            .GroupBy(position => position.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().MarkPrice,
                StringComparer.OrdinalIgnoreCase);

        var calculatedAt = DateTimeOffset.UtcNow;

        return fills
            .GroupBy(fill => fill.Symbol, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => PositionPnLCalculator.Calculate(
                group.Key,
                group,
                markPrices.TryGetValue(group.Key, out var markPrice)
                    ? markPrice
                    : null,
                calculatedAt))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PositionState>> CaptureSnapshotsAsync(
        CancellationToken cancellationToken = default)
    {
        var states = await GetCurrentAsync(cancellationToken);
        if (states.Count == 0)
        {
            return states;
        }

        var snapshots = states
            .Select(state => new PositionSnapshot
            {
                Id = Guid.NewGuid(),
                Symbol = state.Symbol,
                Side = state.Side,
                Quantity = state.Quantity,
                AverageEntryPrice = state.AverageEntryPrice,
                RealizedPnL = state.RealizedPnL,
                MarkPrice = state.MarkPrice,
                UnrealizedPnL = state.UnrealizedPnL,
                CapturedAt = state.CalculatedAt
            })
            .ToArray();

        await snapshotRepository.AddRangeAsync(snapshots, cancellationToken);
        return states;
    }
}
