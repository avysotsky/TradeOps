using Microsoft.AspNetCore.Mvc;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/risk")]
public sealed class RiskController(
    RiskSettings settings,
    IRiskState riskState) : ControllerBase
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
}
