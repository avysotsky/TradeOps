using System.Security.Cryptography;
using System.Text;

namespace TradeOps.Infrastructure.Exchange.Bitget;

internal static class BitgetSigner
{
    public static string Sign(
        long timestamp,
        string method,
        string requestPathWithQuery,
        string body,
        string secret)
    {
        var prehash =
            timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + method.ToUpperInvariant()
            + requestPathWithQuery
            + body;

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(secret));

        return Convert.ToBase64String(
            hmac.ComputeHash(
                Encoding.UTF8.GetBytes(prehash)));
    }
}
