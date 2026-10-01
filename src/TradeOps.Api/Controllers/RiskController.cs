using Microsoft.AspNetCore.Mvc;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/risk")]
public sealed class RiskController(
    RiskSettings settings,
    IRiskState riskState,
    IRiskEventRepository riskEventRepository) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            settings.MaxPositionSize,
            settings.MaxOrderSize,
            settings.MaxDailyLoss,
            settings.MaxOpenPositions,
            AllowedSymbols = settings.AllowedSymbols.OrderBy(x => x),
            settings.TradingEnabled,
            settings.EmergencyStop,
            riskState.CurrentDailyPnl
        });
    }

    [HttpGet("events")]
    [ProducesResponseType<IReadOnlyCollection<RiskEvent>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<RiskEvent>>> GetEvents(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var events = await riskEventRepository.GetRecentAsync(limit, cancellationToken);
        return Ok(events);
    }
}
