using System.Security.Cryptography;
using System.Text;

namespace TradeOps.Infrastructure.Exchange.Gate;

internal static class GateSigner
{
    public static string Sign(
        string method,
        string requestPath,
        string queryString,
        string body,
        long timestampSeconds,
        string secret)
    {
        var bodyHash = Convert.ToHexString(
                SHA512.HashData(
                    Encoding.UTF8.GetBytes(body)))
            .ToLowerInvariant();

        var signatureString =
            method.ToUpperInvariant()
            + "\n"
            + requestPath
            + "\n"
            + queryString
            + "\n"
            + bodyHash
            + "\n"
            + timestampSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

        using var hmac = new HMACSHA512(
            Encoding.UTF8.GetBytes(secret));

        return Convert.ToHexString(
                hmac.ComputeHash(
                    Encoding.UTF8.GetBytes(signatureString)))
            .ToLowerInvariant();
    }
}
