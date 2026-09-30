using System.Text.Json.Serialization;

namespace TradeOps.Infrastructure.Exchange.Bybit;

internal sealed class BybitEnvelope<T>
{
    [JsonPropertyName("retCode")]
    public int RetCode { get; init; }

    [JsonPropertyName("retMsg")]
    public string RetMsg { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public T? Result { get; init; }
}

internal sealed class BybitWalletResult
{
    [JsonPropertyName("list")]
    public List<BybitWalletAccount> List { get; init; } = [];
}

internal sealed class BybitWalletAccount
{
    [JsonPropertyName("totalEquity")]
    public string TotalEquity { get; init; } = "0";

    [JsonPropertyName("totalWalletBalance")]
    public string TotalWalletBalance { get; init; } = "0";

    [JsonPropertyName("totalAvailableBalance")]
    public string TotalAvailableBalance { get; init; } = "0";
}

internal sealed class BybitPositionResult
{
    [JsonPropertyName("list")]
    public List<BybitPositionDto> List { get; init; } = [];
}

internal sealed class BybitPositionDto
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public string Size { get; init; } = "0";

    [JsonPropertyName("avgPrice")]
    public string AveragePrice { get; init; } = "0";

    [JsonPropertyName("markPrice")]
    public string MarkPrice { get; init; } = "0";

    [JsonPropertyName("unrealisedPnl")]
    public string UnrealizedPnl { get; init; } = "0";
}

internal sealed class BybitOrderListResult
{
    [JsonPropertyName("list")]
    public List<BybitOrderDto> List { get; init; } = [];
}

internal sealed class BybitOrderDto
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("orderLinkId")]
    public string OrderLinkId { get; init; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    [JsonPropertyName("orderType")]
    public string OrderType { get; init; } = string.Empty;

    [JsonPropertyName("qty")]
    public string Quantity { get; init; } = "0";

    [JsonPropertyName("cumExecQty")]
    public string CumulativeExecutedQuantity { get; init; } = "0";

    [JsonPropertyName("avgPrice")]
    public string AveragePrice { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public string Price { get; init; } = string.Empty;

    [JsonPropertyName("orderStatus")]
    public string OrderStatus { get; init; } = string.Empty;

    [JsonPropertyName("createdTime")]
    public string CreatedTime { get; init; } = string.Empty;

    [JsonPropertyName("updatedTime")]
    public string UpdatedTime { get; init; } = string.Empty;
}

internal sealed class BybitOrderAckResult
{
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    [JsonPropertyName("orderLinkId")]
    public string OrderLinkId { get; init; } = string.Empty;
}
