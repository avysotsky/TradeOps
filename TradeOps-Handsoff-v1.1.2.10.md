# TradeOps handoff — v1.1.2.10

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.10`

Verified implementation commit:

`cd483d6e0cb144d602fb1d8c4bafbf264997a3fa`

Commit message:

`feat: add bounded execution metrics by symbol`

GitHub Actions validation:

- workflow: `build`
- run number: **106**
- run id: `36880784635`
- job id: `110431721738`
- result: **success**

Successful steps:

- Restore
- Build
- Unit tests, including PostgreSQL-backed by-symbol integration coverage
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.10

The narrow goal from `TradeOps-Handsoff-v1.1.2.9.md` is complete:

**Execution metrics breakdown by symbol.**

New read-only endpoint:

`GET /api/metrics/execution/by-symbol?from=...&to=...&limit=50`

The existing endpoints remain unchanged:

- `GET /api/metrics/execution`
- `GET /api/metrics/execution/window`
- `GET /api/metrics/execution/series`

The series endpoint was deliberately not changed into a `symbol x bucket` matrix.

## 3. API contract

Required query parameters:

- `from`
- `to`

Optional:

- `limit`

Window semantics remain half-open:

`[from, to)`

`from` and `to` are normalized to UTC before repository execution.

Invalid or missing window parameters return HTTP 400.

### Limit rules

Default:

`limit=50`

Allowed range:

`1..100`

A value below 1 or above 100 returns HTTP 400.

The repository fetches at most `limit + 1` aggregate rows. The extra row is used only to determine whether more symbols exist.

Response fields include:

- `generatedAt`
- `fromInclusive`
- `toExclusive`
- `limit`
- `isTruncated`
- `symbols[]`

`isTruncated=true` means at least one additional symbol with matching persisted activity exists outside the returned bounded result.

## 4. Per-symbol metrics

Each symbol item contains:

- `symbol`
- `signals`
- `orderLifecycle`
- `fillsReceived`

Signal counters preserve the existing historical-window semantics:

- signals are selected by `TradingSignals.CreatedAt`;
- `AcceptedCurrentOutcome`, `RejectedCurrentOutcome`, and `PendingCurrentOutcome` reflect the current persisted outcome of signals created inside the selected window;
- `Received` is the sum of those three counters.

The known limitation remains: signal outcome transition timestamps are not persisted, so these counters are not an outcome-transition timeline.

Order lifecycle counters use persisted `OrderLifecycleEvents.OccurredAt` facts and join `Orders` only to obtain the symbol.

`ordersTouched` is `COUNT(DISTINCT OrderId)` per symbol over the selected window.

Current `Orders.Status` is not used to reconstruct historical lifecycle state.

Fill counts use persisted `Fills.FilledAt` facts and join `Orders` only to obtain the symbol.

## 5. Symbol inclusion and ordering

A symbol is included if it has at least one matching persisted fact in any of these families inside `[from,to)`:

- trading signal;
- order lifecycle event;
- fill.

The PostgreSQL query combines the three independently aggregated fact families with `FULL OUTER JOIN`, so signal-only and fill-only symbols are not lost.

Results are ordered by descending persisted execution activity:

`signals received + lifecycle events + fills received`

Ties are resolved by ascending symbol text.

This ordering exists only to make bounded truncation deterministic and operator-useful. It does not represent strategy quality, trading performance, profitability, or alpha.

## 6. Implementation structure

New application model:

`src/TradeOps.Application/Models/ExecutionMetricsBySymbolSnapshot.cs`

New interface:

`src/TradeOps.Application/Interfaces/IExecutionMetricsBySymbolRepository.cs`

New PostgreSQL repository:

`src/TradeOps.Infrastructure/Persistence/Repositories/EfExecutionMetricsBySymbolRepository.cs`

New API controller:

`src/TradeOps.Api/Controllers/MetricsBySymbolController.cs`

DI registration added in:

`src/TradeOps.Api/Program.cs`

The existing `EfExecutionMetricsRepository` for point/window/series metrics was not modified in this version.

## 7. PostgreSQL aggregation design

By-symbol aggregation is performed in PostgreSQL rather than by materializing raw persisted facts in .NET.

The query uses three grouped CTEs:

- signal counts grouped by `TradingSignals.Symbol`;
- lifecycle counts grouped by `Orders.Symbol`;
- fill counts grouped by `Orders.Symbol`.

The aggregated CTEs are combined with `FULL OUTER JOIN`.

Only bounded aggregate rows cross the PostgreSQL -> .NET boundary.

The repository requests `limit + 1` rows and removes the extra row when setting `isTruncated=true`.

No raw signal/lifecycle/fill arrays are loaded for the by-symbol endpoint.

## 8. Test coverage

Added:

`tests/TradeOps.UnitTests/MetricsBySymbolControllerTests.cs`

Controller coverage verifies:

- UTC normalization;
- default `limit=50`;
- invalid limit below 1;
- invalid limit above 100;
- invalid window rejection;
- repository is not called for invalid requests.

Added:

`tests/TradeOps.UnitTests/ExecutionMetricsBySymbolPostgresTests.cs`

The PostgreSQL-backed test creates an isolated temporary database, applies the real migrations and verifies:

- symbols originating from different persisted fact families are all included;
- signal current-outcome counts;
- lifecycle counts;
- distinct `ordersTouched` semantics;
- fill counts;
- a fact exactly at `toExclusive` is excluded;
- deterministic activity ordering;
- `limit` enforcement;
- `isTruncated` behavior.

The integration test ran successfully against the PostgreSQL 16 service in GitHub Actions #106.

## 9. Database / migration status

No schema change was required.

No migration was added.

Existing indexes remain in force.

The endpoint performs window filtering before grouping. Lifecycle/fill symbol resolution uses the existing `OrderId -> Orders.Id` relationship and existing order-symbol indexing.

## 10. Safety and project boundaries preserved

Still in force:

- .NET 8;
- PostgreSQL 16;
- Mock exchange remains the default provider;
- Bybit Testnet remains the only real venue adapter;
- no mainnet / real-money support;
- no blind placement retry after ambiguous outcome;
- no blind cancellation retry after ambiguous outcome;
- no automatic position flattening;
- no strategy / alpha generation;
- no return guarantees;
- persistent risk state unchanged;
- reconciliation/recovery behavior unchanged.

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 11. Known limitations after v1.1.2.10

Still intentionally outside the current scope:

- no `symbol x bucket` matrix endpoint;
- no Prometheus/OpenTelemetry exporter;
- no Grafana dashboard;
- no materialized metrics rollups;
- no formal metrics query latency SLO;
- no representative-volume query-plan benchmark;
- no signal outcome transition history;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 12. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.11`

Recommended narrow scope:

**Execution metrics query-plan and representative-volume validation.**

Goal:

- keep all existing metrics API contracts unchanged;
- create deterministic representative PostgreSQL fact volume for signals/lifecycle/fills;
- capture/verify `EXPLAIN (ANALYZE, BUFFERS)` plans for window, series and by-symbol query shapes;
- identify sequential scans, excessive joins/sorts, or missing access paths;
- add only evidence-driven indexes if the measured plans justify them;
- define a practical benchmark fixture and documented latency baseline rather than claiming production-scale performance without measurement.

Do not combine this milestone with Prometheus/Grafana, a second exchange, mainnet, strategy code, or new metrics API shapes.

## 13. Instruction for the next chat

Use this file as the authoritative continuation point.

Continue from:

`TradeOps/v_1.1.2.10`

Verified implementation SHA:

`cd483d6e0cb144d602fb1d8c4bafbf264997a3fa`

GitHub Actions #106 is fully green.

Do not undo PostgreSQL-side series aggregation from v1.1.2.9 or bounded PostgreSQL-side by-symbol aggregation from v1.1.2.10.
