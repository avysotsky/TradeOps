using System.Globalization;
using Microsoft.Extensions.Options;
using TradeOps.Application.Interfaces;

namespace TradeOps.Api.Security;

public sealed class SignalIngressAuthenticationMiddleware(
    RequestDelegate next,
    IOptions<SignalIngressAuthOptions> authOptions,
    IOptions<SignalIngressReplayProtectionOptions> replayOptions)
{
    private readonly SignalIngressAuthOptions _authOptions = authOptions.Value;
    private readonly SignalIngressReplayProtectionOptions _replayOptions = replayOptions.Value;

    public async Task InvokeAsync(
        HttpContext context,
        ISignalIngressReplayStore replayStore)
    {
        if (!IsSignalSubmission(context.Request))
        {
            await next(context);
            return;
        }

        if (_authOptions.Enabled
            && (!TryGetSingleHeader(context.Request, _authOptions.HeaderName, out var suppliedKey)
                || !ApiKeyVerifier.Matches(_authOptions.ApiKey, suppliedKey)))
        {
            context.Response.Headers["WWW-Authenticate"] = "ApiKey";
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Signal ingress authentication failed.");
            return;
        }

        if (_replayOptions.Enabled)
        {
            var replayValidation = await ValidateReplayProtectionAsync(
                context,
                replayStore);

            if (!replayValidation)
            {
                return;
            }
        }

        await next(context);
    }

    private async Task<bool> ValidateReplayProtectionAsync(
        HttpContext context,
        ISignalIngressReplayStore replayStore)
    {
        if (!TryGetSingleHeader(
                context.Request,
                _replayOptions.TimestampHeaderName,
                out var timestampValue)
            || !TryGetSingleHeader(
                context.Request,
                _replayOptions.RequestIdHeaderName,
                out var requestIdValue)
            || !long.TryParse(
                timestampValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var unixTimestamp)
            || !Guid.TryParse(requestIdValue, out var requestId))
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Signal ingress replay headers are missing or invalid.");
            return false;
        }

        DateTimeOffset requestTimestamp;
        try
        {
            requestTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
        }
        catch (ArgumentOutOfRangeException)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Signal ingress replay headers are missing or invalid.");
            return false;
        }

        var receivedAt = DateTimeOffset.UtcNow;
        var allowedClockSkew = TimeSpan.FromSeconds(
            _replayOptions.AllowedClockSkewSeconds);

        if ((requestTimestamp - receivedAt).Duration() > allowedClockSkew)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Signal ingress request timestamp is outside the allowed window.");
            return false;
        }

        var expiresAt = receivedAt.AddSeconds(
            _replayOptions.ReceiptRetentionSeconds);

        var registered = await replayStore.TryRegisterAsync(
            requestId,
            requestTimestamp,
            receivedAt,
            expiresAt,
            context.RequestAborted);

        if (!registered)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "Signal ingress request ID has already been used.");
            return false;
        }

        return true;
    }

    private static bool TryGetSingleHeader(
        HttpRequest request,
        string headerName,
        out string value)
    {
        value = string.Empty;

        if (!request.Headers.TryGetValue(headerName, out var values)
            || values.Count != 1
            || string.IsNullOrWhiteSpace(values[0]))
        {
            return false;
        }

        value = values[0]!;
        return true;
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string title)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(
            new
            {
                type = $"https://httpstatuses.com/{statusCode}",
                title,
                status = statusCode
            },
            cancellationToken: context.RequestAborted);
    }

    private static bool IsSignalSubmission(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && request.Path.Equals(new PathString("/api/signals"));
}
