using TradeOps.Application.Models;
using TradeOps.Domain.Entities;

namespace TradeOps.Application.Interfaces;

public interface IExchangeClient
{
    Task<AccountInfo> GetAccountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Position>> GetPositionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Order>> GetOpenOrdersAsync(
        CancellationToken cancellationToken = default);

    Task<OrderResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        CancellationToken cancellationToken = default);

    Task CancelOrderAsync(
        string exchangeOrderId,
        CancellationToken cancellationToken = default);

    Task<Order?> GetOrderAsync(
        string exchangeOrderId,
        CancellationToken cancellationToken = default);

    Task<Order?> GetOrderByClientOrderIdAsync(
        string clientOrderId,
        CancellationToken cancellationToken = default);
}
