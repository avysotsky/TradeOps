using Microsoft.AspNetCore.Mvc;
using TradeOps.Application.Interfaces;
using TradeOps.Domain.Entities;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IExchangeClient exchangeClient) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<Order>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Order>>> GetOpenOrders(
        CancellationToken cancellationToken)
    {
        var orders = await exchangeClient.GetOpenOrdersAsync(cancellationToken);
        return Ok(orders);
    }
}
