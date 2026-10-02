using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeOps.Application.Interfaces;
using TradeOps.Infrastructure.Exchange;
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
