# TradeOps handoff — v1.1.2.13

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.13`

Verified implementation commit:

`d5921f5a084aa06440d8b3cdd5b604797b647ee1`

Commit message:

`feat: add signal transition window metrics`

GitHub Actions validation:

- workflow: `build`
- run number: **109**
- run id: `36892403164`
- job id: `110470989160`
- result: **success**

Successful steps:

- Restore
- Build
- Unit tests, including PostgreSQL-backed signal transition metrics coverage
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.13

The narrow goal from `TradeOps-Handsoff-v1.1.2.12.md` is complete:

**Historical signal outcome transition window metrics, exposed separately from the established execution metrics contract.**

The implementation deliberately does not reinterpret the existing execution metrics endpoints.

## 3. New API surface

New endpoint:

`GET /api/metrics/signal-transitions/window?from=...&to=...&symbol=...`

Parameters:

- `from` required;
- `to` required;
- `symbol` optional;
- window semantics are `[from,to)`;
- timestamps are normalized to UTC;
- symbol is trimmed and normalized to uppercase;
- symbols longer than 50 characters after trimming are rejected with HTTP 400.

Response fields:

- `generatedAt`
- `fromInclusive`
- `toExclusive`
- `symbol`
- `receivedTransitions`
- `acceptedTransitions`
- `rejectedTransitions`

## 4. Time-axis semantics

This endpoint uses only persisted signal outcome events selected by:

`TradingSignalOutcomeEvents.OccurredAt`

Therefore:

- `receivedTransitions` counts persisted `Received` events whose `OccurredAt` lies inside `[from,to)`;
- `acceptedTransitions` counts persisted `Accepted` events whose `OccurredAt` lies inside `[from,to)`;
- `rejectedTransitions` counts persisted `Rejected` events whose `OccurredAt` lies inside `[from,to)`.

It does **not** select signals by `TradingSignals.CreatedAt`.

It does **not** reconstruct historical transitions from the current `TradingSignals.Outcome` value.

This keeps transition-event metrics semantically separate from current-projection metrics.

## 5. Existing execution metrics remain unchanged

The following established endpoints retain their previous signal-creation/current-outcome semantics:

- `GET /api/metrics/execution`
- `GET /api/metrics/execution/window`
- `GET /api/metrics/execution/series`
- `GET /api/metrics/execution/by-symbol`

No method was added to `IExecutionMetricsRepository`.

No existing execution metrics repository query was modified.

A separate abstraction was introduced instead:

`ISignalTransitionMetricsRepository`

Implementation:

`EfSignalTransitionMetricsRepository`

This separation is intentional and should be preserved unless a later version explicitly redesigns the public metrics model.

## 6. Symbol scoping

When `symbol` is supplied, transition events are joined to their parent `TradingSignals` row through `TradingSignalId`, and filtering uses the parent signal's `Symbol`.

The event table itself intentionally remains free of duplicated symbol data.

## 7. Historical coverage limitation

Signal outcome history was introduced in v1.1.2.12 without synthetic backfill.

Therefore transition metrics are truthful only for outcome events actually persisted after the outcome-history model exists.

Legacy signals can have a current projection such as `Accepted` or `Rejected` while having no historical outcome events.

Consequences:

- do not interpret zero transition counts for legacy periods as proof that no transitions happened;
- do not infer or fabricate `OccurredAt` timestamps from `TradingSignals.CreatedAt`, current outcome, migration time, order timestamps or other approximations;
- do not silently mix legacy current-projection counts into this endpoint.

## 8. Tests added

### Controller tests

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsControllerTests.cs`

Coverage includes:

- UTC normalization;
- symbol normalization;
- required window bounds;
- invalid/reversed bounds;
- repository is not called for invalid requests.

### PostgreSQL integration test

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsPostgresTests.cs`

The test creates an isolated PostgreSQL database, applies the real migrations and verifies:

- event-time selection is based on `TradingSignalOutcomeEvents.OccurredAt` rather than signal creation time;
- `[from,to)` exclusion at the exact upper bound;
- received / accepted / rejected transition counts;
- optional symbol scope through parent `TradingSignals.Symbol`;
- a legacy current projection with no history contributes no fabricated transition;
- real PostgreSQL 16 migration/runtime behavior.

This coverage passed in GitHub Actions #109.

## 9. Documentation added

Added:

`docs/signal-transition-metrics.md`

It documents:

- the new endpoint;
- response fields;
- outcome-event time axis;
- separation from existing execution metrics;
- incomplete pre-v1.1.2.12 historical coverage;
- prohibition on fabricating legacy transition timestamps.

## 10. Files changed in the verified implementation commit

Added:

- `src/TradeOps.Application/Interfaces/ISignalTransitionMetricsRepository.cs`
- `src/TradeOps.Application/Models/SignalTransitionMetricsWindowSnapshot.cs`
- `src/TradeOps.Infrastructure/Persistence/Repositories/EfSignalTransitionMetricsRepository.cs`
- `src/TradeOps.Api/Controllers/SignalTransitionMetricsController.cs`
- `tests/TradeOps.UnitTests/SignalTransitionMetricsControllerTests.cs`
- `tests/TradeOps.UnitTests/SignalTransitionMetricsPostgresTests.cs`
- `docs/signal-transition-metrics.md`

Modified:

- `src/TradeOps.Api/Program.cs`

No database migration was added in v1.1.2.13.

No existing execution-metrics repository was modified.

## 11. Current indexing situation

The v1.1.2.12 outcome-history model currently has the index:

`(TradingSignalId, OccurredAt)`

That index is appropriate for per-signal history reads and can participate in symbol-scoped joins depending on the PostgreSQL plan.

The new transition metrics endpoint also supports an unscoped `OccurredAt` range query. No new index was added in v1.1.2.13 because the project rule from v1.1.2.11 remains evidence-driven: do not add speculative indexes before measuring a representative PostgreSQL query plan.

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

## 13. Known limitations after v1.1.2.13

- transition history is incomplete for legacy pre-v1.1.2.12 signals because no synthetic backfill exists;
- transition metrics currently expose only a window aggregate, not a bucketed series;
- no representative-volume query-plan fixture yet covers `TradingSignalOutcomeEvents` transition-metrics access paths;
- no evidence-driven decision has yet been made about adding an `OccurredAt`-leading transition-history index;
- no signal-transition by-symbol ranking endpoint;
- no signal-transition `symbol x bucket` matrix;
- no formal production latency SLO;
- no materialized metrics rollups;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboard;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 14. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.14`

Recommended narrow scope:

**Representative PostgreSQL query-plan validation for signal transition metrics and an evidence-driven index decision.**

Suggested work:

- extend the v1.1.2.11 query-plan discipline to `TradingSignalOutcomeEvents`;
- load representative transition-history volume across multiple symbols and days;
- run `ANALYZE` before measurement;
- validate both unscoped `[from,to)` and symbol-scoped `[from,to)` transition aggregation shapes;
- capture `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` results;
- identify sequential scans of `TradingSignalOutcomeEvents` for deliberately selective windows;
- add an `OccurredAt`-leading index only if the representative plan demonstrates the need;
- if an index is added, cover the real migration on PostgreSQL and verify the plan after migration;
- do not add transition series or unrelated observability/export work in the same version.

This keeps the project consistent with the evidence-before-index discipline established in v1.1.2.11.

## 15. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.13.md`

Continue from branch:

`TradeOps/v_1.1.2.13`

Verified implementation SHA:

`d5921f5a084aa06440d8b3cdd5b604797b647ee1`

GitHub Actions #109 (`36892403164`) is fully green.

Do not change existing execution metrics semantics.

Do not fabricate legacy transition history.

Do not add a transition-history index without representative PostgreSQL plan evidence.
