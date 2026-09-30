using System.Collections.Concurrent;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Infrastructure.Exchange;

public sealed class MockExchangeClient : IExchangeClient
{
    private readonly ConcurrentDictionary<string, Order> _orders = new();

    private readonly List<Position> _positions =
    [
        new Position
        {
            Symbol = "BTCUSDT",
            Side = OrderSide.Buy,
            Quantity = 0.002m,
            AverageEntryPrice = 63_500m,
            MarkPrice = 64_000m,
            UnrealizedPnL = 1.00m
        }
    ];

    public Task<AccountInfo> GetAccountAsync(CancellationToken cancellationToken = default)
    {
        var account = new AccountInfo
        {
            Currency = "USDT",
            Balance = 10_000m,
            Equity = 10_001m,
            AvailableBalance = 8_500m
        };

        return Task.FromResult(account);
    }

    public Task<IReadOnlyCollection<Position>> GetPositionsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Position> result = _positions.AsReadOnly();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyCollection<Order>> GetOpenOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Order> result = _orders.Values
            .Where(order => order.Status is OrderStatus.Created
                or OrderStatus.Submitted
                or OrderStatus.Accepted
                or OrderStatus.PartiallyFilled
                or OrderStatus.Unknown)
            .OrderBy(order => order.CreatedAt)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<OrderResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var exchangeOrderId = $"mock-{Guid.NewGuid():N}";

        var order = new Order
        {
            Id = Guid.NewGuid(),
            ExchangeOrderId = exchangeOrderId,
            ClientOrderId = request.ClientOrderId,
            Symbol = request.Symbol,
            Side = request.Side,
            OrderType = request.OrderType,
            RequestedQuantity = request.Quantity,
            FilledQuantity = 0m,
            Price = request.Price,
            Status = OrderStatus.Accepted,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (!_orders.TryAdd(exchangeOrderId, order))
        {
            throw new InvalidOperationException("Could not add mock order.");
        }

        return Task.FromResult(new OrderResult(
            exchangeOrderId,
            request.ClientOrderId,
            order.Status,
            order.FilledQuantity,
            order.AverageFillPrice));
    }

    public Task CancelOrderAsync(
        string exchangeOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(exchangeOrderId, out var order))
        {
            throw new KeyNotFoundException($"Order '{exchangeOrderId}' was not found.");
        }

        order.Status = OrderStatus.Cancelled;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        return Task.CompletedTask;
    }

    public Task<Order?> GetOrderAsync(
        string exchangeOrderId,
        CancellationToken cancellationToken = default)
    {
        _orders.TryGetValue(exchangeOrderId, out var order);
        return Task.FromResult(order);
    }
}
