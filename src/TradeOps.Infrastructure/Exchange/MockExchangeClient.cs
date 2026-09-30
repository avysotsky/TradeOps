using System.Collections.Concurrent;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Infrastructure.Exchange;

public sealed class MockExchangeClient : IExchangeClient
{
    private readonly ConcurrentDictionary<string, Order> _ordersByClientOrderId = new();

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
        IReadOnlyCollection<Order> result = _ordersByClientOrderId.Values
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
        var order = _ordersByClientOrderId.GetOrAdd(
            request.ClientOrderId,
            _ => CreatePartiallyFilledOrder(request));

        return Task.FromResult(ToResult(order));
    }

    public Task CancelOrderAsync(
        string exchangeOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = FindByExchangeOrderId(exchangeOrderId)
            ?? throw new KeyNotFoundException($"Order '{exchangeOrderId}' was not found.");

        lock (order)
        {
            if (order.Status != OrderStatus.Filled)
            {
                order.Status = OrderStatus.Cancelled;
                order.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    public Task<Order?> GetOrderAsync(
        string exchangeOrderId,
        CancellationToken cancellationToken = default)
    {
        var order = FindByExchangeOrderId(exchangeOrderId);

        if (order is not null)
        {
            AdvancePartialFill(order);
        }

        return Task.FromResult(order);
    }

    public Task<Order?> GetOrderByClientOrderIdAsync(
        string clientOrderId,
        CancellationToken cancellationToken = default)
    {
        _ordersByClientOrderId.TryGetValue(clientOrderId, out var order);

        if (order is not null)
        {
            AdvancePartialFill(order);
        }

        return Task.FromResult(order);
    }

    private Order? FindByExchangeOrderId(string exchangeOrderId)
    {
        return _ordersByClientOrderId.Values.FirstOrDefault(
            candidate => candidate.ExchangeOrderId == exchangeOrderId);
    }

    private static Order CreatePartiallyFilledOrder(PlaceOrderRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var averageFillPrice = request.Price ?? 64_000m;

        return new Order
        {
            Id = Guid.NewGuid(),
            ExchangeOrderId = $"mock-{Guid.NewGuid():N}",
            ClientOrderId = request.ClientOrderId,
            Symbol = request.Symbol,
            Side = request.Side,
            OrderType = request.OrderType,
            RequestedQuantity = request.Quantity,
            FilledQuantity = request.Quantity * 0.60m,
            AverageFillPrice = averageFillPrice,
            Price = request.Price,
            Status = OrderStatus.PartiallyFilled,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static void AdvancePartialFill(Order order)
    {
        lock (order)
        {
            if (order.Status != OrderStatus.PartiallyFilled)
            {
                return;
            }

            order.FilledQuantity = order.RequestedQuantity;
            order.Status = OrderStatus.Filled;
            order.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private static OrderResult ToResult(Order order)
    {
        return new OrderResult(
            order.ExchangeOrderId,
            order.ClientOrderId,
            order.Status,
            order.FilledQuantity,
            order.AverageFillPrice);
    }
}
