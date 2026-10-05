using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeOps.Application.Interfaces;
using TradeOps.Infrastructure.Exchange;
using TradeOps.Infrastructure.Exchange.Binance;
using TradeOps.Infrastructure.Exchange.Bybit;
using TradeOps.Infrastructure.Exchange.Deribit;
using TradeOps.Infrastructure.Exchange.Hyperliquid;
using TradeOps.Infrastructure.Exchange.Mexc;
using TradeOps.Infrastructure.Exchange.Okx;
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
        var eventStream = provider.GetRequiredService<IExchangeEventStream>();

        Assert.IsType<MockExchangeClient>(exchangeClient);
        Assert.Same(exchangeClient, connectionManager);
        Assert.IsType<NullExchangeEventStream>(eventStream);
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
    public void AddTradeOpsExchange_SelectsHyperliquidTestnetReadOnlyFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "HyperliquidTestnet",
                ["Exchange:Hyperliquid:UserAddress"] = "0x1111111111111111111111111111111111111111",
                ["Exchange:Hyperliquid:HttpTimeoutSeconds"] = "9"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exchangeClient = provider.GetRequiredService<IExchangeClient>();
        var connectionManager = provider.GetRequiredService<IExchangeConnectionManager>();
        var eventStream = provider.GetRequiredService<IExchangeEventStream>();
        var options = provider.GetRequiredService<HyperliquidOptions>();

        Assert.IsType<HyperliquidExchangeClient>(exchangeClient);
        Assert.Same(exchangeClient, connectionManager);
        Assert.IsType<HyperliquidUserDataStream>(eventStream);
        Assert.Equal("https://api.hyperliquid-testnet.xyz", options.BaseUrl);
        Assert.Equal("wss://api.hyperliquid-testnet.xyz/ws", options.WebSocketUrl);
        Assert.Equal("0x1111111111111111111111111111111111111111", options.UserAddress);
        Assert.Equal(9, options.HttpTimeoutSeconds);
    }

    [Fact]
    public void AddTradeOpsExchange_HyperliquidMainnetWebSocketUrlIsRejectedOnResolution()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "HyperliquidTestnet",
                ["Exchange:Hyperliquid:UserAddress"] = "0x1111111111111111111111111111111111111111",
                ["Exchange:Hyperliquid:WebSocketUrl"] = "wss://api.hyperliquid.xyz/ws"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IExchangeEventStream>());

        Assert.Contains(
            "restricted to the official testnet endpoint",
            exception.Message);
    }

    [Fact]
    public void AddTradeOpsExchange_HyperliquidMainnetBaseUrlIsRejectedOnResolution()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "HyperliquidTestnet",
                ["Exchange:Hyperliquid:BaseUrl"] = "https://api.hyperliquid.xyz",
                ["Exchange:Hyperliquid:UserAddress"] = "0x1111111111111111111111111111111111111111"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IExchangeClient>());

        Assert.Contains("restricted to the official testnet API host", exception.Message);
    }

    [Fact]
    public void AddTradeOpsExchange_SelectsMexcFuturesReadOnlyFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "MexcFuturesReadOnly",
                ["Exchange:Mexc:ApiKey"] = "read-key",
                ["Exchange:Mexc:ApiSecret"] = "read-secret",
                ["Exchange:Mexc:HttpTimeoutSeconds"] = "11"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exchangeClient = provider.GetRequiredService<IExchangeClient>();
        var connectionManager = provider.GetRequiredService<IExchangeConnectionManager>();
        var eventStream = provider.GetRequiredService<IExchangeEventStream>();
        var options = provider.GetRequiredService<MexcOptions>();

        Assert.IsType<MexcFuturesReadOnlyExchangeClient>(exchangeClient);
        Assert.Same(exchangeClient, connectionManager);
        Assert.IsType<NullExchangeEventStream>(eventStream);
        Assert.Equal("https://contract.mexc.com", options.BaseUrl);
        Assert.Equal("read-key", options.ApiKey);
        Assert.Equal("read-secret", options.ApiSecret);
        Assert.Equal(11, options.HttpTimeoutSeconds);
    }

    [Fact]
    public void AddTradeOpsExchange_MexcNonOfficialBaseUrlIsRejectedOnResolution()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "MexcFuturesReadOnly",
                ["Exchange:Mexc:BaseUrl"] = "https://contract-testnet.mexc.com"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IExchangeClient>());

        Assert.Contains(
            "restricted to the official contract.mexc.com API host",
            exception.Message);
    }

    [Fact]
    public async Task MexcFuturesReadOnly_RejectsTradingMutationsLocally()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "MexcFuturesReadOnly",
                ["Exchange:Mexc:ApiKey"] = "read-key",
                ["Exchange:Mexc:ApiSecret"] = "read-secret"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();
        var exchangeClient = provider.GetRequiredService<IExchangeClient>();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => exchangeClient.PlaceOrderAsync(
                new TradeOps.Application.Models.PlaceOrderRequest(
                    "client-1",
                    "BTCUSDT",
                    TradeOps.Domain.Enums.OrderSide.Buy,
                    TradeOps.Domain.Enums.OrderType.Market,
                    0.001m)));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => exchangeClient.CancelOrderAsync("123456", "BTCUSDT"));
    }

    [Fact]
    public void AddTradeOpsExchange_SelectsDeribitTestnetFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "DeribitTestnet",
                ["Exchange:Deribit:ClientId"] = "test-client",
                ["Exchange:Deribit:ClientSecret"] = "test-secret",
                ["Exchange:Deribit:AccountCurrency"] = "ETH",
                ["Exchange:Deribit:HttpTimeoutSeconds"] = "12"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exchangeClient = provider.GetRequiredService<IExchangeClient>();
        var connectionManager = provider.GetRequiredService<IExchangeConnectionManager>();
        var eventStream = provider.GetRequiredService<IExchangeEventStream>();
        var options = provider.GetRequiredService<DeribitOptions>();

        Assert.IsType<DeribitTestnetExchangeClient>(exchangeClient);
        Assert.Same(exchangeClient, connectionManager);
        Assert.IsType<NullExchangeEventStream>(eventStream);
        Assert.Equal("https://test.deribit.com/api/v2", options.BaseUrl);
        Assert.Equal("test-client", options.ClientId);
        Assert.Equal("test-secret", options.ClientSecret);
        Assert.Equal("ETH", options.AccountCurrency);
        Assert.Equal(12, options.HttpTimeoutSeconds);
    }

    [Fact]
    public void AddTradeOpsExchange_DeribitProductionBaseUrlIsRejectedOnResolution()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "DeribitTestnet",
                ["Exchange:Deribit:BaseUrl"] = "https://www.deribit.com/api/v2"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IExchangeClient>());

        Assert.Contains(
            "restricted to the official testnet API endpoint",
            exception.Message);
    }

    [Fact]
    public void AddTradeOpsExchange_SelectsOkxDemoFromConfiguration()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "OkxDemo",
                ["Exchange:Okx:ApiKey"] = "demo-key",
                ["Exchange:Okx:ApiSecret"] = "demo-secret",
                ["Exchange:Okx:Passphrase"] = "demo-passphrase",
                ["Exchange:Okx:AccountCurrency"] = "USDT",
                ["Exchange:Okx:TradeMode"] = "cross",
                ["Exchange:Okx:HttpTimeoutSeconds"] = "13"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exchangeClient = provider.GetRequiredService<IExchangeClient>();
        var connectionManager = provider.GetRequiredService<IExchangeConnectionManager>();
        var eventStream = provider.GetRequiredService<IExchangeEventStream>();
        var options = provider.GetRequiredService<OkxOptions>();

        Assert.IsType<OkxDemoExchangeClient>(exchangeClient);
        Assert.Same(exchangeClient, connectionManager);
        Assert.IsType<NullExchangeEventStream>(eventStream);
        Assert.Equal("https://openapi.okx.com", options.BaseUrl);
        Assert.Equal("demo-key", options.ApiKey);
        Assert.Equal("cross", options.TradeMode);
        Assert.Equal(13, options.HttpTimeoutSeconds);
    }

    [Fact]
    public void AddTradeOpsExchange_OkxNonOfficialBaseUrlIsRejectedOnResolution()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            new Dictionary<string, string?>
            {
                ["Exchange:Provider"] = "OkxDemo",
                ["Exchange:Okx:BaseUrl"] = "https://example.invalid"
            });

        services.AddLogging();
        services.AddTradeOpsExchange(configuration);

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IExchangeClient>());

        Assert.Contains(
            "restricted to the official openapi.okx.com REST host",
            exception.Message);
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
