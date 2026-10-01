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
}
