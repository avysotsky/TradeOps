using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TradeOps.Application.Models;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services;

public sealed record EarningsDecisionRuleSettings(
    decimal RevenueGrowthThreshold = 0.05m,
    decimal DilutedEpsGrowthThreshold = 0.05m,
    decimal OperatingMarginDeltaThreshold = 0.005m,
    int MinimumDirectionalSignals = 2);

public static class DeterministicEarningsDecisionRule
{
    public const string DefaultStrategyId =
        "earnings-fundamentals-demo-v1";

    public static ResearchDecision Evaluate(
        EarningsEvent current,
        EarningsEvent priorComparable,
        DateTimeOffset generatedAt,
        string strategyId = DefaultStrategyId,
        EarningsDecisionRuleSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(priorComparable);

        settings ??=
            new EarningsDecisionRuleSettings();

        ValidateSettings(settings);
        ValidateAvailabilityInvariant(
            current,
            nameof(current));
        ValidateAvailabilityInvariant(
            priorComparable,
            nameof(priorComparable));

        if (!string.Equals(
                current.Instrument.Symbol,
                priorComparable.Instrument.Symbol,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Current and prior earnings events must reference the same symbol.");
        }

        if (priorComparable.PublishedAt >=
            current.PublishedAt)
        {
            throw new ArgumentException(
                "Prior comparable event must have been published before the current event.",
                nameof(priorComparable));
        }

        var normalizedGeneratedAt =
            generatedAt.ToUniversalTime();

        if (normalizedGeneratedAt <
            current.PublishedAt.ToUniversalTime())
        {
            throw new ArgumentOutOfRangeException(
                nameof(generatedAt),
                "Research decisions cannot be generated before the source event became available.");
        }

        var signals =
            new List<int>();

        var revenueGrowth =
            AddGrowthSignal(
                signals,
                current.Snapshot.Revenue,
                priorComparable.Snapshot.Revenue,
                settings.RevenueGrowthThreshold);

        var epsGrowth =
            AddGrowthSignal(
                signals,
                current.Snapshot.DilutedEps,
                priorComparable.Snapshot.DilutedEps,
                settings.DilutedEpsGrowthThreshold);

        var operatingMarginDelta =
            AddDeltaSignal(
                signals,
                current.Snapshot.OperatingMargin,
                priorComparable.Snapshot.OperatingMargin,
                settings.OperatingMarginDeltaThreshold);

        var score =
            signals.Sum();

        var action =
            score >= settings.MinimumDirectionalSignals
                ? ResearchDecisionAction.Buy
                : score <= -settings.MinimumDirectionalSignals
                    ? ResearchDecisionAction.Sell
                    : ResearchDecisionAction.NoAction;

        var confidence =
            signals.Count == 0
                ? 0m
                : Math.Round(
                    Math.Min(
                        1m,
                        Math.Abs(score) /
                        (decimal)signals.Count),
                    6,
                    MidpointRounding.AwayFromZero);

        var metadata =
            new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["researchRule"] =
                    "deterministic-earnings-comparison-v1",
                ["provider"] =
                    current.Provenance.Provider,
                ["fiscalPeriod"] =
                    current.FiscalPeriod,
                ["sourceDocumentId"] =
                    current.Provenance.SourceDocumentId
                    ?? "n/a",
                ["sourceTimestamp"] =
                    current.Provenance.SourceTimestamp
                        .ToUniversalTime()
                        .ToString(
                            "O",
                            CultureInfo.InvariantCulture),
                ["publishedAt"] =
                    current.PublishedAt
                        .ToUniversalTime()
                        .ToString(
                            "O",
                            CultureInfo.InvariantCulture),
                ["score"] =
                    score.ToString(
                        CultureInfo.InvariantCulture),
                ["comparableSignals"] =
                    signals.Count.ToString(
                        CultureInfo.InvariantCulture),
                ["revenueGrowth"] =
                    FormatNullable(revenueGrowth),
                ["dilutedEpsGrowth"] =
                    FormatNullable(epsGrowth),
                ["operatingMarginDelta"] =
                    FormatNullable(operatingMarginDelta)
            };

        var decision =
            new ResearchDecision(
                CreateDecisionId(
                    current,
                    strategyId),
                strategyId,
                current.Instrument,
                action,
                normalizedGeneratedAt,
                Confidence: confidence,
                SourceEventId: current.EventId,
                Reason:
                    $"Deterministic earnings comparison score {score} from {signals.Count} comparable KPI signals.",
                Metadata: metadata);

        var validation =
            ResearchDecisionValidator.Validate(
                decision);

        if (!validation.IsValid
            || validation.NormalizedDecision is null)
        {
            var errors =
                string.Join(
                    "; ",
                    validation.Errors.SelectMany(
                        item =>
                            item.Value.Select(
                                message =>
                                    $"{item.Key}: {message}")));

            throw new InvalidOperationException(
                $"Generated ResearchDecision is invalid. {errors}");
        }

        return validation.NormalizedDecision;
    }

    private static void ValidateAvailabilityInvariant(
        EarningsEvent earningsEvent,
        string parameterName)
    {
        if (earningsEvent.PublishedAt == default)
        {
            throw new ArgumentException(
                "PublishedAt is required.",
                parameterName);
        }

        if (earningsEvent.Provenance is null)
        {
            throw new ArgumentException(
                "Provenance is required.",
                parameterName);
        }

        if (earningsEvent.Provenance.SourceTimestamp == default)
        {
            throw new ArgumentException(
                "Provenance SourceTimestamp is required.",
                parameterName);
        }

        if (earningsEvent.Provenance.RetrievedAt == default)
        {
            throw new ArgumentException(
                "Provenance RetrievedAt is required.",
                parameterName);
        }

        var sourceTimestamp =
            earningsEvent.Provenance.SourceTimestamp
                .ToUniversalTime();
        var publishedAt =
            earningsEvent.PublishedAt
                .ToUniversalTime();
        var retrievedAt =
            earningsEvent.Provenance.RetrievedAt
                .ToUniversalTime();

        if (publishedAt < sourceTimestamp)
        {
            throw new ArgumentException(
                "PublishedAt cannot precede the provider source timestamp.",
                parameterName);
        }

        if (publishedAt > retrievedAt)
        {
            throw new ArgumentException(
                "PublishedAt cannot be later than the acquisition RetrievedAt timestamp.",
                parameterName);
        }
    }

    private static decimal? AddGrowthSignal(
        ICollection<int> signals,
        decimal? current,
        decimal? prior,
        decimal threshold)
    {
        if (!current.HasValue
            || !prior.HasValue
            || prior.Value == 0m)
        {
            return null;
        }

        var growth =
            (current.Value - prior.Value) /
            Math.Abs(prior.Value);

        signals.Add(
            Compare(
                growth,
                threshold));

        return growth;
    }

    private static decimal? AddDeltaSignal(
        ICollection<int> signals,
        decimal? current,
        decimal? prior,
        decimal threshold)
    {
        if (!current.HasValue
            || !prior.HasValue)
        {
            return null;
        }

        var delta =
            current.Value - prior.Value;

        signals.Add(
            Compare(
                delta,
                threshold));

        return delta;
    }

    private static int Compare(
        decimal value,
        decimal threshold)
    {
        if (value >= threshold)
        {
            return 1;
        }

        if (value <= -threshold)
        {
            return -1;
        }

        return 0;
    }

    private static string CreateDecisionId(
        EarningsEvent current,
        string strategyId)
    {
        if (string.IsNullOrWhiteSpace(strategyId))
        {
            throw new ArgumentException(
                "StrategyId is required.",
                nameof(strategyId));
        }

        var canonical =
            string.Join(
                "|",
                strategyId.Trim(),
                current.Instrument.Symbol.Trim().ToUpperInvariant(),
                current.EventId.Trim());

        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    canonical));

        return
            $"erd-{Convert.ToHexString(hash)[..24].ToLowerInvariant()}";
    }

    private static string FormatNullable(
        decimal? value) =>
        value?.ToString(
            "0.########",
            CultureInfo.InvariantCulture)
        ?? "n/a";

    private static void ValidateSettings(
        EarningsDecisionRuleSettings settings)
    {
        if (settings.RevenueGrowthThreshold < 0m
            || settings.DilutedEpsGrowthThreshold < 0m
            || settings.OperatingMarginDeltaThreshold < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "Earnings comparison thresholds cannot be negative.");
        }

        if (settings.MinimumDirectionalSignals is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "MinimumDirectionalSignals must be between 1 and 3.");
        }
    }
}
