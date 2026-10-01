using Microsoft.EntityFrameworkCore;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Infrastructure.Persistence.Repositories;

public sealed class EfOperationalRunStatusRepository(TradeOpsDbContext dbContext)
    : IOperationalRunStatusRepository
{
    public Task<OperationalRunStatus?> GetAsync(
        string runType,
        CancellationToken cancellationToken = default)
    {
        return dbContext.OperationalRunStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.RunType == runType, cancellationToken);
    }

    public async Task MarkStartedAsync(
        string runType,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var status = await dbContext.OperationalRunStatuses
            .FirstOrDefaultAsync(item => item.RunType == runType, cancellationToken);

        if (status is null)
        {
            status = new OperationalRunStatus
            {
                RunType = runType,
                StartedAt = now,
                IsRunning = true,
                UpdatedAt = now
            };
            dbContext.OperationalRunStatuses.Add(status);
        }
        else
        {
            status.StartedAt = now;
            status.CompletedAt = null;
            status.IsRunning = true;
            status.Succeeded = null;
            status.OrdersScanned = 0;
            status.OrdersUpdated = 0;
            status.OrderIssues = 0;
            status.OrdersMissingOnExchange = 0;
            status.PositionsCompared = 0;
            status.PositionMismatches = 0;
            status.PositionSnapshots = 0;
            status.ErrorMessage = null;
            status.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkCompletedAsync(
        string runType,
        OperationalRunMetrics metrics,
        CancellationToken cancellationToken = default)
    {
        var status = await GetTrackedOrCreateAsync(runType, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        status.CompletedAt = now;
        status.IsRunning = false;
        status.Succeeded = true;
        status.OrdersScanned = metrics.OrdersScanned;
        status.OrdersUpdated = metrics.OrdersUpdated;
        status.OrderIssues = metrics.OrderIssues;
        status.OrdersMissingOnExchange = metrics.OrdersMissingOnExchange;
        status.PositionsCompared = metrics.PositionsCompared;
        status.PositionMismatches = metrics.PositionMismatches;
        status.PositionSnapshots = metrics.PositionSnapshots;
        status.ErrorMessage = null;
        status.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        string runType,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var status = await GetTrackedOrCreateAsync(runType, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        status.CompletedAt = now;
        status.IsRunning = false;
        status.Succeeded = false;
        status.ErrorMessage = Truncate(errorMessage, 1000);
        status.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<OperationalRunStatus> GetTrackedOrCreateAsync(
        string runType,
        CancellationToken cancellationToken)
    {
        var status = await dbContext.OperationalRunStatuses
            .FirstOrDefaultAsync(item => item.RunType == runType, cancellationToken);

        if (status is not null)
        {
            return status;
        }

        var now = DateTimeOffset.UtcNow;
        status = new OperationalRunStatus
        {
            RunType = runType,
            StartedAt = now,
            IsRunning = true,
            UpdatedAt = now
        };
        dbContext.OperationalRunStatuses.Add(status);
        return status;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
