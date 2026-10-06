# WS-04 — Event-Driven Backtester

State: READY_FOR_INTEGRATION

Repository:

```text
avysotsky/TradeOps
```

Branch:

```text
TradeOps/ws04-backtester-contracts
```

Synchronized baseline:

```text
main @ fc322ef7fd946f710ec64f6b2f834d2ea855f795
```

Validated implementation HEAD:

```text
364db39fde1064827298d0232bb2481819a7ed09
```

## Goal

Evaluate long-horizon earnings/research decisions with event-time correctness and the same portfolio decision semantics used by production TradeOps.

## Integrated dependencies reused

WS-04 reuses the existing production contracts from main and does not define backtest-only substitutes.

From WS-02 / PR #51:

```text
EarningsEvent
EarningsSnapshot
ResearchSourceProvenance
PublishedAt semantics
ResearchDecision(Action = SetTargetWeight)
```

From WS-03 / PR #49:

```text
PortfolioSnapshot
RebalanceConstraints
PortfolioRebalancePlanner
RebalancePlan
RebalanceOrderIntent
```

Shared/frozen production contracts changed by WS-04:

```text
none
```

## Completed bounded slice

The first deterministic event-driven replay slice is implemented.

Added:

- deterministic `DeterministicEventClock`;
- broker-neutral `MarketDataBar`;
- `IBacktestExecutionPricePolicy` plus conservative `NextBarOpenExecutionPricePolicy`;
- historical `PortfolioSnapshot` construction from simulated cash/position state;
- replay through the real production `PortfolioRebalancePlanner`;
- production `ResearchDecision(SetTargetWeight) -> RebalancePlan -> RebalanceOrderIntent` path;
- configurable per-order commissions and basis-point slippage;
- simulated fills without any IBKR dependency;
- equity curve and final historical portfolio snapshot;
- basic metrics: event count, total return, CAGR, max drawdown, Sharpe, Sortino, turnover and average exposure;
- deterministic fixtures/tests.

The bounded v1 replay intentionally supports one instrument per run. Multi-asset mark-to-market requires an explicit synchronized historical price matrix and is not approximated in this slice.

## Event-time / look-ahead invariants enforced

WS-04 fails closed when the WS-02 causal boundary is violated:

```text
ResearchSourceProvenance.SourceTimestamp <= EarningsEvent.PublishedAt
EarningsEvent.PublishedAt <= ResearchSourceProvenance.RetrievedAt
ResearchDecision.GeneratedAt >= EarningsEvent.PublishedAt
```

If `ResearchDecision.SourceEventId` is present, it must match the replayed `EarningsEvent.EventId`.

Execution eligibility is additionally constrained by:

```text
execution bar OpenTime >= max(EarningsEvent.PublishedAt, ResearchDecision.GeneratedAt)
```

Therefore an after-hours event cannot execute against an earlier regular-session close.

The actual production `PortfolioRebalancePlanner` still performs its own downstream check that `ResearchDecision.GeneratedAt <= PortfolioSnapshot.AsOf`.

## Execution-cost semantics

The production planner receives the historical bar open as its reference price and produces the real `RebalanceOrderIntent`.

The simulator then applies configured slippage and commission to that planner-sized intent. It does not silently resize an unaffordable buy after execution costs; such a case fails explicitly in the bounded slice.

No broker API, IBKR session, LLM call, SEC HTTP fetch, transcript ingestion or profitability claim exists inside the backtester.

## Tests

New WS-04 tests:

```text
7 passed
```

Covered:

- deterministic clock ordering/deduplication;
- rejection of decision generation before PublishedAt;
- after-hours release cannot fill at an earlier close;
- snapshot/rebalance waits until GeneratedAt is available;
- configurable commission/slippage;
- deterministic replay output;
- both sides of SourceTimestamp <= PublishedAt <= RetrievedAt provenance enforcement.

Full solution unit test result:

```text
Total tests: 334
Passed: 334
Failed: 0
```

## CI

GitHub Actions:

```text
workflow: build
run number: 615
run id: 37517372936
commit: 364db39fde1064827298d0232bb2481819a7ed09
conclusion: success
```

Validated stages:

```text
restore
build
unit tests
API + PostgreSQL smoke test
signed webhook end-to-end demo
customer TradingView demo
client pilot starter-kit validation
Docker Compose validation
TradingView gateway deployment validation
Docker image build
```

Run:

https://github.com/avysotsky/TradeOps/actions/runs/37517372936

## Known next-slice work

Not blockers for integration of this bounded slice:

- multi-asset synchronized mark-to-market;
- explicit delisting/survivorship model for broad-universe tests;
- signal-quality metrics such as hit rate and 1/5/20/60/250 trading-day forward returns;
- richer commission schedules and liquidity/volume-aware execution;
- corporate-action handling.

No profitability inference should be drawn from backtest output.

## Worker handoff

```text
State: READY_FOR_INTEGRATION
Validated implementation HEAD: 364db39fde1064827298d0232bb2481819a7ed09
Completed: first deterministic single-instrument event-driven replay slice
Shared contracts changed: none
Tests: 334/334 passed; WS-04 7/7 passed
CI: build #615 / run 37517372936 / SUCCESS
Blockers: none for integration of this bounded slice
Next integration action: Development Orchestrator reviews branch diff against current main and decides PR/integration.
```

This status-file commit is metadata-only and follows the CI-validated implementation HEAD above.
