using Microsoft.AspNetCore.Mvc;
using TradeOps.Api.Contracts;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController(
    IOrderReconciliationService reconciliationService,
    IPositionReconciliationService positionReconciliationService,
    IOperationalRunStatusRepository runStatusRepository) : ControllerBase
{
    [HttpPost("reconcile")]
    [ProducesResponseType<ReconciliationSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReconciliationSummary>> Reconcile(
        CancellationToken cancellationToken)
    {
        var result = await reconciliationService.ReconcileAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("reconcile/positions")]
    [ProducesResponseType<PositionReconciliationSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PositionReconciliationSummary>> ReconcilePositions(
        CancellationToken cancellationToken)
    {
        var result = await positionReconciliationService.ReconcileAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("reconciliation/status")]
    [ProducesResponseType<OperationalRunStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationalRunStatusResponse>> GetReconciliationStatus(
        CancellationToken cancellationToken)
    {
        var status = await runStatusRepository.GetAsync(
            OperationalRunTypes.OrderReconciliation,
            cancellationToken);

        return status is null ? NotFound() : Ok(ToResponse(status));
    }

    [HttpGet("recovery/status")]
    [ProducesResponseType<OperationalRunStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationalRunStatusResponse>> GetRecoveryStatus(
        CancellationToken cancellationToken)
    {
        var status = await runStatusRepository.GetAsync(
            OperationalRunTypes.RecoveryCycle,
            cancellationToken);

        return status is null ? NotFound() : Ok(ToResponse(status));
    }

    private static OperationalRunStatusResponse ToResponse(OperationalRunStatus status) => new(
        status.RunType,
        status.StartedAt,
        status.CompletedAt,
        status.IsRunning,
        status.Succeeded,
        status.OrdersScanned,
        status.OrdersUpdated,
        status.OrderIssues,
        status.OrdersMissingOnExchange,
        status.PositionsCompared,
        status.PositionMismatches,
        status.PositionSnapshots,
        status.ErrorMessage,
        status.UpdatedAt);
}
