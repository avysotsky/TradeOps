using Microsoft.AspNetCore.Mvc;
using TradeOps.Api.Contracts;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/signals")]
public sealed class SignalsController(
    ISignalExecutionService signalExecutionService,
    IOperatorReadRepository operatorReadRepository) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SignalExecutionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<SignalExecutionResult>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SignalExecutionResult>> Post(
        CreateTradingSignalRequest request,
        CancellationToken cancellationToken)
    {
        var signal = new TradingSignal
        {
            Id = request.SignalId ?? Guid.NewGuid(),
            Symbol = request.Symbol.Trim().ToUpperInvariant(),
            Side = request.Side,
            SignalType = "External",
            RequestedQuantity = request.Quantity,
            RiskPercent = request.RiskPercent,
            StopLoss = request.StopLoss,
            TakeProfit = request.TakeProfit,
            CreatedAt = DateTimeOffset.UtcNow,
            Source = request.Source
        };

        var result = await signalExecutionService.ExecuteSignalAsync(
            signal,
            cancellationToken);

        return result.Accepted
            ? Ok(result)
            : UnprocessableEntity(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TradingSignalAuditResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TradingSignalAuditResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var signal = await operatorReadRepository.GetSignalByIdAsync(id, cancellationToken);
        return signal is null ? NotFound() : Ok(ToResponse(signal));
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<TradingSignalAuditResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TradingSignalAuditResponse>>> GetRecent(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var signals = await operatorReadRepository.GetSignalsAsync(limit, cancellationToken);
        return Ok(signals.Select(ToResponse).ToArray());
    }

    private static TradingSignalAuditResponse ToResponse(TradingSignal signal)
    {
        return new TradingSignalAuditResponse(
            signal.Id,
            signal.Symbol,
            signal.Side,
            signal.SignalType,
            signal.RequestedQuantity,
            signal.RiskPercent,
            signal.StopLoss,
            signal.TakeProfit,
            signal.CreatedAt,
            signal.Source,
            signal.Outcome,
            signal.RiskRejectionReasons,
            signal.OrderId,
            signal.ClientOrderId);
    }
}
