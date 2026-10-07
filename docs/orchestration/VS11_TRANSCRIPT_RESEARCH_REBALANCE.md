# VS-11 — Transcript Research-to-Backtest/Rebalance Composition

## State

READY

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs11-transcript-research-rebalance-demo
```

## Baseline

```text
a763b4eda2906cdac5c551869f14005013a4b9ab
```

This branch must remain application-only and independent from the active VS-10 provider/process harness.

## Purpose

Compose the already integrated transcript-research path with the already integrated backtest/rebalance path without creating new domain contracts.

Target:

```text
prior normalized transcript JSON
+ prior structured earnings JSON
+ current normalized transcript JSON
+ current structured earnings JSON
+ existing EarningsResearchPolicyDefinition
+ historical daily MarketDataBar set
+ current PortfolioSnapshot
+ current reference price

-> existing TranscriptResearchDecisionDemoService
-> prior/current existing EarningsEvent
-> existing transcript-derived EarningsAssessmentResult
-> existing transcript-derived ResearchDecision

-> existing ResearchToRebalanceDemoService
-> existing EventDrivenBacktester
-> existing PortfolioRebalancePlanner
-> existing BacktestPerformanceMetrics
-> existing RebalancePlan
-> existing RebalanceOrderIntent
```

VS-11 is an application composition slice.

It does not call OpenAI, DocFlow, external processes, HTTP, brokers, or CLI tools.

## Architecture rule

Do not introduce an alternative:

- transcript adapter;
- structured earnings adapter;
- EarningsEvent;
- earnings assessment rule;
- ResearchDecision;
- backtest engine;
- portfolio snapshot;
- rebalance planner;
- RebalancePlan;
- RebalanceOrderIntent.

Reuse integrated production/demo boundaries exactly.

## Files owned by this slice

Recommended implementation files only:

```text
src/TradeOps.Application/Services/TranscriptResearchToRebalanceDemoService.cs
tests/TradeOps.UnitTests/TranscriptResearchToRebalanceDemoTests.cs
docs/orchestration/VS11_TRANSCRIPT_RESEARCH_REBALANCE.md
```

Do not modify:

```text
TradeOps.sln
tests/TradeOps.UnitTests/TradeOps.UnitTests.csproj
tools/TradeOps.ProviderTranscriptResearchDemo/**
tools/TradeOps.TranscriptResearchDemo/**
```

unless Development Orchestrator explicitly changes ownership.

This avoids overlap with active VS-10.

## Existing components to reuse unchanged

Required:

- `TranscriptResearchDecisionDemoService`
- `TranscriptResearchDecisionDemoRequest`
- `TranscriptResearchDecisionDemoResult`
- `ResearchToRebalanceDemoService`
- `ResearchToRebalanceDemoRequest`
- `ResearchToRebalanceDemoResult`
- `EarningsResearchPolicyConfiguration.ToDecisionRuleSettings`
- `EarningsResearchPolicyConfiguration.ToTargetWeightPolicy`
- `EarningsResearchPolicyConfiguration.ComputeFingerprint`
- `ResearchDecisionTimingMode.ObservedRetrieval`
- `EventDrivenBacktester` indirectly through existing VS-01 service
- `PortfolioRebalancePlanner` indirectly through existing VS-01 service.

Do not call the backtester or planner directly from the new VS-11 composition service if the existing `ResearchToRebalanceDemoService` can own that path.

## Recommended request

A narrow additive demo request is appropriate.

Recommended shape:

```csharp
public sealed record TranscriptResearchToRebalanceDemoRequest(
    TranscriptResearchDecisionDemoRequest TranscriptResearch,
    IReadOnlyList<MarketDataBar> HistoricalDailyMarketBars,
    decimal InitialCash,
    PortfolioSnapshot CurrentPortfolio,
    decimal CurrentReferencePrice,
    BacktestExecutionCosts? ExecutionCosts = null,
    RebalanceConstraints? BacktestConstraints = null,
    RebalanceConstraints? CurrentRebalanceConstraints = null);
```

The exact type name may vary if a clearer bounded name is justified.

Do not add duplicate policy thresholds/weights to this request.

The policy is already owned by `TranscriptResearchDecisionDemoRequest.Policy`.

Do not add timestamps generated from a clock.

## Recommended result

Keep the result audit-oriented and avoid duplicating all fields.

Recommended:

```csharp
public sealed record TranscriptResearchToRebalanceDemoResult(
    TranscriptResearchDecisionDemoResult TranscriptResearch,
    ResearchToRebalanceDemoResult ResearchToRebalance);
```

The exact name may vary.

Do not create a second flattened backtest/rebalance contract.

Existing nested results are the authoritative data.

## Composition algorithm

1. Validate request/non-null inputs.
2. Call the existing:
   ```text
   TranscriptResearchDecisionDemoService.Run(...)
   ```
3. Use exactly:
   ```text
   transcriptResult.PriorEvent
   transcriptResult.CurrentEvent
   ```
   as the historical earnings-event input to the existing VS-01 service.
4. Map the same existing policy through:
   ```text
   EarningsResearchPolicyConfiguration.ToDecisionRuleSettings
   EarningsResearchPolicyConfiguration.ToTargetWeightPolicy
   ```
5. Call:
   ```text
   ResearchToRebalanceDemoService.Run(...)
   ```
   with:
   - historical events = prior + current, in that order;
   - caller historical daily market bars;
   - caller initial cash;
   - caller current portfolio;
   - caller current reference price;
   - caller execution costs/constraints;
   - strategy ID = exact policy StrategyId;
   - timing mode = explicitly `ObservedRetrieval`.
6. Compare the existing outputs from the two integrated paths.

## Mandatory equivalence gate

The transcript path and VS-01 path must produce semantically identical latest research results for the same prior/current events and policy.

At minimum require exact equality of:

```text
TranscriptResearch.Assessment
ResearchToRebalance.LatestAssessment
```

and:

```text
TranscriptResearch.Decision
ResearchToRebalance.LatestDecision
```

If equality does not hold, fail closed with a clear exception.

Do not choose one result and silently ignore disagreement.

This equivalence gate is central to VS-11: it proves that transcript-originated earnings events enter the existing downstream client path without changing research semantics.

## GeneratedAt semantics

Do not calculate a new timestamp.

The transcript service already uses:

```text
max(CurrentEvent.PublishedAt, CurrentEvent.Provenance.RetrievedAt)
```

The existing VS-01 service must be invoked with:

```text
ResearchDecisionTimingMode.ObservedRetrieval
```

The equivalence gate must therefore confirm the same exact `GeneratedAt`.

No clock access.

## Historical-event scope

VS-11 bounded v1 uses exactly the two events produced by the transcript research request:

```text
prior
current
```

This yields one comparable research decision for the historical replay.

Do not add older earnings acquisition or a multi-quarter transcript store in this slice.

The caller provides market bars sufficient for the existing backtester.

## Market data

VS-11 does not fetch market data.

Input is existing:

```text
IReadOnlyList<MarketDataBar>
```

All existing WS-04 rules remain authoritative:

- stocks only;
- daily bars;
- causal execution timing;
- no look-ahead;
- existing execution-price policy;
- existing commission/slippage semantics;
- existing instrument identity.

Do not loosen any WS-04 validation.

## Current rebalance

Use the caller-provided existing:

```text
PortfolioSnapshot
current reference price
optional CurrentRebalanceConstraints
```

The resulting:

```text
ResearchToRebalance.CurrentRebalancePlan
```

must be the existing WS-03 result.

Do not recalculate target/current/delta quantity in VS-11.

Do not convert `RebalanceOrderIntent` to an executable broker order.

## Policy

The exact `EarningsResearchPolicyDefinition` from the transcript request is reused downstream.

Require:

```text
transcriptResult.PolicyFingerprint
==
EarningsResearchPolicyConfiguration.ComputeFingerprint(request.TranscriptResearch.Policy)
```

This should naturally be true, but verify/preserve it for audit.

No new policy format.

## Determinism

The same request must produce the same:

- transcript assessment;
- transcript decision;
- backtest result;
- backtest metrics;
- final portfolio;
- current rebalance plan.

No time/random/network state.

## Tests

Use existing integrated transcript-research synthetic artifacts from:

```text
samples/research/transcript-research/
```

where practical.

Do not add another copy of the normalized/structured transcript fixtures.

Tests may build deterministic market bars/current portfolio in code.

At minimum cover:

1. valid transcript artifacts pass through existing `TranscriptResearchDecisionDemoService`;
2. exact prior/current EarningsEvent identities are preserved;
3. same existing policy is mapped downstream;
4. policy fingerprint is preserved;
5. downstream call uses exactly prior/current transcript-derived events;
6. explicit `ObservedRetrieval` timing semantics;
7. transcript and VS-01 assessment equality;
8. transcript and VS-01 ResearchDecision equality;
9. exact GeneratedAt equality;
10. existing strategy ID preserved;
11. existing target weight preserved;
12. existing `ResearchToRebalanceDemoService` produces backtest output;
13. backtest has one replayed comparable event for the two-event input;
14. existing `BacktestPerformanceMetrics` is exposed unchanged;
15. existing current `RebalancePlan` is exposed unchanged;
16. existing `RebalanceOrderIntent`, when present, is exposed unchanged;
17. deterministic repeated run equality;
18. invalid normalized transcript still fails through existing VS-06 path;
19. invalid structured extraction still fails through existing VS-07 path;
20. mismatched transcript instruments still fail through existing VS-08 path;
21. invalid policy still fails through existing configuration/VS-08 semantics;
22. insufficient/invalid market data fails through existing VS-01/WS-04 semantics;
23. invalid current portfolio/reference price fails through existing VS-01/WS-03 semantics;
24. no clock/network/process/provider code is added;
25. no direct `EventDrivenBacktester` or `PortfolioRebalancePlanner` duplicate orchestration when existing VS-01 service is sufficient;
26. frozen shared contracts are unchanged;
27. existing VS-01 tests remain green;
28. existing VS-08 tests remain green;
29. existing VS-09 tests remain green;
30. full TradeOps regression suite remains green.

## No project wiring changes

The implementation belongs to the already referenced `TradeOps.Application` project.

The test file belongs to the already existing `TradeOps.UnitTests` project.

Therefore this slice should not need:

- a new csproj;
- a ProjectReference;
- a solution entry;
- package dependencies.

If implementation unexpectedly requires one of those, stop and document the blocker instead of overlapping VS-10-owned wiring.

## Explicitly out of scope

Do not implement:

- OpenAI/provider calls;
- DocFlow process invocation;
- provider demo harness;
- transcript download/scraping;
- SEC fetch;
- new market-data fetch;
- new CLI/tool;
- new JSON manifest;
- new policy schema;
- new research rule;
- new backtest engine;
- new rebalance planner;
- broker execution;
- IBKR mutation;
- persistence/database;
- web API;
- multi-asset backtesting.

VS-11 stops at existing backtest metrics + current `RebalancePlan/RebalanceOrderIntent`.

## Privacy

Use only existing synthetic sample artifacts and generic synthetic market/portfolio values.

Before handoff scan every changed file against the orchestration privacy rule.

Do not mention prohibited personal/client names even in statements asserting absence.

## Completion protocol

Before handoff update this file with:

- State;
- Current HEAD;
- changed files;
- public/shared contracts changed or not;
- composition semantics;
- equivalence-gate result;
- tests;
- exact CI run + conclusion;
- privacy scan;
- blockers;
- next integration action.

Stop after this bounded slice.

Do not merge independently.
