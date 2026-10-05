namespace TradeOps.Infrastructure.Exchange.Hyperliquid;

public sealed class HyperliquidOptions
{
    public const string SectionName = "Exchange:Hyperliquid";

    public string BaseUrl { get; init; } = "https://api.hyperliquid-testnet.xyz";

    public string UserAddress { get; init; } = string.Empty;

    public int HttpTimeoutSeconds { get; init; } = 10;
}
