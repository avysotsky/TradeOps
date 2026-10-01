using Microsoft.AspNetCore.Mvc;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/metrics/signal-transitions")]
public sealed class SignalTransitionMetricsController(
    ISignalTransitionMetricsRepository metricsRepository) : ControllerBase
{
    [HttpGet("window")]
    [ProducesResponseType(typeof(SignalTransitionMetricsWindowSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SignalTransitionMetricsWindowSnapshot>> GetWindowAsync(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        if (from is null || to is null)
        {
            return InvalidWindow("Both 'from' and 'to' query parameters are required.");
        }

        var fromUtc = from.Value.ToUniversalTime();
        var toUtc = to.Value.ToUniversalTime();

        if (fromUtc >= toUtc)
        {
            return InvalidWindow("'from' must be earlier than 'to'. The window uses [from, to) semantics.");
        }

        var normalizedSymbol = string.IsNullOrWhiteSpace(symbol)
            ? null
            : symbol.Trim().ToUpperInvariant();

        if (normalizedSymbol is { Length: > 50 })
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid signal transition metrics symbol",
                detail: "'symbol' must be 50 characters or fewer after trimming.");
        }

        var metrics = await metricsRepository.GetWindowAsync(
            fromUtc,
            toUtc,
            normalizedSymbol,
            cancellationToken);

        return Ok(metrics);
    }

    private ActionResult InvalidWindow(string detail) =>
        Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid signal transition metrics window",
            detail: detail);
}
