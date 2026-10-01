namespace TradeOps.Application.Models;

public sealed record SignalTransitionMetricsWindowSnapshot(
    DateTimeOffset GeneratedAt,
    DateTimeOffset FromInclusive,
    DateTimeOffset ToExclusive,
    string? Symbol,
    long ReceivedTransitions,
    long AcceptedTransitions,
    long RejectedTransitions);
