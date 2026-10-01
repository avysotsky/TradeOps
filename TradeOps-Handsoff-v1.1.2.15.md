# TradeOps handoff — v1.1.2.15

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.15`

Verified implementation commit:

`ee880860d668278dece8c4e80116564d0b4c364c`

Commit message:

`feat: add signal transition series metrics`

GitHub Actions validation:

- workflow: `build`
- run number: **112**
- run id: `36899697588`
- job id: `110495482075`
- result: **success**
- unit tests: **98 passed / 98 total**

Successful steps:

- Restore
- Build
- Unit tests, including PostgreSQL-backed signal-transition series coverage
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.15

The narrow goal from `TradeOps-Handsoff-v1.1.2.14.md` is complete:

**Bounded bucketed signal-outcome transition series, kept separate from the established execution-series semantics.**

No execution/trading behavior changed.

No existing execution-metrics semantics changed.

## 3. New endpoint

Added:

`GET /api/metrics/signal-transitions/series?from=...&to=...&bucket=...&symbol=...`

Required query parameters:

- `from`
- `to`
- `bucket`

Optional:

- `symbol`

Window semantics remain `[from,to)`.

Timestamps are normalized to UTC.

Symbols are trimmed and normalized to uppercase.

Supported buckets:

- `1m`
- `5m`
- `15m`
- `1h`
- `1d`

The maximum returned series length is 500 buckets. Requests requiring more than 500 buckets return HTTP 400.

## 4. Time-axis semantics

The transition series uses only:

`TradingSignalOutcomeEvents.OccurredAt`

It does not bucket by `TradingSignals.CreatedAt`.

It does not infer historical transition time from the current `TradingSignals.Outcome` projection.

Each bucket reports:

- `receivedTransitions`
- `acceptedTransitions`
- `rejectedTransitions`

This is intentionally distinct from the existing execution series, whose signal counters preserve signal-creation/current-outcome semantics.

## 5. PostgreSQL aggregation

`EfSignalTransitionMetricsRepository.GetSeriesAsync(...)` performs aggregation in PostgreSQL.

Implementation uses:

- `generate_series(...)` to generate the complete bucket range;
- `date_bin(...)` aligned to UTC Unix epoch;
- aggregation of `TradingSignalOutcomeEvents` by `OccurredAt`;
- optional join to `TradingSignals` for symbol filtering;
- left join from generated buckets to transition counts.

Therefore empty buckets are returned explicitly with zero transition counts rather than disappearing from the response.

The first and last response bucket boundaries are clipped to the requested `[from,to)` interval.

## 6. Symbol scoping

When `symbol` is supplied, transition events are joined to their parent `TradingSignals` record using `TradingSignalId`, and filtered by `TradingSignals.Symbol`.

The event table does not duplicate symbol data.

## 7. New application models

Added to the signal-transition metrics model surface:

`SignalTransitionMetricsSeriesBucket`

Fields:

- `FromInclusive`
- `ToExclusive`
- `ReceivedTransitions`
- `AcceptedTransitions`
- `RejectedTransitions`

Added:

`SignalTransitionMetricsSeriesSnapshot`

Fields:

- `GeneratedAt`
- `FromInclusive`
- `ToExclusive`
- `Bucket`
- `Symbol`
- `Buckets`

`ISignalTransitionMetricsRepository` now exposes both:

- `GetWindowAsync(...)`
- `GetSeriesAsync(...)`

## 8. Controller validation

`SignalTransitionMetricsController` now shares window/symbol validation between window and series routes.

Series validation covers:

- missing/invalid window;
- unsupported or missing bucket;
- symbol normalization and length limit;
- bucket alignment;
- maximum 500 buckets.

Controller tests verify:

- UTC normalization;
- bucket normalization;
- symbol normalization;
- unsupported buckets rejected;
- 501 buckets rejected;
- exactly 500 buckets accepted.

## 9. PostgreSQL integration coverage

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsSeriesPostgresTests.cs`

The test applies real migrations to an isolated PostgreSQL database and verifies:

- series is bucketed by persisted outcome-event `OccurredAt`;
- event exactly before `from` is excluded;
- event exactly at `to` is excluded;
- first partial bucket is clipped to requested `from`;
- final partial bucket is clipped to requested `to`;
- explicit zero-count bucket is emitted;
- optional BTCUSDT scope excludes ETHUSDT events;
- unscoped series includes both BTCUSDT and ETHUSDT events;
- a legacy terminal current projection with no history does not create a fabricated transition.

The test passed in GitHub Actions #112.

## 10. Documentation

Added:

`docs/SignalTransitionMetricsSeries.md`

It documents:

- endpoint and parameters;
- supported buckets;
- 500-bucket bound;
- event-time semantics;
- explicit empty-bucket behavior;
- symbol scoping;
- legacy-history coverage limitation;
- separation from execution metrics.

## 11. Existing metrics contracts remain unchanged

The existing transition-window endpoint remains:

`GET /api/metrics/signal-transitions/window`

The established execution metrics remain unchanged:

- `GET /api/metrics/execution`
- `GET /api/metrics/execution/window`
- `GET /api/metrics/execution/series`
- `GET /api/metrics/execution/by-symbol`

Do not silently mix creation-time/current-outcome signal metrics with outcome-event-time transition metrics.

## 12. Historical coverage limitation remains

Outcome history began in v1.1.2.12 without synthetic backfill.

Pre-v1.1.2.12 signals may have a current `Accepted` or `Rejected` projection with no persisted historical outcome events.

Therefore a zero transition bucket means only that no persisted transition event exists in that bucket and scope. It is not proof that no historical transition happened before truthful history persistence existed.

Do not fabricate legacy transition timestamps.

## 13. Indexing/performance baseline remains

v1.1.2.14 added:

`IX_TradingSignalOutcomeEvents_OccurredAt`

and representative PostgreSQL plan regression coverage.

That index remains the primary event-time access path used by transition window/series queries.

No additional index was added in v1.1.2.15.

## 14. Files changed in the verified implementation commit

Modified:

- `src/TradeOps.Application/Interfaces/ISignalTransitionMetricsRepository.cs`
- `src/TradeOps.Application/Models/SignalTransitionMetricsWindowSnapshot.cs`
- `src/TradeOps.Api/Controllers/SignalTransitionMetricsController.cs`
- `src/TradeOps.Infrastructure/Persistence/Repositories/EfSignalTransitionMetricsRepository.cs`
- `tests/TradeOps.UnitTests/SignalTransitionMetricsControllerTests.cs`

Added:

- `tests/TradeOps.UnitTests/SignalTransitionMetricsSeriesPostgresTests.cs`
- `docs/SignalTransitionMetricsSeries.md`

No database migration was added in v1.1.2.15.

## 15. Safety and project boundaries preserved

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

## 16. Known limitations after v1.1.2.15

- transition history remains incomplete for legacy pre-v1.1.2.12 signals;
- no transition by-symbol ranking endpoint;
- no `symbol x bucket` transition matrix;
- no formal production latency SLO;
- no materialized metrics rollups;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboard;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 17. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.16`

Recommended narrow scope:

**Bounded signal-transition metrics grouped by symbol, still based only on persisted outcome-event time.**

A suitable API is:

`GET /api/metrics/signal-transitions/by-symbol?from=...&to=...&limit=...`

Recommended rules:

- use only `TradingSignalOutcomeEvents.OccurredAt` for the requested window;
- join parent `TradingSignals` only to obtain `Symbol`;
- return transition counts per symbol (`received`, `accepted`, `rejected`) plus a total transition count;
- deterministic ordering: total transitions descending, then symbol ascending;
- keep a bounded result, for example default 20 and maximum 100;
- preserve `[from,to)` semantics;
- do not infer transitions from current signal projections;
- document legacy historical incompleteness;
- aggregate in PostgreSQL;
- add PostgreSQL-backed correctness coverage before considering any new symbol-oriented index;
- do not combine this version with Prometheus/Grafana/export work.

## 18. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.15.md`

Continue from branch:

`TradeOps/v_1.1.2.15`

Verified implementation SHA:

`ee880860d668278dece8c4e80116564d0b4c364c`

GitHub Actions #112 (`36899697588`) is fully green with 98/98 tests passing.

Do not change existing execution-metrics semantics.

Do not fabricate legacy transition history.

Preserve the v1.1.2.14 event-time index and query-plan regression guard.
