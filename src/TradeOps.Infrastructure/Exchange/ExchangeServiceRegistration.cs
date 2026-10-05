using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeOps.Application.Interfaces;
using TradeOps.Infrastructure.Exchange.Binance;
using TradeOps.Infrastructure.Exchange.Bybit;
using TradeOps.Infrastructure.Exchange.Hyperliquid;

namespace TradeOps.Infrastructure.Exchange;

public static class ExchangeServiceRegistration
{
    public static IServiceCollection AddTradeOpsExchange(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration[$"{ExchangeOptions.SectionName}:Provider"]?.Trim();
        provider = string.IsNullOrWhiteSpace(provider) ? ExchangeProviders.Mock : provider;

        var exchangeOptions = new ExchangeOptions { Provider = provider };
        var bybitOptions = new BybitOptions
        {
            BaseUrl = configuration[$"{BybitOptions.SectionName}:BaseUrl"] ?? "https://api-testnet.bybit.com",
            PrivateWebSocketUrl = configuration[$"{BybitOptions.SectionName}:PrivateWebSocketUrl"] ?? "wss://stream-testnet.bybit.com/v5/private",
            ApiKey = configuration[$"{BybitOptions.SectionName}:ApiKey"] ?? string.Empty,
            ApiSecret = configuration[$"{BybitOptions.SectionName}:ApiSecret"] ?? string.Empty,
            Category = configuration[$"{BybitOptions.SectionName}:Category"] ?? "linear",
            SettleCoin = configuration[$"{BybitOptions.SectionName}:SettleCoin"] ?? "USDT",
            AccountType = configuration[$"{BybitOptions.SectionName}:AccountType"] ?? "UNIFIED",
            RecvWindowMilliseconds = ReadPositiveInt(configuration[$"{BybitOptions.SectionName}:RecvWindowMilliseconds"], 5_000),
            HttpTimeoutSeconds = ReadPositiveInt(configuration[$"{BybitOptions.SectionName}:HttpTimeoutSeconds"], 10),
            WebSocketPingIntervalSeconds = ReadPositiveInt(configuration[$"{BybitOptions.SectionName}:WebSocketPingIntervalSeconds"], 20)
        };

        var binanceOptions = new BinanceOptions
        {
            BaseUrl = configuration[$"{BinanceOptions.SectionName}:BaseUrl"] ?? "https://testnet.binancefuture.com",
            PrivateWebSocketBaseUrl = configuration[$"{BinanceOptions.SectionName}:PrivateWebSocketBaseUrl"] ?? "wss://stream.binancefuture.com",
            ApiKey = configuration[$"{BinanceOptions.SectionName}:ApiKey"] ?? string.Empty,
            ApiSecret = configuration[$"{BinanceOptions.SectionName}:ApiSecret"] ?? string.Empty,
            SettlementCurrency = configuration[$"{BinanceOptions.SectionName}:SettlementCurrency"] ?? "USDT",
            RecvWindowMilliseconds = ReadPositiveInt(configuration[$"{BinanceOptions.SectionName}:RecvWindowMilliseconds"], 5_000),
            HttpTimeoutSeconds = ReadPositiveInt(configuration[$"{BinanceOptions.SectionName}:HttpTimeoutSeconds"], 10),
            ListenKeyKeepaliveMinutes = ReadPositiveInt(configuration[$"{BinanceOptions.SectionName}:ListenKeyKeepaliveMinutes"], 30)
        };

        var hyperliquidOptions = new HyperliquidOptions
        {
            BaseUrl = configuration[$"{HyperliquidOptions.SectionName}:BaseUrl"] ?? "https://api.hyperliquid-testnet.xyz",
            WebSocketUrl = configuration[$"{HyperliquidOptions.SectionName}:WebSocketUrl"] ?? "wss://api.hyperliquid-testnet.xyz/ws",
            UserAddress = configuration[$"{HyperliquidOptions.SectionName}:UserAddress"] ?? string.Empty,
            PrivateKey = configuration[$"{HyperliquidOptions.SectionName}:PrivateKey"] ?? string.Empty,
            MarketSlippagePercent = ReadPositiveDecimal(configuration[$"{HyperliquidOptions.SectionName}:MarketSlippagePercent"], 5m),
            HttpTimeoutSeconds = ReadPositiveInt(configuration[$"{HyperliquidOptions.SectionName}:HttpTimeoutSeconds"], 10)
        };

        services.AddSingleton(exchangeOptions);
        services.AddSingleton(bybitOptions);
        services.AddSingleton(binanceOptions);
        services.AddSingleton(hyperliquidOptions);
        services.AddHttpClient(BybitExchangeClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(bybitOptions.HttpTimeoutSeconds);
        });
        services.AddHttpClient(BinanceFuturesExchangeClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(binanceOptions.HttpTimeoutSeconds);
        });
        services.AddHttpClient(HyperliquidExchangeClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(hyperliquidOptions.HttpTimeoutSeconds);
        });

        services.AddSingleton<MockExchangeClient>();
        services.AddSingleton<BybitExchangeClient>();
        services.AddSingleton<BinanceFuturesExchangeClient>();
        services.AddSingleton<BinanceUserDataStream>();
        services.AddSingleton<HyperliquidExchangeClient>();
        services.AddSingleton<HyperliquidUserDataStream>();
        services.AddSingleton<NullExchangeEventStream>();
        services.AddSingleton<BybitPrivateWebSocketStream>();

        if (string.Equals(provider, ExchangeProviders.Mock, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IExchangeClient>(sp => sp.GetRequiredService<MockExchangeClient>());
            services.AddSingleton<IExchangeConnectionManager>(sp => sp.GetRequiredService<MockExchangeClient>());
            services.AddSingleton<IExchangeEventStream>(sp => sp.GetRequiredService<NullExchangeEventStream>());
            return services;
        }

        if (string.Equals(provider, ExchangeProviders.BybitTestnet, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IExchangeClient>(sp => sp.GetRequiredService<BybitExchangeClient>());
            services.AddSingleton<IExchangeConnectionManager>(sp => sp.GetRequiredService<BybitExchangeClient>());
            services.AddSingleton<IExchangeEventStream>(sp => sp.GetRequiredService<BybitPrivateWebSocketStream>());
            return services;
        }

        if (string.Equals(provider, ExchangeProviders.BinanceFuturesTestnet, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IExchangeClient>(sp => sp.GetRequiredService<BinanceFuturesExchangeClient>());
            services.AddSingleton<IExchangeConnectionManager>(sp => sp.GetRequiredService<BinanceFuturesExchangeClient>());
            services.AddSingleton<IExchangeEventStream>(sp => sp.GetRequiredService<BinanceUserDataStream>());
            return services;
        }

        if (string.Equals(provider, ExchangeProviders.HyperliquidTestnet, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IExchangeClient>(sp => sp.GetRequiredService<HyperliquidExchangeClient>());
            services.AddSingleton<IExchangeConnectionManager>(sp => sp.GetRequiredService<HyperliquidExchangeClient>());
            services.AddSingleton<IExchangeEventStream>(sp => sp.GetRequiredService<HyperliquidUserDataStream>());
            return services;
        }

        throw new InvalidOperationException(
            $"Unsupported Exchange:Provider '{provider}'. Supported values: {ExchangeProviders.Mock}, {ExchangeProviders.BybitTestnet}, {ExchangeProviders.BinanceFuturesTestnet}, {ExchangeProviders.HyperliquidTestnet}.");
    }

    private static int ReadPositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static decimal ReadPositiveDecimal(string? value, decimal fallback) =>
        decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
        && parsed > 0m
            ? parsed
            : fallback;
}
