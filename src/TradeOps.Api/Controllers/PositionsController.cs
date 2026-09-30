using Microsoft.AspNetCore.Mvc;
using TradeOps.Application.Interfaces;
using TradeOps.Domain.Entities;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/positions")]
public sealed class PositionsController(IExchangeClient exchangeClient) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<Position>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Position>>> Get(
        CancellationToken cancellationToken)
    {
        var positions = await exchangeClient.GetPositionsAsync(cancellationToken);
        return Ok(positions);
    }
}
