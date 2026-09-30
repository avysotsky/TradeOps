using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services;

public sealed class RiskEngine(
    IExchangeClient exchangeClient,
    IRiskState riskState,
    RiskSettings settings) : IRiskEngine
{
    public async Task<RiskDecision> CheckAsync(
        TradingSignal signal,
        CancellationToken cancellationToken = default)
    {
        var reasons = new List<string>();

        if (!settings.TradingEnabled)
        {
            reasons.Add("Trading is disabled.");
        }

        if (settings.EmergencyStop)
        {
            reasons.Add("Emergency stop is active.");
        }

        if (!settings.AllowedSymbols.Contains(signal.Symbol))
        {
            reasons.Add($"Symbol '{signal.Symbol}' is not allowed.");
        }

        if (signal.RequestedQuantity <= 0)
        {
            reasons.Add("Requested quantity must be greater than zero.");
        }
        else if (signal.RequestedQuantity > settings.MaxOrderSize)
        {
            reasons.Add(
                $"Order size {signal.RequestedQuantity} exceeds max order size {settings.MaxOrderSize}.");
        }

        if (riskState.CurrentDailyPnl <= -settings.MaxDailyLoss)
        {
            reasons.Add(
                $"Daily loss limit reached. Current daily PnL: {riskState.CurrentDailyPnl}.");
        }

        var positions = await exchangeClient.GetPositionsAsync(cancellationToken);
        var currentPosition = positions.FirstOrDefault(
            position => string.Equals(
                position.Symbol,
                signal.Symbol,
                StringComparison.OrdinalIgnoreCase));

        var isNewPosition = currentPosition is null || currentPosition.Quantity == 0;

        if (isNewPosition && positions.Count >= settings.MaxOpenPositions)
        {
            reasons.Add(
                $"Max open positions limit reached: {settings.MaxOpenPositions}.");
        }

        var currentSignedQuantity = currentPosition is null
            ? 0m
            : currentPosition.Side == OrderSide.Buy
                ? currentPosition.Quantity
                : -currentPosition.Quantity;

        var requestedSignedQuantity = signal.Side == OrderSide.Buy
            ? signal.RequestedQuantity
            : -signal.RequestedQuantity;

        var projectedAbsolutePosition = Math.Abs(
            currentSignedQuantity + requestedSignedQuantity);

        if (projectedAbsolutePosition > settings.MaxPositionSize)
        {
            reasons.Add(
                $"Projected position {projectedAbsolutePosition} exceeds max position size {settings.MaxPositionSize}.");
        }

        return reasons.Count == 0
            ? RiskDecision.Allowed()
            : new RiskDecision(false, reasons);
    }
}
