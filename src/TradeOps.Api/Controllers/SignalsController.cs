using Microsoft.AspNetCore.Mvc;
using TradeOps.Api.Contracts;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/signals")]
public sealed class SignalsController(IOrderManager orderManager) : ControllerBase
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
            Id = Guid.NewGuid(),
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

        var result = await orderManager.ExecuteSignalAsync(
            signal,
            cancellationToken);

        return result.Accepted
            ? Ok(result)
            : UnprocessableEntity(result);
    }
}
