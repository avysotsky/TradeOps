using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeOps.Application.Interfaces;
using TradeOps.Domain.Entities;

namespace TradeOps.Infrastructure.Persistence.Repositories;

public sealed class EfTradingSignalRepository(TradeOpsDbContext dbContext)
    : ITradingSignalRepository
{
    public Task<TradingSignal?> GetByIdAsync(
        Guid signalId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.TradingSignals
            .AsNoTracking()
            .FirstOrDefaultAsync(
                signal => signal.Id == signalId,
                cancellationToken);
    }

    public async Task<bool> TryAddAsync(
        TradingSignal signal,
        CancellationToken cancellationToken = default)
    {
        dbContext.TradingSignals.Add(signal);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.Entry(signal).State = EntityState.Detached;
            return false;
        }
    }

    public async Task UpdateAsync(
        TradingSignal signal,
        CancellationToken cancellationToken = default)
    {
        dbContext.TradingSignals.Update(signal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
