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
        provider = string.IsNullOrWhiteSpace(provider)
            ? ExchangeProviders.Mock
            : provider;

        var exchangeOptions = new ExchangeOptions
        {
            Provider = provider
        };

        var bybitOptions = new BybitOptions
        {
            BaseUrl = configuration[$"{BybitOptions.SectionName}:BaseUrl"]
                ?? "https://api-testnet.bybit.com",
            ApiKey = configuration[$"{BybitOptions.SectionName}:ApiKey"] ?? string.Empty,
            ApiSecret = configuration[$"{BybitOptions.SectionName}:ApiSecret"] ?? string.Empty,
            Category = configuration[$"{BybitOptions.SectionName}:Category"] ?? "linear",
            SettleCoin = configuration[$"{BybitOptions.SectionName}:SettleCoin"] ?? "USDT",
            AccountType = configuration[$"{BybitOptions.SectionName}:AccountType"] ?? "UNIFIED",
            RecvWindowMilliseconds = ReadPositiveInt(
                configuration[$"{BybitOptions.SectionName}:RecvWindowMilliseconds"],
                5_000),
            HttpTimeoutSeconds = ReadPositiveInt(
                configuration[$"{BybitOptions.SectionName}:HttpTimeoutSeconds"],
                10)
        };

        services.AddSingleton(exchangeOptions);
        services.AddSingleton(bybitOptions);
        services.AddHttpClient(BybitExchangeClient.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(bybitOptions.HttpTimeoutSeconds);
        });

        services.AddSingleton<MockExchangeClient>();
        services.AddSingleton<BybitExchangeClient>();

        if (string.Equals(provider, ExchangeProviders.Mock, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IExchangeClient>(serviceProvider =>
                serviceProvider.GetRequiredService<MockExchangeClient>());
            services.AddSingleton<IExchangeConnectionManager>(serviceProvider =>
                serviceProvider.GetRequiredService<MockExchangeClient>());
            return services;
        }

        if (string.Equals(provider, ExchangeProviders.BybitTestnet, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IExchangeClient>(serviceProvider =>
                serviceProvider.GetRequiredService<BybitExchangeClient>());
            services.AddSingleton<IExchangeConnectionManager>(serviceProvider =>
                serviceProvider.GetRequiredService<BybitExchangeClient>());
            return services;
        }

        throw new InvalidOperationException(
            $"Unsupported Exchange:Provider '{provider}'. Supported values: " +
            $"{ExchangeProviders.Mock}, {ExchangeProviders.BybitTestnet}.");
    }

    private static int ReadPositiveInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }
}
