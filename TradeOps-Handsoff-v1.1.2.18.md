# TradeOps handoff — v1.1.2.18

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.18`

Initial test commit:

`20755346fc05b8f19edcf3c4366ca855454e8c0b`

Commit message:

`test: validate transition aggregation query plans`

Verified corrective/final implementation commit:

`15a77cf5364c3c1949976cbcd0973dc2541ad54f`

Commit message:

`fix: use transition by-symbol snapshot contract`

The corrective commit only changes the new test to use the existing `SignalTransitionMetricsBySymbolSnapshot.Symbols` property. It does not change production behavior.

GitHub Actions validation:

- workflow: `build`
- run number: **117**
- run id: `36904331558`
- job id: `110510962499`
- result: **success**
- tests: **104 passed / 104 total**
- Build: **0 warnings / 0 errors**

Successful steps:

- Restore
- Build
- Unit/integration tests
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.18

The narrow goal from `TradeOps-Handsoff-v1.1.2.17.md` is complete:

**Representative PostgreSQL query-plan validation for transition-series and transition-by-symbol queries.**

No production API contract changed.

No application or infrastructure runtime code changed.

No database schema/index changed.

No migration was added.

## 3. New representative PostgreSQL test

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsAggregationQueryPlanPostgresTests.cs`

Test:

`RepresentativeVolume_UsesSelectiveAccessPathsForSeriesAndBySymbol`

The test:

1. creates an isolated PostgreSQL database;
2. applies the complete real EF Core migration chain;
3. seeds **40,000** `TradingSignals`;
4. seeds **80,000** persisted `TradingSignalOutcomeEvents`;
5. distributes data deterministically across approximately 28 days and 8 symbols;
6. runs `ANALYZE` on both tables;
7. warms the actual `EfSignalTransitionMetricsRepository` methods;
8. records five-call median repository latency observations;
9. runs `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` against SQL matching the production aggregation shapes;
10. enforces selective transition-event access-path regression guards;
11. emits a Markdown plan report to GitHub Actions step summary/workspace when running in CI;
12. drops the isolated database.

The recorded latency values are CI-fixture observations only. They are not a production SLO and the test does not use latency thresholds.

## 4. Query shapes validated

The representative fixture validates three aggregation plans over a selective 1-hour event-time window:

- unscoped transition series, 15-minute buckets;
- BTCUSDT-scoped transition series, 15-minute buckets;
- transition by-symbol aggregation/ranking, limit 20.

All use the persisted event-time axis:

`TradingSignalOutcomeEvents.OccurredAt`

The SQL shapes match the current production repository behavior, including:

- half-open `[from, to)` bounds;
- `date_bin` bucket aggregation for series;
- parent `TradingSignals` join for symbol filtering/grouping;
- by-symbol ordering `total_transitions DESC, symbol ASC`;
- bounded by-symbol fetch behavior.

## 5. Query-plan result

GitHub Actions #117 passed the new plan-regression test.

Therefore, on the representative **80,000-event** fixture and selective 1-hour window:

- unscoped transition series does **not** use a sequential scan on `TradingSignalOutcomeEvents`;
- symbol-scoped transition series does **not** use a sequential scan on `TradingSignalOutcomeEvents`;
- transition by-symbol does **not** use a sequential scan on `TradingSignalOutcomeEvents`.

The regression test additionally requires the existing index:

`IX_TradingSignalOutcomeEvents_OccurredAt`

in the plans for:

- unscoped transition series;
- transition by-symbol.

The symbol-scoped series test intentionally guards selective access without hard-coding a specific supporting index choice beyond the absence of a transition-event sequential scan.

## 6. Index decision

No additional transition-metrics index was added in v1.1.2.18.

The representative evidence does not demonstrate a concrete access-path problem that would justify another index or migration.

This preserves the evidence-driven indexing rule introduced in v1.1.2.14: do not add speculative indexes merely because a query shape exists.

Existing relevant index remains:

`IX_TradingSignalOutcomeEvents_OccurredAt`

from migration:

`20261001172500_AddSignalTransitionOccurredAtIndex`

## 7. CI result

GitHub Actions #117 validates final implementation commit:

`15a77cf5364c3c1949976cbcd0973dc2541ad54f`

Results:

- Restore: success
- Build: success
- warnings: 0
- errors: 0
- tests: **104/104 passed**
- new transition aggregation query-plan regression test: passed
- existing transition window query-plan fixture: passed
- existing transition `OccurredAt` index regression: passed
- existing transition API integration test: passed
- existing transition window/series/by-symbol PostgreSQL tests: passed
- API + PostgreSQL runtime smoke: success
- Docker Compose validation: success
- API/Worker Docker images: success

The GitHub runner also emitted an external Actions runtime warning that Node.js 20 is deprecated for some action versions and is being forced to Node.js 24. This is not a TradeOps compiler warning; the TradeOps build itself reported 0 warnings and 0 errors.

## 8. Files changed in v1.1.2.18

Added:

- `tests/TradeOps.UnitTests/SignalTransitionMetricsAggregationQueryPlanPostgresTests.cs`

No production file changed.

No migration was added.

## 9. Existing transition metrics contracts remain unchanged

Window:

`GET /api/metrics/signal-transitions/window`

Series:

`GET /api/metrics/signal-transitions/series`

By symbol:

`GET /api/metrics/signal-transitions/by-symbol`

All remain read-only and use persisted `TradingSignalOutcomeEvents.OccurredAt` as the transition time axis.

## 10. Existing execution metrics remain unchanged

The following retain their established creation-time/current-outcome semantics:

- `GET /api/metrics/execution`
- `GET /api/metrics/execution/window`
- `GET /api/metrics/execution/series`
- `GET /api/metrics/execution/by-symbol`

Do not silently reinterpret these as transition-event metrics.

## 11. Historical coverage limitation remains

Outcome transition history began in v1.1.2.12 without synthetic backfill.

Pre-v1.1.2.12 signals may have current terminal outcomes and no persisted transition history.

Do not reconstruct transition timestamps or historical counts from `TradingSignals.CreatedAt`, the current projection outcome, migration time, or any other surrogate timestamp.

## 12. Safety and project boundaries preserved

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

## 13. Known limitations after v1.1.2.18

- transition history remains incomplete for pre-v1.1.2.12 legacy signals;
- representative query-plan regression now covers selective window, series and by-symbol shapes, but it does not establish behavior for much wider/high-cardinality reporting windows;
- repository latency measurements are CI observations, not a formal production latency SLO;
- generated query-plan Markdown is written to the Actions workspace/step summary but is not retained as a durable downloadable workflow artifact;
- no `symbol x bucket` transition matrix;
- no materialized metrics rollups;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboard;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 14. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.19`

Recommended narrow scope:

**Wider-window transition-metrics plan characterization and durable CI evidence.**

Suggested work:

- reuse the same 40,000-signal / 80,000-event representative fixture;
- characterize a materially wider event-time window (for example 24 hours) for transition series and by-symbol queries;
- record `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` observations without assuming that a sequential scan is inherently wrong at lower selectivity;
- keep the current selective-window regression guard unchanged;
- retain generated query-plan Markdown as a workflow artifact so plan evidence is available after the job completes;
- add no index unless the wider-window evidence identifies a concrete and correctable problem;
- make no API contract or trading/execution changes;
- do not combine this work with a new metrics surface or observability product feature.

## 15. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.18.md`

Continue from branch:

`TradeOps/v_1.1.2.18`

Verified final implementation SHA:

`15a77cf5364c3c1949976cbcd0973dc2541ad54f`

GitHub Actions #117 (`36904331558`) is fully green with 104/104 tests passing.

Do not change existing execution-metrics semantics.

Do not fabricate legacy transition history.

Do not add another transition index without representative evidence.
