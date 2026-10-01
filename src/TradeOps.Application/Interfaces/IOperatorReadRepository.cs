using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Application.Interfaces;

public interface IOperatorReadRepository
{
    Task<TradingSignal?> GetSignalByIdAsync(
        Guid signalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TradingSignal>> GetSignalsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<Order?> GetLocalOrderAsync(
        string idOrClientOrderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FillAuditRecord>> GetFillsAsync(
        string? symbol,
        int limit,
        CancellationToken cancellationToken = default);
}
