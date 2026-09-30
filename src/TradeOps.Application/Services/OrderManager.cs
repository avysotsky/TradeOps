using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services;

public sealed class OrderManager(
    IRiskEngine riskEngine,
    IExchangeClient exchangeClient) : IOrderManager
{
    public async Task<SignalExecutionResult> ExecuteSignalAsync(
        TradingSignal signal,
        CancellationToken cancellationToken = default)
    {
        var riskDecision = await riskEngine.CheckAsync(signal, cancellationToken);

        if (!riskDecision.IsAllowed)
        {
            return new SignalExecutionResult(
                signal.Id,
                false,
                riskDecision.Reasons,
                null);
        }

        // Day 3 provisional ID. Day 4 will replace this with a dedicated
        // client-order-id generator plus persistence/idempotency handling.
        var clientOrderId = $"tradeops-{signal.Id:N}";

        var orderRequest = new PlaceOrderRequest(
            clientOrderId,
            signal.Symbol,
            signal.Side,
            OrderType.Market,
            signal.RequestedQuantity);

        var orderResult = await exchangeClient.PlaceOrderAsync(
            orderRequest,
            cancellationToken);

        return new SignalExecutionResult(
            signal.Id,
            true,
            Array.Empty<string>(),
            orderResult);
    }
}
