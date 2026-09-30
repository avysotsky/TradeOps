using Microsoft.AspNetCore.Mvc;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(
    IOrderReconciliationService reconciliationService) : ControllerBase
{
    [HttpPost("reconcile")]
    [ProducesResponseType<ReconciliationSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconciliationSummary>> Reconcile(
        CancellationToken cancellationToken)
    {
        var result = await reconciliationService.ReconcileAsync(cancellationToken);
        return Ok(result);
    }
}
