using Microsoft.AspNetCore.Mvc;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/risk")]
public sealed class RiskController(
    RiskSettings settings,
    IRiskControlService riskControlService,
    IRiskEventRepository riskEventRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var control = await riskControlService.GetSnapshotAsync(cancellationToken);

        return Ok(new
        {
            settings.MaxPositionSize,
            settings.MaxOrderSize,
            settings.MaxDailyLoss,
            settings.MaxOpenPositions,
            AllowedSymbols = settings.AllowedSymbols.OrderBy(x => x),
            control.TradingEnabled,
            control.EmergencyStop,
            control.EmergencyStopReason,
            control.DailyRealizedPnL,
            control.ActivePositionMismatchCount,
            control.HasPositionMismatch,
            control.UpdatedAt
        });
    }

    [HttpPost("trading-enabled")]
    [ProducesResponseType<RiskControlSnapshot>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskControlSnapshot>> SetTradingEnabled(
        [FromBody] SetTradingEnabledRequest request,
        CancellationToken cancellationToken)
    {
        var result = await riskControlService.SetTradingEnabledAsync(
            request.Enabled,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("emergency-stop")]
    [ProducesResponseType<RiskControlSnapshot>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskControlSnapshot>> SetEmergencyStop(
        [FromBody] SetEmergencyStopRequest request,
        CancellationToken cancellationToken)
    {
        var result = await riskControlService.SetEmergencyStopAsync(
            request.Enabled,
            request.Reason,
            cancellationToken);
        return Ok(result);
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

public sealed record SetTradingEnabledRequest(bool Enabled);

public sealed record SetEmergencyStopRequest(bool Enabled, string? Reason = null);
