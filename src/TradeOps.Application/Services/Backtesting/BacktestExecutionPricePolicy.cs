using TradeOps.Application.Models;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services.Backtesting;

public interface IBacktestExecutionPricePolicy
{
    MarketDataBar? SelectExecutionBar(
        IReadOnlyList<MarketDataBar> orderedBars,
        InstrumentReference instrument,
        DateTimeOffset notBefore);

    decimal ApplySlippage(
        decimal referencePrice,
        OrderSide side,
        decimal slippageBasisPoints);
}

public sealed class NextBarOpenExecutionPricePolicy
    : IBacktestExecutionPricePolicy
{
    public MarketDataBar? SelectExecutionBar(
        IReadOnlyList<MarketDataBar> orderedBars,
        InstrumentReference instrument,
        DateTimeOffset notBefore)
    {
        ArgumentNullException.ThrowIfNull(orderedBars);
        ArgumentNullException.ThrowIfNull(instrument);

        return orderedBars
            .Where(
                bar =>
                    IsSameInstrument(
                        bar.Instrument,
                        instrument) &&
                    bar.OpenTime >= notBefore)
            .OrderBy(bar => bar.OpenTime)
            .FirstOrDefault();
    }

    public decimal ApplySlippage(
        decimal referencePrice,
        OrderSide side,
        decimal slippageBasisPoints)
    {
        if (referencePrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(referencePrice),
                "Reference price must be greater than zero.");
        }

        if (slippageBasisPoints < 0m ||
            slippageBasisPoints >= 10_000m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slippageBasisPoints),
                "Slippage basis points must be non-negative and less than 10,000.");
        }

        var factor =
            slippageBasisPoints /
            10_000m;

        return side switch
        {
            OrderSide.Buy =>
                referencePrice *
                (1m + factor),

            OrderSide.Sell =>
                referencePrice *
                (1m - factor),

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(side),
                    "Unsupported order side.")
        };
    }

    private static bool IsSameInstrument(
        InstrumentReference left,
        InstrumentReference right) =>
        left.AssetClass == right.AssetClass &&
        string.Equals(
            left.Symbol.Trim(),
            right.Symbol.Trim(),
            StringComparison.OrdinalIgnoreCase) &&
        (
            string.IsNullOrWhiteSpace(left.Currency) ||
            string.IsNullOrWhiteSpace(right.Currency) ||
            string.Equals(
                left.Currency.Trim(),
                right.Currency.Trim(),
                StringComparison.OrdinalIgnoreCase)
        );
}
