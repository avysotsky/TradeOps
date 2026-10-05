using TradeOps.Infrastructure.Exchange.Deribit;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class DeribitAdapterTests
{
    [Theory]
    [InlineData("BTC-PERPETUAL")]
    [InlineData("ETH-25DEC26")]
    public void ValidateInstrumentName_AcceptsNativeDeribitNames(string symbol)
    {
        DeribitTestnetExchangeClient.ValidateInstrumentName(symbol);
    }

    [Theory]
    [InlineData("BTCUSDT")]
    [InlineData("")]
    public void ValidateInstrumentName_RejectsNonNativeSymbols(string symbol)
    {
        Assert.Throws<ArgumentException>(
            () => DeribitTestnetExchangeClient.ValidateInstrumentName(symbol));
    }

    [Fact]
    public void ValidateClientOrderId_RejectsMoreThan64Characters()
    {
        Assert.Throws<ArgumentException>(
            () => DeribitTestnetExchangeClient.ValidateClientOrderId(
                new string('x', 65)));
    }
}
