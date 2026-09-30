using TradeOps.Application.Interfaces;

namespace TradeOps.Application.Services;

public sealed class ClientOrderIdGenerator : IClientOrderIdGenerator
{
    public string Generate(Guid signalId) => $"tradeops-{signalId:N}";
}
