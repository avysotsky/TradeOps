using TradeOps.Infrastructure.Exchange.Gate;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class GateAdapterTests
{
    [Fact]
    public void Sign_MatchesOfficialGateVector()
    {
        var signature = GateSigner.Sign(
            "GET",
            "/api/v4/futures/orders",
            "contract=BTC_USD&status=finished&limit=50",
            string.Empty,
            1541993715,
            "secret");

        Assert.Equal(
            "55f84ea195d6fe57ce62464daaa7c3c02fa9d1dde954e4c898289c9a2407a3d6fb3faf24deff16790d726b66ac9f74526668b13bd01029199cc4fcc522418b8a",
            signature);
    }

    [Fact]
    public void ClientOrderIdFactory_IsStableAndGateCompatible()
    {
        var first = GateClientOrderIdFactory.Create("tradeops-order-123");
        var second = GateClientOrderIdFactory.Create("tradeops-order-123");

        Assert.Equal(first, second);
        Assert.StartsWith("t-", first);
        Assert.Equal(30, first.Length);
        Assert.All(
            first[2..],
            character => Assert.True(char.IsLetterOrDigit(character)));
    }

    [Theory]
    [InlineData("BTC_USDT")]
    [InlineData("ETH_USDT")]
    public void ValidateContract_AcceptsNativeGateContracts(string symbol)
    {
        GateFuturesTestnetExchangeClient.ValidateContract(symbol);
    }

    [Theory]
    [InlineData("BTCUSDT")]
    [InlineData("BTC-USDT")]
    [InlineData("")]
    public void ValidateContract_RejectsNonNativeContracts(string symbol)
    {
        Assert.Throws<ArgumentException>(
            () => GateFuturesTestnetExchangeClient.ValidateContract(symbol));
    }
}
