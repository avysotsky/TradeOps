using Microsoft.AspNetCore.Mvc;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/metrics")]
public sealed class MetricsController(IExecutionMetricsRepository metricsRepository) : ControllerBase
{
    [HttpGet("execution")]
    [ProducesResponseType(typeof(ExecutionMetricsSnapshot), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExecutionMetricsSnapshot>> GetExecutionAsync(
        CancellationToken cancellationToken)
    {
        var metrics = await metricsRepository.GetAsync(cancellationToken);
        return Ok(metrics);
    }

    [HttpGet("execution/window")]
    [ProducesResponseType(typeof(ExecutionMetricsWindowSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExecutionMetricsWindowSnapshot>> GetExecutionWindowAsync(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        if (from is null || to is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid execution metrics window",
                detail: "Both 'from' and 'to' query parameters are required.");
        }

        var fromUtc = from.Value.ToUniversalTime();
        var toUtc = to.Value.ToUniversalTime();

        if (fromUtc >= toUtc)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid execution metrics window",
                detail: "'from' must be earlier than 'to'. The window uses [from, to) semantics.");
        }

        var normalizedSymbol = string.IsNullOrWhiteSpace(symbol)
            ? null
            : symbol.Trim().ToUpperInvariant();

        if (normalizedSymbol is { Length: > 50 })
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid execution metrics symbol",
                detail: "'symbol' must be 50 characters or fewer after trimming.");
        }

        var metrics = await metricsRepository.GetWindowAsync(
            fromUtc,
            toUtc,
            normalizedSymbol,
            cancellationToken);

        return Ok(metrics);
    }
}
