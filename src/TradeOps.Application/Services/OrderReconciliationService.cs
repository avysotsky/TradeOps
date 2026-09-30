using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services;

public sealed class OrderReconciliationService(
    IExchangeClient exchangeClient,
    IOrderRepository orderRepository,
    IOrderStateMachine orderStateMachine) : IOrderReconciliationService
{
    public async Task<ReconciliationSummary> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        var localOrders = await orderRepository.GetReconciliationCandidatesAsync(cancellationToken);
        var issues = new List<ReconciliationIssue>();
        var updated = 0;
        var unchanged = 0;
        var missing = 0;

        foreach (var localOrder in localOrders)
        {
            var exchangeOrder = !string.IsNullOrWhiteSpace(localOrder.ExchangeOrderId)
                ? await exchangeClient.GetOrderAsync(localOrder.ExchangeOrderId, cancellationToken)
                : await exchangeClient.GetOrderByClientOrderIdAsync(localOrder.ClientOrderId, cancellationToken);

            if (exchangeOrder is null)
            {
                missing++;
                var previousStatus = localOrder.Status;

                if (localOrder.Status != OrderStatus.Unknown)
                {
                    orderStateMachine.Apply(
                        localOrder,
                        OrderStatus.Unknown,
                        localOrder.FilledQuantity,
                        localOrder.AverageFillPrice,
                        localOrder.ExchangeOrderId,
                        localOrder.Price);

                    await orderRepository.UpdateAsync(localOrder, cancellationToken);
                    updated++;
                }
                else
                {
                    unchanged++;
                }

                issues.Add(new ReconciliationIssue(
                    localOrder.ClientOrderId,
                    previousStatus,
                    null,
                    "Exchange did not return the order. Local state remains unresolved."));

                continue;
            }

            if (!HasSameIdentity(localOrder, exchangeOrder))
            {
                issues.Add(new ReconciliationIssue(
                    localOrder.ClientOrderId,
                    localOrder.Status,
                    exchangeOrder.Status,
                    "Exchange order identity does not match local symbol, side, or requested quantity."));

                continue;
            }

            if (HasSameState(localOrder, exchangeOrder))
            {
                unchanged++;
                continue;
            }

            try
            {
                orderStateMachine.Apply(
                    localOrder,
                    exchangeOrder.Status,
                    exchangeOrder.FilledQuantity,
                    exchangeOrder.AverageFillPrice,
                    exchangeOrder.ExchangeOrderId,
                    exchangeOrder.Price);

                await orderRepository.UpdateAsync(localOrder, cancellationToken);
                updated++;
            }
            catch (InvalidOperationException exception)
            {
                issues.Add(new ReconciliationIssue(
                    localOrder.ClientOrderId,
                    localOrder.Status,
                    exchangeOrder.Status,
                    exception.Message));
            }
        }

        return new ReconciliationSummary(
            localOrders.Count,
            updated,
            unchanged,
            missing,
            issues);
    }

    private static bool HasSameIdentity(Order localOrder, Order exchangeOrder)
    {
        return string.Equals(localOrder.Symbol, exchangeOrder.Symbol, StringComparison.OrdinalIgnoreCase)
            && localOrder.Side == exchangeOrder.Side
            && localOrder.RequestedQuantity == exchangeOrder.RequestedQuantity;
    }

    private static bool HasSameState(Order localOrder, Order exchangeOrder)
    {
        return localOrder.Status == exchangeOrder.Status
            && localOrder.FilledQuantity == exchangeOrder.FilledQuantity
            && localOrder.AverageFillPrice == exchangeOrder.AverageFillPrice
            && localOrder.ExchangeOrderId == exchangeOrder.ExchangeOrderId;
    }
}
