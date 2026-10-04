using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TradeOps.Api.Security;

public sealed class SignalIngressAuthenticationMiddleware(
    RequestDelegate next,
    IOptions<SignalIngressAuthOptions> options)
{
    private readonly SignalIngressAuthOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || !IsSignalSubmission(context.Request))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(_options.HeaderName, out var suppliedKeys)
            || suppliedKeys.Count != 1
            || string.IsNullOrEmpty(suppliedKeys[0])
            || !ApiKeysMatch(_options.ApiKey, suppliedKeys[0]!))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers["WWW-Authenticate"] = "ApiKey";

            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = "https://httpstatuses.com/401",
                    title = "Signal ingress authentication failed.",
                    status = StatusCodes.Status401Unauthorized
                },
                cancellationToken: context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool IsSignalSubmission(HttpRequest request) =>
        HttpMethods.IsPost(request.Method)
        && request.Path.Equals(new PathString("/api/signals"));

    private static bool ApiKeysMatch(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));

        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }
}
