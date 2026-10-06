using TradeOps.Application.Models;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services;

public static class PortfolioRebalancePlanner
{
    public static RebalancePlan Plan(
        ResearchDecision decision,
        PortfolioSnapshot portfolio,
        decimal referencePrice,
        RebalanceConstraints? constraints = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(portfolio);

        constraints ??= new RebalanceConstraints();

        ValidatePortfolio(portfolio);
        ValidateConstraints(constraints);

        if (referencePrice <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(referencePrice),
                "Reference price must be greater than zero.");
        }

        var normalizedDecision =
            GetNormalizedValidatedDecision(decision);

        if (normalizedDecision.Action !=
            ResearchDecisionAction.SetTargetWeight)
        {
            throw new NotSupportedException(
                "WS-03 first slice supports only SetTargetWeight decisions.");
        }

        if (normalizedDecision.Instrument.AssetClass !=
            AssetClass.Stock)
        {
            throw new NotSupportedException(
                "WS-03 first slice supports long-only stocks only.");
        }

        var targetWeight =
            normalizedDecision.TargetWeight!.Value;
        var targetNotional =
            portfolio.NetAssetValue * targetWeight;
        var targetQuantity =
            targetNotional / referencePrice;

        var targetPosition =
            new TargetPosition(
                normalizedDecision.Instrument,
                targetWeight,
                referencePrice,
                targetNotional,
                targetQuantity);

        var currentQuantity =
            portfolio.Positions
                .Where(
                    position =>
                        IsSameInstrument(
                            position.Instrument,
                            normalizedDecision.Instrument))
                .Sum(position => position.Quantity);

        var deltaQuantity =
            targetQuantity - currentQuantity;

        var violations =
            EvaluateTargetConstraints(
                normalizedDecision,
                portfolio,
                constraints,
                targetWeight);

        if (violations.Count > 0)
        {
            return new RebalancePlan(
                normalizedDecision.DecisionId,
                normalizedDecision.StrategyId,
                portfolio.AsOf,
                targetPosition,
                currentQuantity,
                deltaQuantity,
                RebalancePlanStatus.Blocked,
                null,
                violations);
        }

        if (deltaQuantity == 0m)
        {
            return new RebalancePlan(
                normalizedDecision.DecisionId,
                normalizedDecision.StrategyId,
                portfolio.AsOf,
                targetPosition,
                currentQuantity,
                deltaQuantity,
                RebalancePlanStatus.NoAction,
                null,
                Array.Empty<RebalanceConstraintViolation>());
        }

        var tradeNotional =
            Math.Abs(deltaQuantity) *
            referencePrice;

        if (tradeNotional <
            constraints.MinimumTradeNotional)
        {
            return new RebalancePlan(
                normalizedDecision.DecisionId,
                normalizedDecision.StrategyId,
                portfolio.AsOf,
                targetPosition,
                currentQuantity,
                deltaQuantity,
                RebalancePlanStatus.NoAction,
                null,
                new[]
                {
                    new RebalanceConstraintViolation(
                        "MinimumTradeNotional",
                        $"Estimated trade notional {tradeNotional} is below minimum {constraints.MinimumTradeNotional}.")
                });
        }

        if (deltaQuantity > 0m)
        {
            var availableCash =
                portfolio.Cash -
                constraints.MinimumCashReserve;

            if (tradeNotional > availableCash)
            {
                return new RebalancePlan(
                    normalizedDecision.DecisionId,
                    normalizedDecision.StrategyId,
                    portfolio.AsOf,
                    targetPosition,
                    currentQuantity,
                    deltaQuantity,
                    RebalancePlanStatus.Blocked,
                    null,
                    new[]
                    {
                        new RebalanceConstraintViolation(
                            "InsufficientCash",
                            $"Estimated buy notional {tradeNotional} exceeds available cash {availableCash} after reserve.")
                    });
            }
        }

        var side =
            deltaQuantity > 0m
                ? OrderSide.Buy
                : OrderSide.Sell;

        var orderIntent =
            new RebalanceOrderIntent(
                normalizedDecision.DecisionId,
                normalizedDecision.Instrument,
                side,
                Math.Abs(deltaQuantity),
                referencePrice,
                tradeNotional);

        return new RebalancePlan(
            normalizedDecision.DecisionId,
            normalizedDecision.StrategyId,
            portfolio.AsOf,
            targetPosition,
            currentQuantity,
            deltaQuantity,
            RebalancePlanStatus.Ready,
            orderIntent,
            Array.Empty<RebalanceConstraintViolation>());
    }

    private static ResearchDecision
        GetNormalizedValidatedDecision(
            ResearchDecision decision)
    {
        var validation =
            ResearchDecisionValidator.Validate(
                decision);

        if (validation.IsValid &&
            validation.NormalizedDecision is not null)
        {
            return validation.NormalizedDecision;
        }

        var details =
            string.Join(
                "; ",
                validation.Errors.SelectMany(
                    item =>
                        item.Value.Select(
                            message =>
                                $"{item.Key}: {message}")));

        throw new ArgumentException(
            $"ResearchDecision is invalid. {details}",
            nameof(decision));
    }

    private static void ValidatePortfolio(
        PortfolioSnapshot portfolio)
    {
        if (string.IsNullOrWhiteSpace(
                portfolio.BaseCurrency))
        {
            throw new ArgumentException(
                "Portfolio base currency is required.",
                nameof(portfolio));
        }

        if (portfolio.NetAssetValue <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(portfolio),
                "Portfolio NAV must be greater than zero.");
        }

        if (portfolio.Cash < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(portfolio),
                "Portfolio cash cannot be negative in the long-only first slice.");
        }

        if (portfolio.AsOf == default)
        {
            throw new ArgumentException(
                "Portfolio AsOf timestamp is required.",
                nameof(portfolio));
        }

        if (portfolio.Positions is null)
        {
            throw new ArgumentException(
                "Portfolio positions are required.",
                nameof(portfolio));
        }

        foreach (var position in portfolio.Positions)
        {
            if (position is null ||
                position.Instrument is null)
            {
                throw new ArgumentException(
                    "Portfolio positions must contain an instrument.",
                    nameof(portfolio));
            }

            if (position.Quantity < 0m)
            {
                throw new ArgumentException(
                    "Short positions are not supported by the WS-03 first slice.",
                    nameof(portfolio));
            }
        }
    }

    private static void ValidateConstraints(
        RebalanceConstraints constraints)
    {
        if (constraints.MaxTargetWeight <= 0m ||
            constraints.MaxTargetWeight > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(constraints),
                "MaxTargetWeight must be greater than zero and no greater than one.");
        }

        if (constraints.MinimumTradeNotional < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(constraints),
                "MinimumTradeNotional cannot be negative.");
        }

        if (constraints.MinimumCashReserve < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(constraints),
                "MinimumCashReserve cannot be negative.");
        }
    }

    private static IReadOnlyList<RebalanceConstraintViolation>
        EvaluateTargetConstraints(
            ResearchDecision decision,
            PortfolioSnapshot portfolio,
            RebalanceConstraints constraints,
            decimal targetWeight)
    {
        var violations =
            new List<RebalanceConstraintViolation>();

        if (targetWeight < 0m)
        {
            violations.Add(
                new RebalanceConstraintViolation(
                    "LongOnly",
                    "Target weight cannot be negative in the WS-03 long-only first slice."));
        }

        if (targetWeight >
            constraints.MaxTargetWeight)
        {
            violations.Add(
                new RebalanceConstraintViolation(
                    "MaxTargetWeight",
                    $"Target weight {targetWeight} exceeds maximum {constraints.MaxTargetWeight}."));
        }

        if (decision.ValidUntil.HasValue &&
            decision.ValidUntil.Value <=
            portfolio.AsOf)
        {
            violations.Add(
                new RebalanceConstraintViolation(
                    "DecisionExpired",
                    "Research decision is expired at the portfolio snapshot time."));
        }

        if (!string.IsNullOrWhiteSpace(
                decision.Instrument.Currency) &&
            !string.Equals(
                decision.Instrument.Currency,
                portfolio.BaseCurrency.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(
                new RebalanceConstraintViolation(
                    "CurrencyMismatch",
                    "WS-03 first slice does not perform FX conversion between instrument and portfolio currencies."));
        }

        return violations;
    }

    private static bool IsSameInstrument(
        InstrumentReference current,
        InstrumentReference target)
    {
        if (current.AssetClass !=
            target.AssetClass)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(
                current.VenueInstrumentId) &&
            !string.IsNullOrWhiteSpace(
                target.VenueInstrumentId))
        {
            return string.Equals(
                current.VenueInstrumentId.Trim(),
                target.VenueInstrumentId.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        if (!string.Equals(
                current.Symbol.Trim(),
                target.Symbol,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(
                   current.Currency) ||
               string.IsNullOrWhiteSpace(
                   target.Currency) ||
               string.Equals(
                   current.Currency.Trim(),
                   target.Currency,
                   StringComparison.OrdinalIgnoreCase);
    }
}
