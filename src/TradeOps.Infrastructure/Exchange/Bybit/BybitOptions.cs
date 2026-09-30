namespace TradeOps.Infrastructure.Exchange.Bybit;

public sealed class BybitOptions
{
    public const string SectionName = "Exchange:Bybit";

    public string BaseUrl { get; init; } = "https://api-testnet.bybit.com";

    public string ApiKey { get; init; } = string.Empty;

    public string ApiSecret { get; init; } = string.Empty;

    public string Category { get; init; } = "linear";

    public string SettleCoin { get; init; } = "USDT";

    public string AccountType { get; init; } = "UNIFIED";

    public int RecvWindowMilliseconds { get; init; } = 5_000;

    public int HttpTimeoutSeconds { get; init; } = 10;
}
