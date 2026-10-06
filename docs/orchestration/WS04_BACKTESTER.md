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

Validated correctness-fix implementation HEAD:

```text
04a46215c8dab08af858ee17fc43c911b7639e27
```

## Goal

Evaluate long-horizon earnings/research decisions with event-time correctness and the same portfolio decision semantics used by production TradeOps.

## Integrated production contracts reused

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

Frozen/shared production contracts changed by WS-04:

```text
none
```

WS-04 does not modify:

```text
EarningsEvent
ResearchDecision
InstrumentReference
PortfolioSnapshot
RebalancePlan
RebalanceOrderIntent
PortfolioRebalancePlanner
```

## Completed bounded slice

The deterministic single-instrument event-driven replay slice includes:

- deterministic `DeterministicEventClock`;
- broker-neutral `MarketDataBar`;
- explicit `MarketDataBarPeriod`;
- `IBacktestExecutionPricePolicy` plus conservative `NextBarOpenExecutionPricePolicy`;
- historical `PortfolioSnapshot` construction from simulated cash/position state;
- replay through the real production `PortfolioRebalancePlanner`;
- production `ResearchDecision(SetTargetWeight) -> RebalancePlan -> RebalanceOrderIntent` path;
- configurable per-order commissions and basis-point slippage;
- simulated fills without broker dependencies;
- equity curve and final historical portfolio snapshot;
- basic metrics: event count, total return, CAGR, max drawdown, Sharpe, Sortino, turnover and average exposure;
- deterministic fixtures/tests.

The bounded v1 replay intentionally supports one instrument per run.

## Integration-review correctness fixes

### 1. Instrument identity aligned with production planner

WS-04 now uses one internal identity helper with the same v1 semantics as `PortfolioRebalancePlanner`:

```text
AssetClass must match

if both InstrumentReference values have VenueInstrumentId:
    identity = VenueInstrumentId equality
else:
    identity = Symbol + Currency fallback
```

Consequences covered by tests:

- same symbol/currency but different non-empty VenueInstrumentId values are different instruments;
- same VenueInstrumentId is the same instrument even when symbol text differs;
- when VenueInstrumentId is absent, symbol/currency fallback continues to work.

No change was made to frozen `InstrumentReference v1` or WS-03 contracts.

### 2. Daily stock bar frequency is explicit

WS-04 bounded v1 accepts:

```text
AssetClass.Stock
MarketDataBarPeriod.Daily
one market bar per UTC trading date
bar OpenTime and CloseTime on the same UTC trading date
```

It fails closed for:

- non-stock market bars;
- `Intraday`, `Weekly`, `Unknown` or any non-`Daily` period;
- more than one market bar for one trading date in the single-instrument v1 replay;
- a daily bar whose open/close timestamps cross the UTC trading-date boundary.

WS-04 does not attempt universal intraday/weekly annualization in this slice.

## Performance metric frequency contract

`BacktestPerformanceCalculator` v1 uses:

```text
252 daily trading observations per year
```

Therefore:

```text
Sharpe v1 annualization = sqrt(252)
Sortino v1 annualization = sqrt(252)
```

This assumption is valid only because the replay boundary now accepts daily stock bars only.

CAGR remains calendar-time based.

## Event-time / look-ahead invariants preserved

WS-04 continues to fail closed when:

```text
ResearchSourceProvenance.SourceTimestamp > EarningsEvent.PublishedAt
EarningsEvent.PublishedAt > ResearchSourceProvenance.RetrievedAt
ResearchDecision.GeneratedAt < EarningsEvent.PublishedAt
```

Required causal boundary remains:

```text
SourceTimestamp <= PublishedAt <= RetrievedAt
Decision.GeneratedAt >= PublishedAt
execution bar OpenTime >= max(PublishedAt, GeneratedAt)
```

If `ResearchDecision.SourceEventId` is present, it must match the replayed `EarningsEvent.EventId`.

An after-hours event therefore cannot execute against an earlier regular-session close.

The production `PortfolioRebalancePlanner` still independently enforces:

```text
ResearchDecision.GeneratedAt <= PortfolioSnapshot.AsOf
```

## Execution-cost semantics

The production planner receives the historical daily bar open as its reference price and produces the real `RebalanceOrderIntent`.

The simulator then applies configured slippage and commission to that planner-sized intent. It does not silently resize an unaffordable buy after execution costs; that condition fails explicitly.

No IBKR API/session, LLM call, SEC HTTP fetch, transcript ingestion or profitability claim exists inside the backtester.

## Tests

WS-04 tests:

```text
12 passed
```

Coverage includes:

- deterministic clock ordering/deduplication;
- rejection of decision generation before PublishedAt;
- after-hours release cannot fill at an earlier close;
- snapshot/rebalance waits until GeneratedAt is available;
- configurable commission/slippage;
- deterministic replay output;
- both sides of SourceTimestamp <= PublishedAt <= RetrievedAt;
- different VenueInstrumentId values override identical symbol/currency identity;
- same VenueInstrumentId is treated as the same instrument;
- symbol/currency fallback without VenueInstrumentId;
- rejection of non-daily MarketDataBar period;
- rejection of duplicate daily bars for one trading date.

Full solution:

```text
Total tests: 339
Passed: 339
Failed: 0
```

## CI

GitHub Actions full workflow:

```text
workflow: build
run number: 617
run id: 37519276349
commit: 04a46215c8dab08af858ee17fc43c911b7639e27
conclusion: SUCCESS
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

https://github.com/avysotsky/TradeOps/actions/runs/37519276349

## Known next-slice work

Not blockers for integration of this bounded slice:

- multi-asset synchronized mark-to-market;
- explicit delisting/survivorship model for broad-universe tests;
- signal-quality metrics such as hit rate and 1/5/20/60/250 trading-day forward returns;
- richer commission schedules and liquidity/volume-aware execution;
- corporate-action handling;
- any future frequency-aware intraday/weekly annualization model.

No profitability inference should be drawn from backtest output.

## Worker handoff

```text
State: READY_FOR_INTEGRATION
Validated implementation HEAD: 04a46215c8dab08af858ee17fc43c911b7639e27
Completed: bounded WS-04 slice + integration-review identity/frequency correctness fixes
Shared contracts changed: none
Tests: 339/339 passed; WS-04 12/12 passed
CI: build #617 / run 37519276349 / SUCCESS
Blockers: none for integration of this bounded slice
Next integration action: Development Orchestrator reviews the final branch diff and may proceed with PR/integration.
```

This status-file update is metadata-only and follows the CI-validated implementation HEAD above.
