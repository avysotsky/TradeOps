# VS-01 — Research-to-Rebalance Client Demo

State: INTEGRATED

Repository / branch:

```text
avysotsky/TradeOps
TradeOps/vs01-research-to-rebalance-demo
```

Baseline:

```text
main @ 435dd51b70f4a48ab702c5374dedea0c13c4174e
```

Validated implementation HEAD:

```text
d923568d440affaaa5c49c7f11af39ba66d8be5e
```

## Goal

Assemble the first client-ready, reproducible research-to-rebalance vertical slice from the already integrated WS-02, WS-03 and WS-04 production components.

The slice demonstrates infrastructure and deterministic policy translation. It does not claim that the sample research rule is profitable or production-optimal.

## Demo entry point

The bounded slice adds one application-layer entry point:

```text
ResearchToRebalanceDemoService.Run(ResearchToRebalanceDemoRequest)
```

The request accepts:

```text
historical EarningsEvent sequence
historical daily MarketDataBar sequence
explicit EarningsDecisionRuleSettings
explicit EarningsTargetWeightPolicy
initial backtest cash
current PortfolioSnapshot
current reference price
optional existing backtest execution costs / rebalance constraints
```

No CLI or API wrapper was added because the application service is already a direct, minimal entry point and adding another transport layer is not required for this bounded slice.

## Production pipeline reused

Historical path:

```text
EarningsEvent + prior comparable EarningsEvent
-> DeterministicEarningsDecisionRule.Assess
-> EarningsAssessmentResult
-> DeterministicEarningsDecisionRule.Evaluate
-> ResearchDecision(Action = SetTargetWeight)
-> BacktestReplayItem
-> EventDrivenBacktester
-> production PortfolioRebalancePlanner inside the backtester
-> RebalancePlan / RebalanceOrderIntent
-> BacktestPerformanceMetrics
```

Current portfolio path for the latest event:

```text
latest ResearchDecision(Action = SetTargetWeight)
+ current PortfolioSnapshot
+ current reference price
-> PortfolioRebalancePlanner
-> RebalancePlan
-> RebalanceOrderIntent
```

The demo does not introduce a second research decision, portfolio, rebalance or backtest contract.

## Event-time behavior

For each historical event after the first prior comparable, the demo produces the research decision at:

```text
max(EarningsEvent.PublishedAt, ResearchSourceProvenance.RetrievedAt)
```

This keeps the demo deterministic while avoiding generation before the fixture says the research data was acquired.

The existing WS-02 / WS-04 availability and anti-look-ahead validations remain authoritative and unchanged.

## Result surface

`ResearchToRebalanceDemoResult` exposes the requested client-facing fields:

```text
Instrument
EventId
FiscalPeriod
PublishedAt
Assessment
TargetWeight
BacktestPeriodStart / BacktestPeriodEnd
EventCount
TotalReturn
Cagr
MaxDrawdown
Sharpe
Sortino
Turnover
FinalEquity
CurrentPortfolioWeight
CurrentPositionQuantity
ProposedTargetWeight
RebalanceStatus
RebalanceSide
RebalanceQuantity
RebalanceNotional
```

It also retains the underlying existing production outputs for auditability:

```text
LatestAssessment
LatestDecision
Backtest
CurrentRebalancePlan
```

## Deterministic fixture

The end-to-end test uses a neutral synthetic stock fixture:

```text
symbol: SAMP
currency: USD
venue instrument id: sample-stock-1
4 earnings events -> 3 comparable research decisions
explicit rule thresholds
explicit target-weight policy:
  Positive -> 40%
  Neutral  -> 20%
  Negative -> 0%
daily historical bars
initial backtest equity: 10,000 USD
current portfolio: 10 shares at 100 USD reference price
current NAV: 10,000 USD
```

The latest synthetic event assesses as Positive under the explicitly supplied sample policy.

For the fixture only, the resulting current rebalance is:

```text
current weight: 10%
proposed target weight: 40%
side: Buy
quantity: 30
estimated notional: 3,000 USD
```

The historical synthetic replay ends at 10,360 USD from 10,000 USD initial equity. This is test-fixture evidence that the pipeline is wired and deterministic; it is not a profitability claim and is not evidence of future returns.

## Shared contracts changed

None.

The following integrated/frozen contracts were reused without modification:

```text
EarningsEvent
EarningsSnapshot
ResearchSourceProvenance
DeterministicEarningsDecisionRule
EarningsTargetWeightPolicy
ResearchDecision
PortfolioSnapshot
PortfolioRebalancePlanner
RebalancePlan
RebalanceOrderIntent
MarketDataBar
EventDrivenBacktester
BacktestPerformanceMetrics
```

No WS-02, WS-03 or WS-04 owned production contract file was changed.

## Scope deliberately excluded

This slice does not add:

```text
LLM research
transcript ingestion
live SEC fetching
IBKR order placement or mutation
execution bridge changes
generic/multi-asset backtester expansion
new alpha or profitability claims
```

## Tests

VS-01 adds:

```text
ResearchToRebalanceDemoTests.Run_end_to_end_demo_reuses_research_backtest_and_rebalance_pipeline
ResearchToRebalanceDemoTests.Run_same_fixture_is_deterministic
```

Validated full solution result:

```text
Total tests: 341
Passed: 341
Failed: 0
VS-01 tests: 2/2 passed
```

The end-to-end test proves:

```text
EarningsEvent
-> EarningsAssessment
-> ResearchDecision(SetTargetWeight)
-> EventDrivenBacktester
-> historical RebalancePlan / RebalanceOrderIntent
-> BacktestPerformanceMetrics
-> latest ResearchDecision(SetTargetWeight)
-> current PortfolioSnapshot
-> current RebalancePlan
-> current RebalanceOrderIntent
```

## CI

Validated implementation:

```text
HEAD: d923568d440affaaa5c49c7f11af39ba66d8be5e
workflow: build
run number: 633
run id: 37522554603
conclusion: SUCCESS
```

Validated stages:

```text
restore
build
341/341 unit tests
API + PostgreSQL smoke test
signed webhook end-to-end demo
customer TradingView demo
client pilot starter-kit validation
Docker Compose validation
TradingView gateway deployment validation
Docker image build
```

No nullable warning remains from the VS-01 service on the validated implementation HEAD.

## Changed implementation files

```text
src/TradeOps.Application/Services/ResearchToRebalanceDemoService.cs
tests/TradeOps.UnitTests/ResearchToRebalanceDemoTests.cs
```

This status file is documentation-only and follows the CI-validated implementation HEAD above.

## Stale-branch check

After the validated implementation CI completed, `main` advanced to:

```text
5ae7f0fe3b83c908e3a387f8e81786cf90ecdbed
```

The two new main commits only modify:

```text
docs/orchestration/ORCHESTRATION.md
docs/orchestration/WS01_IBKR.md
```

VS-01 modifies neither file, so no changed-file overlap was found.

At handoff time the worker branch is therefore:

```text
behind current main by 2 commits
no VS-01 file overlap with those main changes
```

Per `WORKSTREAM_PROTOCOL.md`, Development Orchestrator should perform the final sync/rebase/merge decision before integration.

## Blockers

No VS-01 code or contract blocker remains.

A current-main synchronization step is required during integration review because `main` advanced after the assigned baseline; the observed changes are non-overlapping orchestration/WS-01 documentation.

IBKR mutation remains explicitly out of scope and must not be inferred from this demo.

## Next integration action

Development Orchestrator should:

1. synchronize/reconcile the branch with current `main @ 5ae7f0fe3b83c908e3a387f8e81786cf90ecdbed`; the observed two-commit advance has no VS-01 file overlap;
2. verify the diff remains limited to the VS-01 application service, tests and this status file;
3. confirm no frozen WS-02 / WS-03 / WS-04 contract changed;
4. review the deterministic fixture and client-facing result surface;
5. integrate VS-01 if review remains green;
6. only after separate orchestration decide whether the next client-facing slice adds a transport/demo wrapper, real research data fixtures, or an IBKR Paper read-path demonstration.

Do not start IBKR mutation work or generic backtester expansion from VS-01.


## Integration record

Integrated by Development Orchestrator:

```text
PR #54
validated implementation HEAD: d923568d440affaaa5c49c7f11af39ba66d8be5e
synchronized branch HEAD: 3569ffa281bd978bc82874e1ab1370fab09a50e8
exact synchronized-head CI: 37524381672 — success
merge commit: ef1690203929a5c0c4adafd3fd0f33d55b088170
```

The first client-ready research-to-rebalance vertical slice is now part of main. Future slices must extend this integrated path rather than introduce parallel research, portfolio or backtest contracts.
