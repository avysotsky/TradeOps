namespace TradeOps.Application.Models;

public sealed record RiskControlSnapshot(
    bool TradingEnabled,
    bool EmergencyStop,
    string? EmergencyStopReason,
    decimal DailyRealizedPnL,
    int ActivePositionMismatchCount,
    DateTimeOffset UpdatedAt)
{
    public bool HasPositionMismatch => ActivePositionMismatchCount > 0;
}
