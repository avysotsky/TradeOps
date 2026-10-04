using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradeOps.Api.Contracts;
using TradeOps.Api.Integrations.TradingView;
using TradeOps.Api.Security;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Api.Controllers;

[ApiController]
[Route("api/integrations/tradingview")]
public sealed class TradingViewWebhookController(
    ISignalExecutionService signalExecutionService,
    IOptions<TradingViewWebhookOptions> options) : ControllerBase
{
    private const int MaxEventIdLength = 200;
    private readonly TradingViewWebhookOptions _options = options.Value;

    [HttpPost]
    [ProducesResponseType<TradingViewWebhookResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<TradingViewWebhookResponse>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<SignalIdConflictResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TradingViewWebhookResponse>> Post(
        TradingViewWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return NotFound();
        }

        if (!TryAuthenticateGateway())
        {
            Response.Headers["WWW-Authenticate"] = "GatewayApiKey";
            return Problem(
                title: "TradingView gateway authentication failed.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (_options.RequireGatewayIpAllowlist
            && !IsAllowedGatewayIp(HttpContext.Connection.RemoteIpAddress))
        {
            return Problem(
                title: "TradingView gateway source IP is not allowed.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var validationErrors = ValidateAdapterRequest(request);
        if (validationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(validationErrors)
            {
                Title = "TradingView webhook validation failed.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var side = ParseAction(request.Action);
        var signalId = TradingViewSignalIdFactory.Create(
            request.EventId);

        var canonicalRequest = new CreateTradingSignalRequest(
            request.Symbol,
            side,
            request.Quantity,
            request.RiskPercent,
            request.StopLoss,
            request.TakeProfit,
            Source: "TradingView",
            SignalId: signalId);

        var canonicalValidationErrors =
            TradingSignalRequestValidator.Validate(
                canonicalRequest);

        if (canonicalValidationErrors.Count > 0)
        {
            return BadRequest(new ValidationProblemDetails(
                canonicalValidationErrors)
            {
                Title = "TradingView webhook validation failed.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var signal = new TradingSignal
        {
            Id = signalId,
            Symbol = canonicalRequest.Symbol.Trim().ToUpperInvariant(),
            Side = canonicalRequest.Side,
            SignalType = "TradingView",
            RequestedQuantity = canonicalRequest.Quantity,
            RiskPercent = canonicalRequest.RiskPercent,
            StopLoss = canonicalRequest.StopLoss,
            TakeProfit = canonicalRequest.TakeProfit,
            CreatedAt = DateTimeOffset.UtcNow,
            Source = "TradingView"
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

        var response = new TradingViewWebhookResponse(
            request.EventId.Trim(),
            result.SignalId,
            result.Accepted,
            result.RiskReasons,
            result.Order);

        return result.Accepted
            ? Ok(response)
            : UnprocessableEntity(response);
    }

    private bool TryAuthenticateGateway()
    {
        if (!Request.Headers.TryGetValue(
                _options.GatewayHeaderName,
                out var values)
            || values.Count != 1
            || string.IsNullOrWhiteSpace(values[0]))
        {
            return false;
        }

        return ApiKeyVerifier.Matches(
            _options.GatewayKey,
            values[0]!);
    }

    private bool IsAllowedGatewayIp(IPAddress? remoteIp)
    {
        if (remoteIp is null)
        {
            return false;
        }

        foreach (var configured in _options.AllowedGatewayIps)
        {
            if (!IPAddress.TryParse(configured, out var allowed))
            {
                continue;
            }

            if (remoteIp.Equals(allowed)
                || remoteIp.MapToIPv6().Equals(allowed.MapToIPv6()))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string[]> ValidateAdapterRequest(
        TradingViewWebhookRequest request)
    {
        var errors = new Dictionary<string, string[]>(
            StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.EventId))
        {
            errors[nameof(request.EventId)] =
                ["EventId is required."];
        }
        else if (request.EventId.Trim().Length > MaxEventIdLength)
        {
            errors[nameof(request.EventId)] =
                [$"EventId must not exceed {MaxEventIdLength} characters."];
        }

        if (!TryParseAction(request.Action, out _))
        {
            errors[nameof(request.Action)] =
                ["Action must be 'buy' or 'sell'."];
        }

        return errors;
    }

    private static OrderSide ParseAction(string action)
    {
        _ = TryParseAction(action, out var side);
        return side;
    }

    private static bool TryParseAction(
        string? action,
        out OrderSide side)
    {
        if (string.Equals(
                action?.Trim(),
                "buy",
                StringComparison.OrdinalIgnoreCase))
        {
            side = OrderSide.Buy;
            return true;
        }

        if (string.Equals(
                action?.Trim(),
                "sell",
                StringComparison.OrdinalIgnoreCase))
        {
            side = OrderSide.Sell;
            return true;
        }

        side = default;
        return false;
    }
}
