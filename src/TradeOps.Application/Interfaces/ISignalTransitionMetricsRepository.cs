using TradeOps.Application.Models;

namespace TradeOps.Application.Interfaces;

public interface ISignalTransitionMetricsRepository
{
    Task<SignalTransitionMetricsWindowSnapshot> GetWindowAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string? symbol = null,
        CancellationToken cancellationToken = default);

    Task<SignalTransitionMetricsSeriesSnapshot> GetSeriesAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string bucket,
        TimeSpan bucketSize,
        string? symbol = null,
        CancellationToken cancellationToken = default);
}
