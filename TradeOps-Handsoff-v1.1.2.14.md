# TradeOps handoff — v1.1.2.14

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.14`

Measurement commit:

`252c7e49090efc4365286720fbfe53c2e008481c`

Commit message:

`test: measure signal transition query plans`

Verified optimization commit:

`77a35e83dacf65d03be9d6bc7aa53875961aad41`

Commit message:

`perf: index signal transition event time`

GitHub Actions validation for the verified optimization:

- workflow: `build`
- run number: **111**
- run id: `36898847680`
- job id: `110492616898`
- result: **success**
- unit tests: **90 passed / 90 total**

Successful steps:

- Restore
- Build
- Unit tests, including representative PostgreSQL signal-transition index-plan regression
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.14

The narrow goal from `TradeOps-Handsoff-v1.1.2.13.md` is complete:

**Representative PostgreSQL query-plan validation for signal transition metrics and an evidence-driven event-time index.**

No API semantics were changed in this version.

No trading/execution semantics were changed.

## 3. Measurement fixture added

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsQueryPlanPostgresTests.cs`

The fixture creates an isolated PostgreSQL database, applies real migrations, and loads:

- 40,000 `TradingSignals`;
- 80,000 `TradingSignalOutcomeEvents`;
- eight symbols;
- approximately 28 days of deterministic event-time distribution;
- one `Received` plus one terminal outcome event per signal.

It runs `ANALYZE` before measurement.

It measures the two representative query shapes used by transition-window metrics:

1. unscoped `[from,to)` aggregation over `TradingSignalOutcomeEvents.OccurredAt`;
2. symbol-scoped `[from,to)` aggregation joined through the parent `TradingSignals.Symbol`.

The fixture records:

- repository median latency after warm-up;
- `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`;
- indexes used in the plan;
- whether PostgreSQL performs a sequential scan of `TradingSignalOutcomeEvents`.

These observations are CI-fixture evidence, not a production latency SLO.

GitHub Actions #110 validated the measurement fixture before the optimization was added.

## 4. Index decision

Before v1.1.2.14, signal outcome history had only:

`(TradingSignalId, OccurredAt)`

That index remains useful for per-signal history reads, where `TradingSignalId` is known.

The transition-window endpoint introduced in v1.1.2.13 also supports an unscoped event-time predicate:

`OccurredAt >= from AND OccurredAt < to`

Because `OccurredAt` was only the second key of the existing composite index, there was no selective event-time-leading access path for that query shape.

v1.1.2.14 therefore adds a dedicated physical PostgreSQL index:

`IX_TradingSignalOutcomeEvents_OccurredAt`

The index is intentionally an access-path optimization only. It does not change domain or API semantics.

## 5. Database migration

Added migration:

`20261001172500_AddSignalTransitionOccurredAtIndex`

Up:

- creates `IX_TradingSignalOutcomeEvents_OccurredAt` on `TradingSignalOutcomeEvents(OccurredAt)`.

Down:

- drops the same index.

The migration includes explicit `[DbContext]` and `[Migration]` attributes so it is registered correctly by EF Core.

The runtime smoke in Actions #111 confirms the migration is discovered and applied after `20261001155500_AddTradingSignalOutcomeHistory`.

## 6. Query-plan regression guard

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsIndexPostgresTests.cs`

It uses a representative 40,000-signal / 80,000-event PostgreSQL fixture and verifies:

- the real migration creates `IX_TradingSignalOutcomeEvents_OccurredAt`;
- the selective unscoped transition-window plan uses that index;
- the representative unscoped plan does not sequentially scan `TradingSignalOutcomeEvents`;
- the representative symbol-scoped plan does not sequentially scan `TradingSignalOutcomeEvents`.

This regression test passed in GitHub Actions #111.

## 7. Documentation added

Added:

`docs/SignalTransitionMetricsQueryPlanFixture.md`

It documents:

- representative fixture size and distribution;
- query shapes;
- why the existing `(TradingSignalId, OccurredAt)` index did not cover the unscoped event-time access path;
- the new `OccurredAt` index;
- the plan-regression guarantees;
- the distinction between CI-fixture measurements and production SLOs.

## 8. Existing transition metrics semantics remain unchanged

The v1.1.2.13 endpoint remains:

`GET /api/metrics/signal-transitions/window?from=...&to=...&symbol=...`

It still counts persisted events selected by:

`TradingSignalOutcomeEvents.OccurredAt`

It still returns:

- `receivedTransitions`
- `acceptedTransitions`
- `rejectedTransitions`

with optional parent-signal symbol scoping.

No historical events are reconstructed from `TradingSignals.Outcome`.

No legacy timestamps are fabricated.

## 9. Existing execution metrics remain unchanged

The established execution metrics endpoints still retain signal-creation/current-outcome semantics:

- `GET /api/metrics/execution`
- `GET /api/metrics/execution/window`
- `GET /api/metrics/execution/series`
- `GET /api/metrics/execution/by-symbol`

Do not silently reinterpret them as signal-transition-time metrics.

## 10. Files added in v1.1.2.14

Measurement stage:

- `tests/TradeOps.UnitTests/SignalTransitionMetricsQueryPlanPostgresTests.cs`

Optimization stage:

- `src/TradeOps.Infrastructure/Persistence/Migrations/20261001172500_AddSignalTransitionOccurredAtIndex.cs`
- `tests/TradeOps.UnitTests/SignalTransitionMetricsIndexPostgresTests.cs`
- `docs/SignalTransitionMetricsQueryPlanFixture.md`

No application/API source file was modified by v1.1.2.14.

## 11. Safety and project boundaries preserved

Still in force:

- .NET 8;
- PostgreSQL 16;
- Mock exchange remains the default provider;
- Bybit Testnet remains the only real venue adapter;
- no mainnet / real-money support;
- deterministic `ClientOrderId` remains placement identity;
- no blind order-placement retry after ambiguous outcome;
- no blind cancellation retry after ambiguous outcome;
- no automatic position flattening;
- no strategy / alpha generation;
- no return guarantees;
- persisted order lifecycle semantics unchanged;
- operational-run history/projection semantics unchanged;
- persistent risk-state semantics unchanged;
- recovery/reconciliation semantics unchanged.

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 12. Known limitations after v1.1.2.14

- signal transition history remains incomplete for pre-v1.1.2.12 legacy signals because there is no synthetic backfill;
- transition metrics expose only a window aggregate, not a bucketed transition series;
- no transition by-symbol ranking endpoint;
- no `symbol x bucket` transition matrix;
- CI fixture measurements are not a formal production latency SLO;
- no materialized metrics rollups;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboard;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 13. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.15`

Recommended narrow scope:

**Bounded bucketed signal-transition series, kept separate from existing execution-series semantics.**

A suitable API is:

`GET /api/metrics/signal-transitions/series?from=...&to=...&bucket=...&symbol=...`

Recommended rules:

- use only `TradingSignalOutcomeEvents.OccurredAt` as the time axis;
- preserve `[from,to)` semantics;
- support the same explicit bucket set already used by execution series (`1m`, `5m`, `15m`, `1h`, `1d`) unless there is a concrete reason not to;
- enforce a hard maximum bucket count, preferably the established 500-bucket limit;
- emit empty buckets so the time series is structurally stable;
- optional symbol filtering must resolve through parent `TradingSignals.Symbol`;
- do not mix `TradingSignals.CreatedAt` into transition buckets;
- explicitly retain the pre-v1.1.2.12 historical coverage warning;
- use PostgreSQL aggregation rather than materializing raw transition events in application memory;
- validate real PostgreSQL behavior and boundary buckets;
- do not combine Prometheus/Grafana/export work into the same version.

## 14. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.14.md`

Continue from branch:

`TradeOps/v_1.1.2.14`

Verified optimization SHA:

`77a35e83dacf65d03be9d6bc7aa53875961aad41`

GitHub Actions #111 (`36898847680`) is fully green with 90/90 tests passing.

Do not change existing execution-metrics semantics.

Do not fabricate legacy transition history.

Preserve the new event-time index and its PostgreSQL plan regression guard.
