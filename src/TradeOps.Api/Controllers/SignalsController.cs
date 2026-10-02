using Microsoft.AspNetCore.Mvc;
using TradeOps.Api.Contracts;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/signals")]
public sealed class SignalsController(
    ISignalExecutionService signalExecutionService,
    IOperatorReadRepository operatorReadRepository,
    ITradingSignalOutcomeHistoryRepository outcomeHistoryRepository) : ControllerBase
{
    private const int MaxAuditLimit = 200;
    private const int MaxSymbolLength = 50;

    [HttpPost]
    [ProducesResponseType<SignalExecutionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<SignalExecutionResult>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<SignalIdConflictResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SignalExecutionResult>> Post(
        CreateTradingSignalRequest request,
        CancellationToken cancellationToken)
    {
        var validationErrors = TradingSignalRequestValidator.Validate(request);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors)
            {
                Title = "External signal request validation failed.",
                Status = StatusCodes.Status400BadRequest
            });
        }

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

        SignalExecutionResult result;
        try
        {
            result = await signalExecutionService.ExecuteSignalAsync(
                signal,
                cancellationToken);
        }
        catch (SignalIdConflictException exception)
        {
            return Conflict(new SignalIdConflictResponse(
                exception.SignalId,
                exception.Message,
                exception.ConflictingFields));
        }
        catch (ClientOrderIdConflictException exception)
        {
            return Conflict(new SignalIdConflictResponse(
                exception.SignalId,
                exception.Message,
                exception.ConflictingFields));
        }

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

    [HttpGet("{id:guid}/history")]
    [ProducesResponseType<IReadOnlyCollection<TradingSignalOutcomeEventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<TradingSignalOutcomeEventResponse>>> GetHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var signal = await operatorReadRepository.GetSignalByIdAsync(id, cancellationToken);
        if (signal is null)
        {
            return NotFound();
        }

        var history = await outcomeHistoryRepository.GetBySignalIdAsync(id, cancellationToken);
        return Ok(history.Select(ToResponse).ToArray());
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<TradingSignalAuditResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<TradingSignalAuditResponse>>> GetRecent(
        [FromQuery] string? symbol = null,
        [FromQuery] SignalOutcome? outcome = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxAuditLimit)
        {
            return Problem(
                title: $"Signal audit limit must be between 1 and {MaxAuditLimit}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        string? normalizedSymbol = null;
        if (!string.IsNullOrWhiteSpace(symbol))
        {
            normalizedSymbol = symbol.Trim().ToUpperInvariant();
            if (normalizedSymbol.Length > MaxSymbolLength)
            {
                return Problem(
                    title: $"Signal symbol must not exceed {MaxSymbolLength} characters.",
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }

        var fromInclusive = from?.ToUniversalTime();
        var toExclusive = to?.ToUniversalTime();
        if (fromInclusive.HasValue &&
            toExclusive.HasValue &&
            fromInclusive.Value >= toExclusive.Value)
        {
            return Problem(
                title: "Signal audit 'from' must be earlier than 'to'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var signals = await operatorReadRepository.GetSignalsAsync(
            normalizedSymbol,
            outcome,
            fromInclusive,
            toExclusive,
            limit,
            cancellationToken);

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

    private static TradingSignalOutcomeEventResponse ToResponse(TradingSignalOutcomeEvent item)
    {
        return new TradingSignalOutcomeEventResponse(
            item.Id,
            item.TradingSignalId,
            item.PreviousOutcome,
            item.Outcome,
            item.OccurredAt,
            item.RiskRejectionReasons,
            item.OrderId,
            item.ClientOrderId);
    }
}
