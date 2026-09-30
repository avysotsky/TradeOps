using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services;

public sealed class OrderManager(
    IRiskEngine riskEngine,
    IExchangeClient exchangeClient,
    IOrderRepository orderRepository,
    IClientOrderIdGenerator clientOrderIdGenerator,
    IOrderStateMachine orderStateMachine) : IOrderManager
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

        orderStateMachine.Apply(
            localOrder,
            OrderStatus.Submitted,
            0m,
            null);

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

            orderStateMachine.Apply(
                localOrder,
                exchangeResult.Status,
                exchangeResult.FilledQuantity,
                exchangeResult.AverageFillPrice,
                exchangeResult.ExchangeOrderId);

            await orderRepository.UpdateAsync(localOrder, cancellationToken);

            return FromExistingOrder(signal.Id, localOrder);
        }
        catch (TimeoutException)
        {
            var exchangeOrder = await exchangeClient.GetOrderByClientOrderIdAsync(
                clientOrderId,
                cancellationToken);

            if (exchangeOrder is not null)
            {
                orderStateMachine.Apply(
                    localOrder,
                    exchangeOrder.Status,
                    exchangeOrder.FilledQuantity,
                    exchangeOrder.AverageFillPrice,
                    exchangeOrder.ExchangeOrderId,
                    exchangeOrder.Price);

                await orderRepository.UpdateAsync(localOrder, cancellationToken);
                return FromExistingOrder(signal.Id, localOrder);
            }

            // Never retry blindly after a timeout: the exchange may have accepted
            // the request even though the response was lost.
            orderStateMachine.Apply(
                localOrder,
                OrderStatus.Unknown,
                localOrder.FilledQuantity,
                localOrder.AverageFillPrice,
                localOrder.ExchangeOrderId,
                localOrder.Price);

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
}
