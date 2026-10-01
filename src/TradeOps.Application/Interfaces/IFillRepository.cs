using TradeOps.Domain.Entities;

namespace TradeOps.Application.Interfaces;

public interface IFillRepository
{
    Task<bool> TryAddAsync(
        Fill fill,
        CancellationToken cancellationToken = default);
}
