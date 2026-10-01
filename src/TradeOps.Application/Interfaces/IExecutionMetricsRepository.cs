using TradeOps.Application.Models;

namespace TradeOps.Application.Interfaces;

public interface IExecutionMetricsRepository
{
    Task<ExecutionMetricsSnapshot> GetAsync(
        CancellationToken cancellationToken = default);

    Task<ExecutionMetricsWindowSnapshot> GetWindowAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default);
}
