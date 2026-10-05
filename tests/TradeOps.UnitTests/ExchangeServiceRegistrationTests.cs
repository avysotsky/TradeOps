using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeOps.Application.Interfaces;
using TradeOps.Infrastructure.Exchange;
using TradeOps.Infrastructure.Exchange.Binance;
using TradeOps.Infrastructure.Exchange.Bybit;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class ExchangeServiceRegistrationTests
{
    [Fact]
    public void AddTradeOpsExchange_DefaultsToMockProvider()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration([]);

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exchangeClient = provider.GetRequiredService<IExchangeClient>();
        var connectionManager = provider.GetRequiredService<IExchangeConnectionManager>();

        Assert.IsType<MockExchangeClient>(exchangeClient);
        Assert.Same(exchangeClient, connectionManager);
    }

    [Fact]
    public void AddTradeOpsExchange_SelectsBybitTestnetFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "BybitTestnet",
                ["Exchange:Bybit:ApiKey"] = "test-key",
                ["Exchange:Bybit:ApiSecret"] = "test-secret",
                ["Exchange:Bybit:HttpTimeoutSeconds"] = "7"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exchangeClient = provider.GetRequiredService<IExchangeClient>();
        var connectionManager = provider.GetRequiredService<IExchangeConnectionManager>();
        var options = provider.GetRequiredService<BybitOptions>();

        Assert.IsType<BybitExchangeClient>(exchangeClient);
        Assert.Same(exchangeClient, connectionManager);
        Assert.Equal("https://api-testnet.bybit.com", options.BaseUrl);
        Assert.Equal("test-key", options.ApiKey);
        Assert.Equal("test-secret", options.ApiSecret);
        Assert.Equal(7, options.HttpTimeoutSeconds);
    }

    [Fact]
    public void AddTradeOpsExchange_SelectsBinanceFuturesTestnetFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "BinanceFuturesTestnet",
                ["Exchange:Binance:ApiKey"] = "test-key",
                ["Exchange:Binance:ApiSecret"] = "test-secret",
                ["Exchange:Binance:HttpTimeoutSeconds"] = "8"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exchangeClient = provider.GetRequiredService<IExchangeClient>();
        var connectionManager = provider.GetRequiredService<IExchangeConnectionManager>();
        var eventStream = provider.GetRequiredService<IExchangeEventStream>();
        var options = provider.GetRequiredService<BinanceOptions>();

        Assert.IsType<BinanceFuturesExchangeClient>(exchangeClient);
        Assert.Same(exchangeClient, connectionManager);
        Assert.IsType<BinanceUserDataStream>(eventStream);
        Assert.Equal("https://testnet.binancefuture.com", options.BaseUrl);
        Assert.Equal("wss://stream.binancefuture.com", options.PrivateWebSocketBaseUrl);
        Assert.Equal("test-key", options.ApiKey);
        Assert.Equal("test-secret", options.ApiSecret);
        Assert.Equal(8, options.HttpTimeoutSeconds);
    }

    [Fact]
    public void AddTradeOpsExchange_BinanceProductionWebSocketUrlIsRejectedOnResolution()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "BinanceFuturesTestnet",
                ["Exchange:Binance:ApiKey"] = "test-key",
                ["Exchange:Binance:ApiSecret"] = "test-secret",
                ["Exchange:Binance:PrivateWebSocketBaseUrl"] = "wss://fstream.binance.com"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IExchangeEventStream>());

        Assert.Contains("restricted to approved non-production Futures websocket hosts", exception.Message);
    }

    [Fact]
    public void AddTradeOpsExchange_BinanceProductionBaseUrlIsRejectedOnResolution()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "BinanceFuturesTestnet",
                ["Exchange:Binance:BaseUrl"] = "https://fapi.binance.com",
                ["Exchange:Binance:ApiKey"] = "test-key",
                ["Exchange:Binance:ApiSecret"] = "test-secret"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IExchangeClient>());

        Assert.Contains("restricted to approved non-production", exception.Message);
    }

    [Fact]
    public void AddTradeOpsExchange_RejectsUnsupportedProvider()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "LiveExchange"
            });

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddTradeOpsExchange(configuration));

        Assert.Contains("Unsupported Exchange:Provider", exception.Message);
    }

    private static IConfiguration BuildConfiguration(
        IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
