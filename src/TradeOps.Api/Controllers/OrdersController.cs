using Microsoft.AspNetCore.Mvc;
using TradeOps.Api.Contracts;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(
    IExchangeClient exchangeClient,
    IOperatorReadRepository operatorReadRepository,
    IOrderCancellationService orderCancellationService,
    IOrderBulkCancellationService orderBulkCancellationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<Order>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Order>>> GetOpenOrders(
        CancellationToken cancellationToken)
    {
        var orders = await exchangeClient.GetOpenOrdersAsync(cancellationToken);
        return Ok(orders);
    }

    [HttpGet("local/{idOrClientOrderId}")]
    [ProducesResponseType<LocalOrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocalOrderResponse>> GetLocalOrder(
        string idOrClientOrderId,
        CancellationToken cancellationToken)
    {
        var order = await operatorReadRepository.GetLocalOrderAsync(
            idOrClientOrderId,
            cancellationToken);

        return order is null
            ? NotFound()
            : Ok(OperatorResponseMapper.ToLocalOrder(order));
    }

    [HttpGet("local/{idOrClientOrderId}/history")]
    [ProducesResponseType<IReadOnlyCollection<OrderLifecycleEventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<OrderLifecycleEventResponse>>> GetLocalOrderHistory(
        string idOrClientOrderId,
        CancellationToken cancellationToken)
    {
        var history = await operatorReadRepository.GetOrderHistoryAsync(
            idOrClientOrderId,
            cancellationToken);

        if (history.Count == 0)
        {
            return NotFound();
        }

        return Ok(history.Select(item => new OrderLifecycleEventResponse(
            item.Id,
            item.OrderId,
            item.ClientOrderId,
            item.PreviousStatus,
            item.Status,
            item.FilledQuantity,
            item.AverageFillPrice,
            item.ExchangeOrderId,
            item.Source,
            item.OccurredAt)).ToArray());
    }

    [HttpPost("local/{idOrClientOrderId}/cancel")]
    [ProducesResponseType<OrderCancellationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<OrderCancellationResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<OrderCancellationResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<OrderCancellationResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<OrderCancellationResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<OrderCancellationResponse>> CancelLocalOrder(
        string idOrClientOrderId,
        CancellationToken cancellationToken)
    {
        var result = await orderCancellationService.CancelAsync(
            idOrClientOrderId,
            cancellationToken);
        var response = OperatorResponseMapper.ToOrderCancellation(result);

        return result.Outcome switch
        {
            OrderCancellationOutcome.NotFound => NotFound(response),
            OrderCancellationOutcome.NotCancellable => Conflict(response),
            OrderCancellationOutcome.Unresolved =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, response),
            OrderCancellationOutcome.CancellationRequested => Accepted(response),
            _ => Ok(response)
        };
    }

    [HttpPost("local/cancel-all")]
    [ProducesResponseType<BulkOrderCancellationResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkOrderCancellationResponse>> CancelAllLocalOrders(
        [FromQuery] string? symbol,
        CancellationToken cancellationToken)
    {
        var result = await orderBulkCancellationService.CancelOpenOrdersAsync(
            symbol,
            cancellationToken);

        return Ok(OperatorResponseMapper.ToBulkOrderCancellation(result));
    }

    [HttpGet("{exchangeOrderId}")]
    [ProducesResponseType<Order>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Order>> GetByExchangeOrderId(
        string exchangeOrderId,
        CancellationToken cancellationToken)
    {
        var order = await exchangeClient.GetOrderAsync(exchangeOrderId, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpGet("by-client/{clientOrderId}")]
    [ProducesResponseType<Order>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Order>> GetByClientOrderId(
        string clientOrderId,
        CancellationToken cancellationToken)
    {
        var order = await exchangeClient.GetOrderByClientOrderIdAsync(
            clientOrderId,
            cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    [HttpDelete("{exchangeOrderId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        string exchangeOrderId,
        CancellationToken cancellationToken)
    {
        var order = await exchangeClient.GetOrderAsync(exchangeOrderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        if (order.Status is OrderStatus.Filled
            or OrderStatus.Cancelled
            or OrderStatus.Rejected)
        {
            return Conflict(new
            {
                message = $"Order '{exchangeOrderId}' is already terminal ({order.Status}) and cannot be cancelled."
            });
        }

        await exchangeClient.CancelOrderAsync(exchangeOrderId, cancellationToken);
        return Accepted();
    }
}
