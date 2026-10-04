using System.Globalization;
using Microsoft.Extensions.Options;
using TradeOps.Application.Interfaces;

namespace TradeOps.Api.Security;

public sealed class SignalIngressAuthenticationMiddleware(
    RequestDelegate next,
    IOptions<SignalIngressAuthOptions> authOptions,
    IOptions<SignalIngressReplayProtectionOptions> replayOptions,
    IOptions<SignalIngressSigningOptions> signingOptions)
{
    private readonly SignalIngressAuthOptions _authOptions = authOptions.Value;
    private readonly SignalIngressReplayProtectionOptions _replayOptions = replayOptions.Value;
    private readonly SignalIngressSigningOptions _signingOptions = signingOptions.Value;

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

        ReplayMetadata? replayMetadata = null;
        if (_replayOptions.Enabled)
        {
            replayMetadata = await ValidateReplayMetadataAsync(context);
            if (replayMetadata is null)
            {
                return;
            }
        }

        if (_signingOptions.Enabled)
        {
            if (replayMetadata is null
                || !await ValidateSignatureAsync(
                    context,
                    replayMetadata))
            {
                return;
            }
        }

        if (replayMetadata is not null)
        {
            var expiresAt = replayMetadata.ReceivedAt.AddSeconds(
                _replayOptions.ReceiptRetentionSeconds);

            var registered = await replayStore.TryRegisterAsync(
                replayMetadata.RequestId,
                replayMetadata.RequestTimestamp,
                replayMetadata.ReceivedAt,
                expiresAt,
                context.RequestAborted);

            if (!registered)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status409Conflict,
                    "Signal ingress request ID has already been used.");
                return;
            }
        }

        await next(context);
    }

    private async Task<ReplayMetadata?> ValidateReplayMetadataAsync(
        HttpContext context)
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
            return null;
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
            return null;
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
            return null;
        }

        return new ReplayMetadata(
            unixTimestamp,
            requestId,
            requestTimestamp,
            receivedAt);
    }

    private async Task<bool> ValidateSignatureAsync(
        HttpContext context,
        ReplayMetadata replayMetadata)
    {
        if (!TryGetSingleHeader(
                context.Request,
                _signingOptions.SignatureHeaderName,
                out var suppliedSignature))
        {
            context.Response.Headers["WWW-Authenticate"] = "HMAC-SHA256";
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Signal ingress signature validation failed.");
            return false;
        }

        if (context.Request.ContentLength is > 0
            && context.Request.ContentLength > _signingOptions.MaxBodyBytes)
        {
            await WriteProblemAsync(
                context,
                StatusCodes.Status413PayloadTooLarge,
                "Signal ingress request body exceeds the configured signing limit.");
            return false;
        }

        byte[] body;
        try
        {
            context.Request.EnableBuffering(
                bufferThreshold: Math.Min(
                    _signingOptions.MaxBodyBytes,
                    30 * 1024),
                bufferLimit: _signingOptions.MaxBodyBytes);

            using var bodyBuffer = new MemoryStream();
            await context.Request.Body.CopyToAsync(
                bodyBuffer,
                context.RequestAborted);

            body = bodyBuffer.ToArray();
            context.Request.Body.Position = 0;
        }
        catch (IOException)
        {
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }

            await WriteProblemAsync(
                context,
                StatusCodes.Status413PayloadTooLarge,
                "Signal ingress request body exceeds the configured signing limit.");
            return false;
        }

        var path = string.Concat(
            context.Request.PathBase.Value,
            context.Request.Path.Value);

        if (!SignalIngressHmacVerifier.Verify(
                _signingOptions.Secret,
                replayMetadata.UnixTimestamp,
                replayMetadata.RequestId,
                context.Request.Method,
                path,
                body,
                suppliedSignature))
        {
            context.Response.Headers["WWW-Authenticate"] = "HMAC-SHA256";
            await WriteProblemAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Signal ingress signature validation failed.");
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

    private sealed record ReplayMetadata(
        long UnixTimestamp,
        Guid RequestId,
        DateTimeOffset RequestTimestamp,
        DateTimeOffset ReceivedAt);
}
