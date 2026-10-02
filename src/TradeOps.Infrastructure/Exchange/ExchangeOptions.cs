namespace TradeOps.Infrastructure.Exchange;

public static class ExchangeProviders
{
    public const string Mock = "Mock";
    public const string BybitTestnet = "BybitTestnet";
}

public sealed class ExchangeOptions
{
    public const string SectionName = "Exchange";

    public string Provider { get; init; } = ExchangeProviders.Mock;
}
