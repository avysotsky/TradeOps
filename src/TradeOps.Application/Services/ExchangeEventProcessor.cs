using Microsoft.Extensions.Logging;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;

namespace TradeOps.Application.Services;

public sealed class ExchangeEventProcessor(
    IOrderRepository orderRepository,
    IOrderStateMachine orderStateMachine,
    ILogger<ExchangeEventProcessor> logger) : IExchangeEventProcessor
{
    public async Task ProcessOrderUpdateAsync(
        ExchangeOrderUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.ClientOrderId))
        {
            logger.LogDebug("Ignoring exchange order update {ExchangeOrderId} without ClientOrderId.", update.ExchangeOrderId);
            return;
        }

        var localOrder = await orderRepository.GetByClientOrderIdAsync(update.ClientOrderId, cancellationToken);
        if (localOrder is null)
        {
            logger.LogWarning("Received exchange order update for unknown ClientOrderId {ClientOrderId}.", update.ClientOrderId);
            return;
        }

        if (!string.Equals(localOrder.Symbol, update.Symbol, StringComparison.OrdinalIgnoreCase)
            || localOrder.Side != update.Side
            || localOrder.RequestedQuantity != update.RequestedQuantity)
        {
            logger.LogWarning("Ignoring identity-mismatched exchange update for {ClientOrderId}.", update.ClientOrderId);
            return;
        }

        if (localOrder.Status == update.Status
            && localOrder.FilledQuantity == update.FilledQuantity
            && localOrder.AverageFillPrice == update.AverageFillPrice
            && localOrder.ExchangeOrderId == update.ExchangeOrderId)
        {
            logger.LogDebug("Ignoring duplicate exchange order event for {ClientOrderId} in state {Status}.", update.ClientOrderId, update.Status);
            return;
        }

        try
        {
            var previousStatus = localOrder.Status;
            orderStateMachine.Apply(
                localOrder,
                update.Status,
                update.FilledQuantity,
                update.AverageFillPrice,
                update.ExchangeOrderId,
                update.Price);

            await orderRepository.UpdateAsync(localOrder, cancellationToken);

            logger.LogInformation(
                "Applied websocket order update {ClientOrderId}: {PreviousStatus} -> {CurrentStatus}, filled {Filled}/{Requested}.",
                update.ClientOrderId,
                previousStatus,
                localOrder.Status,
                localOrder.FilledQuantity,
                localOrder.RequestedQuantity);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Rejected websocket order transition for {ClientOrderId}.", update.ClientOrderId);
        }
    }

    public Task ProcessExecutionUpdateAsync(
        ExchangeExecutionUpdate update,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Execution {ExecutionId}: {Side} {Quantity} {Symbol} at {Price}; ClientOrderId={ClientOrderId}; Fee={Fee} {FeeCurrency}.",
            update.ExecutionId,
            update.Side,
            update.Quantity,
            update.Symbol,
            update.Price,
            update.ClientOrderId,
            update.Fee,
            update.FeeCurrency);

        return Task.CompletedTask;
    }
}
