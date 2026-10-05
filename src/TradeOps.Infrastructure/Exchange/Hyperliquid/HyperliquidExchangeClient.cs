using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradeOps.Application.Interfaces;
using TradeOps.Application.Models;
using TradeOps.Domain.Entities;
using TradeOps.Domain.Enums;

namespace TradeOps.Infrastructure.Exchange.Hyperliquid;

public sealed class HyperliquidExchangeClient : IExchangeClient, IExchangeConnectionManager
{
    public const string HttpClientName = "hyperliquid-testnet";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HyperliquidOptions _options;
    private readonly ILogger<HyperliquidExchangeClient> _logger;
    private volatile bool _isConnected;

    public HyperliquidExchangeClient(
        IHttpClientFactory httpClientFactory,
        HyperliquidOptions options,
        ILogger<HyperliquidExchangeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;

        ValidateTestnetConfiguration();
    }

    public bool IsConnected => _isConnected;

    public async Task EnsureConnectedAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await GetAccountAsync(cancellationToken);
            _isConnected = true;
            _logger.LogInformation(
                "ExchangeReadinessSucceeded for Hyperliquid testnet.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _isConnected = false;
            _logger.LogError(
                exception,
                "ExchangeReadinessFailed for Hyperliquid testnet.");
            throw;
        }
    }

    public async Task<AccountInfo> GetAccountAsync(
        CancellationToken cancellationToken = default)
    {
        var state = await GetClearinghouseStateAsync(cancellationToken);

        return new AccountInfo
        {
            Currency = "USDC",
            Balance = ParseDecimal(state.MarginSummary.TotalRawUsd),
            Equity = ParseDecimal(state.MarginSummary.AccountValue),
            AvailableBalance = ParseDecimal(state.Withdrawable)
        };
    }

    public async Task<IReadOnlyCollection<Position>> GetPositionsAsync(
        CancellationToken cancellationToken = default)
    {
        var state = await GetClearinghouseStateAsync(cancellationToken);

        return state.AssetPositions
            .Select(item => MapPosition(item.Position))
            .Where(position => position.Quantity > 0m)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<Order>> GetOpenOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        var orders = await SendInfoAsync<HyperliquidOrderDto[]>(
            new
            {
                type = "frontendOpenOrders",
                user = _options.UserAddress
            },
            cancellationToken);

        return orders
            .Select(order => MapOrder(order, "open", order.Timestamp))
            .ToArray();
    }

    public Task<OrderResult> PlaceOrderAsync(
        PlaceOrderRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Hyperliquid testnet order placement is not enabled in the read-only milestone.");

    public Task CancelOrderAsync(
        string exchangeOrderId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Hyperliquid testnet cancellation is not enabled in the read-only milestone.");

    public Task CancelOrderAsync(
        string exchangeOrderId,
        string? symbol,
        CancellationToken cancellationToken = default) =>
        CancelOrderAsync(exchangeOrderId, cancellationToken);

    public Task<Order?> GetOrderAsync(
        string exchangeOrderId,
        CancellationToken cancellationToken = default) =>
        GetOrderAsync(exchangeOrderId, symbol: null, cancellationToken);

    public async Task<Order?> GetOrderAsync(
        string exchangeOrderId,
        string? symbol,
        CancellationToken cancellationToken = default)
    {
        _ = symbol;

        object oid = long.TryParse(
            exchangeOrderId,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : exchangeOrderId;

        return await GetOrderStatusAsync(oid, cancellationToken);
    }

    public Task<Order?> GetOrderByClientOrderIdAsync(
        string clientOrderId,
        CancellationToken cancellationToken = default) =>
        GetOrderByClientOrderIdAsync(
            clientOrderId,
            symbol: null,
            cancellationToken);

    public async Task<Order?> GetOrderByClientOrderIdAsync(
        string clientOrderId,
        string? symbol,
        CancellationToken cancellationToken = default)
    {
        _ = symbol;

        if (!IsCloid(clientOrderId))
        {
            return null;
        }

        return await GetOrderStatusAsync(clientOrderId, cancellationToken);
    }

    private async Task<HyperliquidClearinghouseStateDto>
        GetClearinghouseStateAsync(CancellationToken cancellationToken)
    {
        return await SendInfoAsync<HyperliquidClearinghouseStateDto>(
            new
            {
                type = "clearinghouseState",
                user = _options.UserAddress
            },
            cancellationToken);
    }

    private async Task<Order?> GetOrderStatusAsync(
        object oid,
        CancellationToken cancellationToken)
    {
        var response = await SendInfoAsync<HyperliquidOrderStatusEnvelopeDto>(
            new
            {
                type = "orderStatus",
                user = _options.UserAddress,
                oid
            },
            cancellationToken);

        if (!string.Equals(
                response.Status,
                "order",
                StringComparison.OrdinalIgnoreCase)
            || response.Order is null)
        {
            return null;
        }

        return MapOrder(
            response.Order.Order,
            response.Order.Status,
            response.Order.StatusTimestamp);
    }

    private async Task<T> SendInfoAsync<T>(
        object payload,
        CancellationToken cancellationToken)
    {
        ValidateUserAddress();

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync(
            BuildInfoUri(),
            payload,
            JsonOptions,
            cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _isConnected = false;
            throw new HttpRequestException(
                $"Hyperliquid returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
        }

        var result = JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                "Hyperliquid returned an empty or invalid JSON response.");

        _isConnected = true;
        return result;
    }

    private Position MapPosition(HyperliquidPositionDto position)
    {
        var signedSize = ParseDecimal(position.SignedSize);
        var quantity = Math.Abs(signedSize);
        var positionValue = Math.Abs(ParseDecimal(position.PositionValue));
        var markPrice = quantity > 0m
            ? positionValue / quantity
            : 0m;

        return new Position
        {
            Symbol = NormalizeSymbol(position.Coin),
            Side = signedSize >= 0m ? OrderSide.Buy : OrderSide.Sell,
            Quantity = quantity,
            AverageEntryPrice = ParseDecimal(position.EntryPrice),
            MarkPrice = markPrice,
            UnrealizedPnL = ParseDecimal(position.UnrealizedPnl)
        };
    }

    private static Order MapOrder(
        HyperliquidOrderDto order,
        string status,
        long statusTimestamp)
    {
        var originalQuantity = ParseDecimal(order.OriginalSize);
        var remainingQuantity = ParseDecimal(order.RemainingSize);
        var filledQuantity = Math.Max(
            0m,
            originalQuantity - remainingQuantity);

        var timestamp = statusTimestamp > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(statusTimestamp)
            : DateTimeOffset.UtcNow;

        return new Order
        {
            Id = Guid.NewGuid(),
            ExchangeOrderId = order.OrderId.ToString(CultureInfo.InvariantCulture),
            ClientOrderId = string.IsNullOrWhiteSpace(order.ClientOrderId)
                ? $"hl-oid-{order.OrderId}"
                : order.ClientOrderId,
            Symbol = NormalizeSymbol(order.Coin),
            Side = MapSide(order.Side),
            OrderType = MapOrderType(order.OrderType),
            RequestedQuantity = originalQuantity,
            FilledQuantity = filledQuantity,
            AverageFillPrice = null,
            Price = ParsePositiveNullableDecimal(order.LimitPrice),
            Status = MapOrderStatus(status),
            CreatedAt = order.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(order.Timestamp)
                : timestamp,
            UpdatedAt = timestamp
        };
    }

    private void ValidateTestnetConfiguration()
    {
        if (!Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var uri)
            || !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                uri.Host,
                "api.hyperliquid-testnet.xyz",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Hyperliquid adapter is restricted to the official testnet API host.");
        }
    }

    private void ValidateUserAddress()
    {
        var address = _options.UserAddress?.Trim();

        if (string.IsNullOrWhiteSpace(address)
            || address.Length != 42
            || !address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            || !address.AsSpan(2).ToString().All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException(
                "Hyperliquid testnet UserAddress must be a 42-character hexadecimal onchain address.");
        }
    }

    private string BuildInfoUri() =>
        $"{_options.BaseUrl.TrimEnd('/')}/info";

    private static bool IsCloid(string value) =>
        value.Length == 34
        && value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        && value.AsSpan(2).ToString().All(Uri.IsHexDigit);

    private static string NormalizeSymbol(string coin) =>
        coin.Trim().ToUpperInvariant();

    private static OrderSide MapSide(string side) =>
        side.ToUpperInvariant() switch
        {
            "B" => OrderSide.Buy,
            "A" => OrderSide.Sell,
            _ => throw new InvalidOperationException(
                $"Unsupported Hyperliquid side '{side}'.")
        };

    private static OrderType MapOrderType(string orderType) =>
        orderType.ToUpperInvariant() switch
        {
            "MARKET" => OrderType.Market,
            _ => OrderType.Limit
        };

    private static OrderStatus MapOrderStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "open" => OrderStatus.Accepted,
            "filled" => OrderStatus.Filled,
            "rejected" => OrderStatus.Rejected,
            "triggered" => OrderStatus.Accepted,
            "canceled" => OrderStatus.Cancelled,
            "margincanceled" => OrderStatus.Cancelled,
            "vaultwithdrawalcanceled" => OrderStatus.Cancelled,
            "openinterestcapcanceled" => OrderStatus.Cancelled,
            "selftradecanceled" => OrderStatus.Cancelled,
            "reduceonlycanceled" => OrderStatus.Cancelled,
            "siblingfilledcanceled" => OrderStatus.Cancelled,
            "delistedcanceled" => OrderStatus.Cancelled,
            "liquidatedcanceled" => OrderStatus.Cancelled,
            "scheduledcancel" => OrderStatus.Cancelled,
            _ when status.EndsWith(
                "Rejected",
                StringComparison.OrdinalIgnoreCase) => OrderStatus.Rejected,
            _ => OrderStatus.Unknown
        };

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0m;

    private static decimal? ParsePositiveNullableDecimal(string? value)
    {
        var parsed = ParseDecimal(value);
        return parsed > 0m ? parsed : null;
    }
}
