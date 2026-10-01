using Microsoft.EntityFrameworkCore;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Infrastructure.Persistence.Repositories;

public sealed class EfExecutionMetricsRepository(
    TradeOpsDbContext dbContext,
    RiskSettings riskSettings) : IExecutionMetricsRepository
{
    public async Task<ExecutionMetricsSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var signalCounts = await dbContext.TradingSignals
            .AsNoTracking()
            .GroupBy(signal => signal.Outcome)
            .Select(group => new
            {
                Outcome = group.Key,
                Count = group.LongCount()
            })
            .ToDictionaryAsync(
                item => item.Outcome,
                item => item.Count,
                cancellationToken);

        var acceptedSignals = signalCounts.GetValueOrDefault(SignalOutcome.Accepted);
        var rejectedSignals = signalCounts.GetValueOrDefault(SignalOutcome.Rejected);
        var pendingSignals = signalCounts.GetValueOrDefault(SignalOutcome.Received);
        var receivedSignals = acceptedSignals + rejectedSignals + pendingSignals;

        var orderCounts = await dbContext.Orders
            .AsNoTracking()
            .GroupBy(order => order.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.LongCount()
            })
            .ToDictionaryAsync(
                item => item.Status,
                item => item.Count,
                cancellationToken);

        var totalOrders = orderCounts.Values.Sum();
        var fillsReceived = await dbContext.Fills
            .AsNoTracking()
            .LongCountAsync(cancellationToken);

        var persistedRiskState = await dbContext.OperationalRiskStates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                state => state.Id == OperationalRiskState.DefaultId,
                cancellationToken);

        var runStatuses = await dbContext.OperationalRunStatuses
            .AsNoTracking()
            .Where(status =>
                status.RunType == OperationalRunTypes.OrderReconciliation ||
                status.RunType == OperationalRunTypes.RecoveryCycle)
            .ToArrayAsync(cancellationToken);

        var reconciliation = runStatuses.FirstOrDefault(
            status => status.RunType == OperationalRunTypes.OrderReconciliation);
        var recovery = runStatuses.FirstOrDefault(
            status => status.RunType == OperationalRunTypes.RecoveryCycle);

        return new ExecutionMetricsSnapshot(
            DateTimeOffset.UtcNow,
            new SignalExecutionMetrics(
                receivedSignals,
                acceptedSignals,
                rejectedSignals,
                pendingSignals),
            new CurrentOrderStatusMetrics(
                totalOrders,
                orderCounts.GetValueOrDefault(OrderStatus.Created),
                orderCounts.GetValueOrDefault(OrderStatus.Submitted),
                orderCounts.GetValueOrDefault(OrderStatus.Accepted),
                orderCounts.GetValueOrDefault(OrderStatus.PartiallyFilled),
                orderCounts.GetValueOrDefault(OrderStatus.Filled),
                orderCounts.GetValueOrDefault(OrderStatus.Cancelled),
                orderCounts.GetValueOrDefault(OrderStatus.Rejected),
                orderCounts.GetValueOrDefault(OrderStatus.Unknown)),
            fillsReceived,
            new ExecutionRiskMetrics(
                persistedRiskState?.TradingEnabled ?? riskSettings.TradingEnabled,
                persistedRiskState?.EmergencyStop ?? riskSettings.EmergencyStop),
            new OperationalExecutionMetrics(
                reconciliation?.Succeeded,
                recovery?.Succeeded,
                recovery?.PositionMismatches));
    }
}
