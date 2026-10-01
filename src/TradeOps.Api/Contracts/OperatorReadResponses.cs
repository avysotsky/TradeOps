using TradeOps.Application.Models;
using TradeOps.Domain.Enums;

namespace TradeOps.Api.Contracts;

public sealed record TradingSignalAuditResponse(
    Guid SignalId,
    string Symbol,
    OrderSide Side,
    string? SignalType,
    decimal RequestedQuantity,
    decimal? RiskPercent,
    decimal? StopLoss,
    decimal? TakeProfit,
    DateTimeOffset ReceivedAt,
    string? Source,
    SignalOutcome Outcome,
    IReadOnlyCollection<string> RiskRejectionReasons,
    Guid? OrderId,
    string? ClientOrderId);

public sealed record LocalOrderResponse(
    Guid Id,
    string? ExchangeOrderId,
    string ClientOrderId,
    string Symbol,
    OrderSide Side,
    OrderType OrderType,
    decimal RequestedQuantity,
    decimal FilledQuantity,
    decimal? AverageFillPrice,
    decimal? Price,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OrderCancellationResponse(
    OrderCancellationOutcome Outcome,
    LocalOrderResponse? Order,
    string? Message);

public sealed record FillAuditResponse(
    Guid FillId,
    Guid OrderId,
    string ExchangeFillId,
    string ClientOrderId,
    string Symbol,
    OrderSide Side,
    decimal Quantity,
    decimal Price,
    decimal? Fee,
    string? FeeCurrency,
    DateTimeOffset FilledAt);

public sealed record UnconvertedFeeResponse(
    string ExchangeFillId,
    decimal Amount,
    string? Currency,
    DateTimeOffset FilledAt);

public sealed record DailyPnlResponse(
    DateOnly UtcDate,
    string SettlementCurrency,
    decimal GrossRealizedPnL,
    decimal SettlementFees,
    decimal? NetRealizedPnL,
    IReadOnlyCollection<UnconvertedFeeResponse> UnconvertedFees,
    bool IsComplete);
