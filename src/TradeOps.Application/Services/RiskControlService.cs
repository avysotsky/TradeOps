using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Application.Services;

public sealed class RiskControlService(
    IOperationalRiskStateRepository operationalStateRepository,
    IFillRepository fillRepository,
    IRiskEventRepository riskEventRepository,
    IAlertService alertService,
    RiskSettings settings) : IRiskControlService
{
    private const string PositionMismatchEventType = "PositionMismatch";
    private const string RiskRejectedEventType = "RiskRejected";

    public async Task<RiskControlSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        var state = await operationalStateRepository.GetOrCreateAsync(
            settings.TradingEnabled,
            settings.EmergencyStop,
            cancellationToken);

        var fills = await fillRepository.GetPositionFillsAsync(
            cancellationToken: cancellationToken);
        var activeMismatches = await riskEventRepository.GetActiveByTypeAsync(
            PositionMismatchEventType,
            cancellationToken);

        return new RiskControlSnapshot(
            state.TradingEnabled,
            state.EmergencyStop,
            state.EmergencyStopReason,
            CalculateDailyRealizedPnL(fills, DateTimeOffset.UtcNow),
            activeMismatches.Count,
            state.UpdatedAt);
    }

    public async Task<RiskControlSnapshot> SetTradingEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var state = await operationalStateRepository.GetOrCreateAsync(
            settings.TradingEnabled,
            settings.EmergencyStop,
            cancellationToken);

        var changed = state.TradingEnabled != enabled;
        state.TradingEnabled = enabled;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        await operationalStateRepository.UpdateAsync(state, cancellationToken);

        if (changed)
        {
            await alertService.SendAsync(
                new AlertMessage(
                    enabled ? "TradingEnabled" : "TradingDisabled",
                    enabled
                        ? "TradeOps trading has been enabled."
                        : "TradeOps trading has been disabled.",
                    enabled ? AlertSeverity.Info : AlertSeverity.Warning),
                cancellationToken);
        }

        return await GetSnapshotAsync(cancellationToken);
    }

    public async Task<RiskControlSnapshot> SetEmergencyStopAsync(
        bool enabled,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var state = await operationalStateRepository.GetOrCreateAsync(
            settings.TradingEnabled,
            settings.EmergencyStop,
            cancellationToken);

        var changed = state.EmergencyStop != enabled;
        state.EmergencyStop = enabled;
        state.EmergencyStopReason = enabled
            ? NormalizeReason(reason)
            : null;
        state.UpdatedAt = DateTimeOffset.UtcNow;
        await operationalStateRepository.UpdateAsync(state, cancellationToken);

        if (changed)
        {
            await alertService.SendAsync(
                new AlertMessage(
                    enabled ? "EmergencyStopActivated" : "EmergencyStopCleared",
                    enabled
                        ? $"TradeOps emergency stop activated. {state.EmergencyStopReason}"
                        : "TradeOps emergency stop cleared.",
                    enabled ? AlertSeverity.Critical : AlertSeverity.Info),
                cancellationToken);
        }

        return await GetSnapshotAsync(cancellationToken);
    }

    public async Task RecordRejectionAsync(
        TradingSignal signal,
        RiskDecision decision,
        CancellationToken cancellationToken = default)
    {
        if (decision.IsAllowed)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var riskEvent = new RiskEvent
        {
            Id = Guid.NewGuid(),
            EventType = RiskRejectedEventType,
            EventKey = signal.Id.ToString("N"),
            Symbol = signal.Symbol,
            Message = Truncate(string.Join("; ", decision.Reasons), 1000),
            Severity = RiskEventSeverity.Warning,
            LocalSide = signal.Side,
            LocalQuantity = signal.RequestedQuantity,
            CreatedAt = now,
            LastObservedAt = now,
            ResolvedAt = now
        };

        await riskEventRepository.TryAddAsync(riskEvent, cancellationToken);
    }

    internal static decimal CalculateDailyRealizedPnL(
        IReadOnlyCollection<PositionFill> fills,
        DateTimeOffset now)
    {
        if (fills.Count == 0)
        {
            return 0m;
        }

        var utcNow = now.ToUniversalTime();
        var dayStart = new DateTimeOffset(utcNow.UtcDateTime.Date, TimeSpan.Zero);

        var currentRealized = CalculateTotalRealized(
            fills.Where(fill => fill.FilledAt <= utcNow),
            utcNow);
        var realizedBeforeDay = CalculateTotalRealized(
            fills.Where(fill => fill.FilledAt < dayStart),
            dayStart);

        return currentRealized - realizedBeforeDay;
    }

    private static decimal CalculateTotalRealized(
        IEnumerable<PositionFill> fills,
        DateTimeOffset calculatedAt)
    {
        return fills
            .GroupBy(fill => fill.Symbol, StringComparer.OrdinalIgnoreCase)
            .Sum(group => PositionPnLCalculator.Calculate(
                group.Key,
                group,
                null,
                calculatedAt).RealizedPnL);
    }

    private static string NormalizeReason(string? reason)
    {
        var value = string.IsNullOrWhiteSpace(reason)
            ? "Emergency stop activated."
            : reason.Trim();
        return Truncate(value, 500);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
