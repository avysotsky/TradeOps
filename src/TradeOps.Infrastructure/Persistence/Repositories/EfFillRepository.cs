using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradeOps.Application.Interfaces;
using TradeOps.Domain.Entities;

namespace TradeOps.Infrastructure.Persistence.Repositories;

public sealed class EfFillRepository(TradeOpsDbContext dbContext) : IFillRepository
{
    public async Task<bool> TryAddAsync(
        Fill fill,
        CancellationToken cancellationToken = default)
    {
        dbContext.Fills.Add(fill);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            dbContext.Entry(fill).State = EntityState.Detached;
            return false;
        }
    }
}
