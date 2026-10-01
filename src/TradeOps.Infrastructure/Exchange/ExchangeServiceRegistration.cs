using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeOps.Application.Interfaces;
using TradeOps.Infrastructure.Exchange.Bybit;

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

        services.AddSingleton(exchangeOptions);
        services.AddSingleton(bybitOptions);
        services.AddHttpClient(BybitExchangeClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(bybitOptions.HttpTimeoutSeconds);
        });

        services.AddSingleton<MockExchangeClient>();
        services.AddSingleton<BybitExchangeClient>();
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

        throw new InvalidOperationException(
            $"Unsupported Exchange:Provider '{provider}'. Supported values: {ExchangeProviders.Mock}, {ExchangeProviders.BybitTestnet}.");
    }

    private static int ReadPositiveInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}
