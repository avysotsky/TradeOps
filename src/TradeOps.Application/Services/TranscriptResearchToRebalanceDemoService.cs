using TradeOps.Application.Models;
using TradeOps.Application.Services.Backtesting;

namespace TradeOps.Application.Services;

public sealed record TranscriptResearchToRebalanceDemoRequest(
    TranscriptResearchDecisionDemoRequest TranscriptResearch,
    IReadOnlyList<MarketDataBar> HistoricalDailyMarketBars,
    decimal InitialCash,
    PortfolioSnapshot CurrentPortfolio,
    decimal CurrentReferencePrice,
    BacktestExecutionCosts? ExecutionCosts = null,
    RebalanceConstraints? BacktestConstraints = null,
    RebalanceConstraints? CurrentRebalanceConstraints = null);

public sealed record TranscriptResearchToRebalanceDemoResult(
    TranscriptResearchDecisionDemoResult TranscriptResearch,
    ResearchToRebalanceDemoResult ResearchToRebalance);

public sealed class TranscriptResearchToRebalanceDemoService
{
    private readonly TranscriptResearchDecisionDemoService
        _transcriptResearchService;
    private readonly ResearchToRebalanceDemoService
        _researchToRebalanceService;

    public TranscriptResearchToRebalanceDemoService(
        TranscriptResearchDecisionDemoService?
            transcriptResearchService = null,
        ResearchToRebalanceDemoService?
            researchToRebalanceService = null)
    {
        _transcriptResearchService =
            transcriptResearchService ??
            new TranscriptResearchDecisionDemoService();
        _researchToRebalanceService =
            researchToRebalanceService ??
            new ResearchToRebalanceDemoService();
    }

    public TranscriptResearchToRebalanceDemoResult Run(
        TranscriptResearchToRebalanceDemoRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(
            request.TranscriptResearch);
        ArgumentNullException.ThrowIfNull(
            request.HistoricalDailyMarketBars);
        ArgumentNullException.ThrowIfNull(
            request.CurrentPortfolio);

        var transcriptResult =
            _transcriptResearchService.Run(
                request.TranscriptResearch);

        var expectedPolicyFingerprint =
            EarningsResearchPolicyConfiguration
                .ComputeFingerprint(
                    request.TranscriptResearch.Policy);

        if (!string.Equals(
                transcriptResult.PolicyFingerprint,
                expectedPolicyFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Transcript research policy fingerprint diverged from the configured earnings research policy.");
        }

        var ruleSettings =
            EarningsResearchPolicyConfiguration
                .ToDecisionRuleSettings(
                    request.TranscriptResearch.Policy);
        var targetWeightPolicy =
            EarningsResearchPolicyConfiguration
                .ToTargetWeightPolicy(
                    request.TranscriptResearch.Policy);

        var researchToRebalance =
            _researchToRebalanceService.Run(
                new ResearchToRebalanceDemoRequest(
                    HistoricalEarningsEvents:
                        new[]
                        {
                            transcriptResult.PriorEvent,
                            transcriptResult.CurrentEvent
                        },
                    HistoricalDailyMarketBars:
                        request.HistoricalDailyMarketBars,
                    RuleSettings:
                        ruleSettings,
                    TargetWeightPolicy:
                        targetWeightPolicy,
                    InitialCash:
                        request.InitialCash,
                    CurrentPortfolio:
                        request.CurrentPortfolio,
                    CurrentReferencePrice:
                        request.CurrentReferencePrice,
                    ExecutionCosts:
                        request.ExecutionCosts,
                    BacktestConstraints:
                        request.BacktestConstraints,
                    CurrentRebalanceConstraints:
                        request.CurrentRebalanceConstraints,
                    StrategyId:
                        request.TranscriptResearch
                            .Policy
                            .StrategyId,
                    DecisionTimingMode:
                        ResearchDecisionTimingMode
                            .ObservedRetrieval));

        if (!Equals(
                transcriptResult.Assessment,
                researchToRebalance.LatestAssessment))
        {
            throw new InvalidOperationException(
                "Transcript research assessment diverged from the existing research-to-rebalance assessment.");
        }

        if (!Equals(
                transcriptResult.Decision,
                researchToRebalance.LatestDecision))
        {
            throw new InvalidOperationException(
                "Transcript research decision diverged from the existing research-to-rebalance decision.");
        }

        return new TranscriptResearchToRebalanceDemoResult(
            transcriptResult,
            researchToRebalance);
    }
}
