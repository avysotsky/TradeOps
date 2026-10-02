using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Interfaces;

public interface IOperatorReadRepository
{
    Task<TradingSignal?> GetSignalByIdAsync(
        Guid signalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TradingSignal>> GetSignalsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<TradingSignal>> GetSignalsAsync(
        string? symbol,
        SignalOutcome? outcome,
        DateTimeOffset? fromInclusive,
        DateTimeOffset? toExclusive,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetSignalsAsync(limit, cancellationToken);

    Task<IReadOnlyCollection<TradingSignal>> GetSignalsAsync(
        string? symbol,
        SignalOutcome? outcome,
        string? executionIssueCode,
        DateTimeOffset? fromInclusive,
        DateTimeOffset? toExclusive,
        int limit,
        CancellationToken cancellationToken = default) =>
        GetSignalsAsync(
            symbol,
            outcome,
            fromInclusive,
            toExclusive,
            limit,
            cancellationToken);

    Task<Order?> GetLocalOrderAsync(
        string idOrClientOrderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<OrderLifecycleEvent>> GetOrderHistoryAsync(
        string idOrClientOrderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FillAuditRecord>> GetFillsAsync(
        string? symbol,
        int limit,
        CancellationToken cancellationToken = default);
}
