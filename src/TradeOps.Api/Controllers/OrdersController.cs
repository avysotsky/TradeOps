using Microsoft.AspNetCore.Mvc;
using TradeOps.Api.Contracts;
using TradeOps.Application.Interfaces;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(
    IExchangeClient exchangeClient,
    IOperatorReadRepository operatorReadRepository) : ControllerBase
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

        return order is null ? NotFound() : Ok(ToLocalResponse(order));
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

    private static LocalOrderResponse ToLocalResponse(Order order)
    {
        return new LocalOrderResponse(
            order.Id,
            order.ExchangeOrderId,
            order.ClientOrderId,
            order.Symbol,
            order.Side,
            order.OrderType,
            order.RequestedQuantity,
            order.FilledQuantity,
            order.AverageFillPrice,
            order.Price,
            order.Status,
            order.CreatedAt,
            order.UpdatedAt);
    }
}
