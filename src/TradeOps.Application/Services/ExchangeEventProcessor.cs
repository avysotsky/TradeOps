using Microsoft.Extensions.Logging;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Application.Services;

public sealed class ExchangeEventProcessor(
    IOrderRepository orderRepository,
    IFillRepository fillRepository,
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

    public async Task ProcessExecutionUpdateAsync(
        ExchangeExecutionUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(update.ExecutionId)
            || string.IsNullOrWhiteSpace(update.ClientOrderId))
        {
            logger.LogWarning(
                "Ignoring execution update without ExecutionId/ClientOrderId. ExchangeOrderId={ExchangeOrderId}.",
                update.ExchangeOrderId);
            return;
        }

        if (update.Quantity <= 0m || update.Price <= 0m)
        {
            logger.LogWarning(
                "Ignoring invalid execution {ExecutionId}: Quantity={Quantity}, Price={Price}.",
                update.ExecutionId,
                update.Quantity,
                update.Price);
            return;
        }

        var localOrder = await orderRepository.GetByClientOrderIdAsync(update.ClientOrderId, cancellationToken);
        if (localOrder is null)
        {
            logger.LogWarning(
                "Received execution {ExecutionId} for unknown ClientOrderId {ClientOrderId}.",
                update.ExecutionId,
                update.ClientOrderId);
            return;
        }

        var exchangeOrderIdMismatch = !string.IsNullOrWhiteSpace(localOrder.ExchangeOrderId)
            && !string.IsNullOrWhiteSpace(update.ExchangeOrderId)
            && !string.Equals(localOrder.ExchangeOrderId, update.ExchangeOrderId, StringComparison.Ordinal);

        if (!string.Equals(localOrder.Symbol, update.Symbol, StringComparison.OrdinalIgnoreCase)
            || localOrder.Side != update.Side
            || exchangeOrderIdMismatch)
        {
            logger.LogWarning(
                "Ignoring identity-mismatched execution {ExecutionId} for {ClientOrderId}.",
                update.ExecutionId,
                update.ClientOrderId);
            return;
        }

        var fill = new Fill
        {
            Id = Guid.NewGuid(),
            OrderId = localOrder.Id,
            ExchangeFillId = update.ExecutionId,
            Quantity = update.Quantity,
            Price = update.Price,
            Fee = update.Fee,
            FeeCurrency = update.FeeCurrency,
            FilledAt = update.ExecutedAt
        };

        var inserted = await fillRepository.TryAddAsync(fill, cancellationToken);
        if (!inserted)
        {
            logger.LogDebug(
                "Ignoring duplicate execution event {ExecutionId} for {ClientOrderId}.",
                update.ExecutionId,
                update.ClientOrderId);
            return;
        }

        logger.LogInformation(
            "Persisted execution {ExecutionId}: {Side} {Quantity} {Symbol} at {Price}; ClientOrderId={ClientOrderId}; Fee={Fee} {FeeCurrency}.",
            update.ExecutionId,
            update.Side,
            update.Quantity,
            update.Symbol,
            update.Price,
            update.ClientOrderId,
            update.Fee,
            update.FeeCurrency);
    }
}
