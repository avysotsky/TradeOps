using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services;

public sealed class OrderManager(
    IRiskEngine riskEngine,
    IExchangeClient exchangeClient,
    IOrderRepository orderRepository,
    IClientOrderIdGenerator clientOrderIdGenerator) : IOrderManager
{
    public async Task<SignalExecutionResult> ExecuteSignalAsync(
        TradingSignal signal,
        CancellationToken cancellationToken = default)
    {
        var clientOrderId = clientOrderIdGenerator.Generate(signal.Id);

        var existingOrder = await orderRepository.GetByClientOrderIdAsync(
            clientOrderId,
            cancellationToken);

        if (existingOrder is not null)
        {
            return FromExistingOrder(signal.Id, existingOrder);
        }

        var riskDecision = await riskEngine.CheckAsync(signal, cancellationToken);

        if (!riskDecision.IsAllowed)
        {
            return new SignalExecutionResult(
                signal.Id,
                false,
                riskDecision.Reasons,
                null);
        }

        var now = DateTimeOffset.UtcNow;
        var localOrder = new Order
        {
            Id = Guid.NewGuid(),
            ClientOrderId = clientOrderId,
            Symbol = signal.Symbol,
            Side = signal.Side,
            OrderType = OrderType.Market,
            RequestedQuantity = signal.RequestedQuantity,
            FilledQuantity = 0m,
            Status = OrderStatus.Created,
            CreatedAt = now,
            UpdatedAt = now
        };

        var inserted = await orderRepository.TryAddAsync(
            localOrder,
            cancellationToken);

        if (!inserted)
        {
            existingOrder = await orderRepository.GetByClientOrderIdAsync(
                clientOrderId,
                cancellationToken);

            if (existingOrder is null)
            {
                throw new InvalidOperationException(
                    $"Duplicate ClientOrderId '{clientOrderId}' was detected, but the existing order could not be loaded.");
            }

            return FromExistingOrder(signal.Id, existingOrder);
        }

        localOrder.Status = OrderStatus.Submitted;
        localOrder.UpdatedAt = DateTimeOffset.UtcNow;
        await orderRepository.UpdateAsync(localOrder, cancellationToken);

        var orderRequest = new PlaceOrderRequest(
            clientOrderId,
            signal.Symbol,
            signal.Side,
            OrderType.Market,
            signal.RequestedQuantity);

        try
        {
            var exchangeResult = await exchangeClient.PlaceOrderAsync(
                orderRequest,
                cancellationToken);

            ApplyExchangeResult(localOrder, exchangeResult);
            await orderRepository.UpdateAsync(localOrder, cancellationToken);

            return new SignalExecutionResult(
                signal.Id,
                true,
                Array.Empty<string>(),
                exchangeResult);
        }
        catch (TimeoutException)
        {
            var exchangeOrder = await exchangeClient.GetOrderByClientOrderIdAsync(
                clientOrderId,
                cancellationToken);

            if (exchangeOrder is not null)
            {
                ApplyExchangeOrder(localOrder, exchangeOrder);
                await orderRepository.UpdateAsync(localOrder, cancellationToken);

                return FromExistingOrder(signal.Id, localOrder);
            }

            // Never retry blindly after a timeout: the exchange may have accepted
            // the request even though the response was lost.
            localOrder.Status = OrderStatus.Unknown;
            localOrder.UpdatedAt = DateTimeOffset.UtcNow;
            await orderRepository.UpdateAsync(localOrder, cancellationToken);

            return FromExistingOrder(signal.Id, localOrder);
        }
    }

    private static SignalExecutionResult FromExistingOrder(Guid signalId, Order order)
    {
        var result = new OrderResult(
            order.ExchangeOrderId,
            order.ClientOrderId,
            order.Status,
            order.FilledQuantity,
            order.AverageFillPrice);

        return new SignalExecutionResult(
            signalId,
            true,
            Array.Empty<string>(),
            result);
    }

    private static void ApplyExchangeResult(Order localOrder, OrderResult result)
    {
        localOrder.ExchangeOrderId = result.ExchangeOrderId;
        localOrder.Status = result.Status;
        localOrder.FilledQuantity = result.FilledQuantity;
        localOrder.AverageFillPrice = result.AverageFillPrice;
        localOrder.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ApplyExchangeOrder(Order localOrder, Order exchangeOrder)
    {
        localOrder.ExchangeOrderId = exchangeOrder.ExchangeOrderId;
        localOrder.Status = exchangeOrder.Status;
        localOrder.FilledQuantity = exchangeOrder.FilledQuantity;
        localOrder.AverageFillPrice = exchangeOrder.AverageFillPrice;
        localOrder.Price = exchangeOrder.Price;
        localOrder.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
