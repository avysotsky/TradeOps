using System.Security.Cryptography;
using System.Text;

namespace TradeOps.Infrastructure.Exchange.Gate;

internal static class GateClientOrderIdFactory
{
    private const string Prefix = "TradeOps:Gate:";

    public static string Create(string clientOrderId)
    {
        if (string.IsNullOrWhiteSpace(clientOrderId))
        {
            throw new ArgumentException(
                "ClientOrderId is required.",
                nameof(clientOrderId));
        }

        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                Prefix + clientOrderId.Trim()));

        return "t-" + Convert.ToHexString(
                hash.AsSpan(0, 14))
            .ToLowerInvariant();
    }
}
