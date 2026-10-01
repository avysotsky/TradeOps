using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Application.Services;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;
using Xunit;

namespace TradeOps.UnitTests;

public sealed class RiskControlServiceTests
{
    [Fact]
    public async Task GetSnapshotAsync_PositionOpenedBeforeUtcDayAndClosedToday_CountsTodayRealizedPnl()
    {
        var now = DateTimeOffset.UtcNow;
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var fills = new[]
        {
            new PositionFill("BTCUSDT", OrderSide.Buy, 1m, 100m, dayStart.AddHours(-1), "open-yesterday"),
            new PositionFill("BTCUSDT", OrderSide.Sell, 1m, 120m, dayStart, "close-today")
        };
        var riskEvents = new FakeRiskEventRepository
        {
            ActiveEvents =
            [
                new RiskEvent
                {
                    Id = Guid.NewGuid(),
                    EventType = "PositionMismatch",
                    EventKey = "ETHUSDT",
                    Symbol = "ETHUSDT",
                    Message = "mismatch",
                    Severity = RiskEventSeverity.Warning,
                    CreatedAt = now,
                    LastObservedAt = now
                }
            ]
        };
        var service = CreateService(fills, riskEvents, out _, out _);

        var snapshot = await service.GetSnapshotAsync();

        Assert.Equal(20m, snapshot.DailyRealizedPnL);
        Assert.Equal(1, snapshot.ActivePositionMismatchCount);
        Assert.True(snapshot.HasPositionMismatch);
    }

    [Fact]
    public async Task SetEmergencyStopAsync_PersistsStateAndEmitsSingleTransitionAlert()
    {
        var service = CreateService([], new FakeRiskEventRepository(), out var stateRepository, out var alerts);

        var first = await service.SetEmergencyStopAsync(true, "operator requested stop");
        var second = await service.SetEmergencyStopAsync(true, "operator requested stop");

        Assert.True(first.EmergencyStop);
        Assert.Equal("operator requested stop", first.EmergencyStopReason);
        Assert.True(stateRepository.State.EmergencyStop);
        Assert.Equal("operator requested stop", stateRepository.State.EmergencyStopReason);
        Assert.Single(alerts.Alerts);
        Assert.Equal("EmergencyStopActivated", alerts.Alerts[0].EventType);
        Assert.True(second.EmergencyStop);
    }

    private static RiskControlService CreateService(
        IReadOnlyCollection<PositionFill> fills,
        FakeRiskEventRepository riskEvents,
        out FakeOperationalRiskStateRepository stateRepository,
        out FakeAlertService alerts)
    {
        stateRepository = new FakeOperationalRiskStateRepository();
        alerts = new FakeAlertService();

        return new RiskControlService(
            stateRepository,
            new FakeFillRepository(fills),
            riskEvents,
            alerts,
            new RiskSettings());
    }

    private sealed class FakeOperationalRiskStateRepository : IOperationalRiskStateRepository
    {
        public OperationalRiskState State { get; private set; } = new()
        {
            Id = OperationalRiskState.DefaultId,
            TradingEnabled = true,
            EmergencyStop = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        public Task<OperationalRiskState> GetOrCreateAsync(
            bool initialTradingEnabled,
            bool initialEmergencyStop,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task UpdateAsync(
            OperationalRiskState state,
            CancellationToken cancellationToken = default)
        {
            State = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFillRepository(IReadOnlyCollection<PositionFill> fills) : IFillRepository
    {
        public Task<bool> TryAddAsync(
            Fill fill,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyCollection<PositionFill>> GetPositionFillsAsync(
            string? symbol = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<PositionFill>>(
                symbol is null
                    ? fills
                    : fills.Where(fill => string.Equals(fill.Symbol, symbol, StringComparison.OrdinalIgnoreCase)).ToArray());
    }

    private sealed class FakeRiskEventRepository : IRiskEventRepository
    {
        public IReadOnlyCollection<RiskEvent> ActiveEvents { get; init; } = Array.Empty<RiskEvent>();
        public List<RiskEvent> Added { get; } = [];

        public Task<RiskEvent?> GetActiveAsync(
            string eventType,
            string eventKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RiskEvent?>(ActiveEvents.FirstOrDefault(x =>
                x.EventType == eventType && x.EventKey == eventKey && x.ResolvedAt is null));

        public Task<IReadOnlyCollection<RiskEvent>> GetActiveByTypeAsync(
            string eventType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<RiskEvent>>(
                ActiveEvents.Where(x => x.EventType == eventType && x.ResolvedAt is null).ToArray());

        public Task<bool> TryAddAsync(
            RiskEvent riskEvent,
            CancellationToken cancellationToken = default)
        {
            Added.Add(riskEvent);
            return Task.FromResult(true);
        }

        public Task UpdateAsync(
            RiskEvent riskEvent,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyCollection<RiskEvent>> GetRecentAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<RiskEvent>>(Added.Take(limit).ToArray());
    }

    private sealed class FakeAlertService : IAlertService
    {
        public List<AlertMessage> Alerts { get; } = [];

        public Task SendAsync(
            AlertMessage alert,
            CancellationToken cancellationToken = default)
        {
            Alerts.Add(alert);
            return Task.CompletedTask;
        }
    }
}
