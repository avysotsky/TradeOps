# TradeOps handoff — v1.1.2.9

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.9`

Verified production-code commit:

`95abfd0642db4517fb3793ca992dda365fc8a985`

Commit message:

`perf: aggregate execution metrics series in postgres`

This commit was validated by GitHub Actions build run **#105** (`36879170678`) and the entire workflow completed successfully.

The handoff commit itself is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.9

The narrow goal from `TradeOps-Handsoff-v1.1.2.8.md` is complete:

**Execution-series scalability hardening: PostgreSQL-side fixed-bucket aggregation.**

The public API contract introduced in v1.1.2.8 was intentionally preserved:

`GET /api/metrics/execution/series?from=...&to=...&bucket=5m&symbol=BTCUSDT`

No route, response DTO/record, bucket option, validation rule, time-window rule, or symbol-normalization rule was changed.

## 3. What changed technically

Before v1.1.2.9, `EfExecutionMetricsRepository.GetSeriesAsync(...)` executed three broad queries and materialized minimal raw facts into .NET:

- trading signal facts: `CreatedAt`, `Outcome`;
- lifecycle facts: `OccurredAt`, `OrderId`, `Status`;
- fill facts: `FilledAt`.

Application code then created a bucket dictionary and iterated through all raw rows.

In v1.1.2.9, series aggregation is performed by PostgreSQL in a single parameterized query.

The query uses:

- PostgreSQL `date_bin(...)` for fixed UTC bucket alignment;
- Unix epoch (`1970-01-01T00:00:00Z`) as the alignment origin;
- `generate_series(...)` to create the complete bucket grid, including empty buckets;
- aggregate `FILTER` expressions for signal outcomes and lifecycle statuses;
- `COUNT(DISTINCT lifecycle."OrderId")` for `ordersTouched`;
- `LEFT JOIN` from the generated bucket grid to signal/lifecycle/fill aggregates;
- `GREATEST(...)` / `LEAST(...)` to preserve clipped first/last bucket boundaries.

Only the final aggregated bucket rows cross from PostgreSQL into the .NET process.

The endpoint still has a maximum of 500 buckets, so the repository normally materializes at most 500 result rows for an API request instead of potentially materializing a large number of persisted facts.

## 4. Preserved series semantics

The following v1.1.2.8 semantics remain unchanged.

### Time interval

Fact selection uses a half-open interval:

`[fromInclusive, toExclusive)`

A fact exactly at `toExclusive` is excluded.

### UTC alignment

Buckets remain aligned to the Unix epoch in UTC, not to the request start.

Example for a `5m` bucket and request `[10:02, 10:13)`:

- `[10:02, 10:05)`
- `[10:05, 10:10)`
- `[10:10, 10:13)`

### Empty buckets

Empty buckets remain present and contain zero counters.

### Bucket whitelist

The API still accepts only:

- `1m`
- `5m`
- `15m`
- `1h`
- `1d`

The 500-bucket guard remains in the API layer. Exactly 500 buckets are accepted; more than 500 returns HTTP 400.

### Symbol semantics

Symbol normalization remains in the API layer:

- null / empty / whitespace => unscoped;
- otherwise `Trim().ToUpperInvariant()`;
- maximum length 50.

For an unscoped series, lifecycle and fill aggregation does not join `Orders` unnecessarily.

For a symbol-scoped series:

- signals filter directly on `TradingSignals.Symbol`;
- lifecycle rows join `Orders` by `OrderId` and filter by `Orders.Symbol`;
- fills join `Orders` by `OrderId` and filter by `Orders.Symbol`.

All user-provided values are SQL parameters. Dynamic SQL fragments are static application-controlled join/predicate fragments only.

## 5. Metric semantics remain unchanged

### Signals

Signals are bucketed by `TradingSignals.CreatedAt`.

Counters retain current-outcome semantics:

- `AcceptedCurrentOutcome`
- `RejectedCurrentOutcome`
- `PendingCurrentOutcome`

`Received` remains the sum of those three counters.

Important historical caveat remains unchanged: the database does not currently store timestamps for signal outcome transitions, so this is not a historical outcome-transition timeline.

### Order lifecycle

Lifecycle metrics are bucketed from persisted `OrderLifecycleEvents.OccurredAt`.

`ordersTouched` is the number of distinct persisted `OrderId` values per bucket.

Current `Orders.Status` is not used to reconstruct history.

### Fills

Fill metrics are bucketed from persisted `Fills.FilledAt`.

## 6. Files changed

### Modified

`src/TradeOps.Infrastructure/Persistence/Repositories/EfExecutionMetricsRepository.cs`

Main change:

- `GetSeriesAsync(...)` no longer materializes raw signal/lifecycle/fill fact arrays;
- one PostgreSQL query now produces the final bucket aggregates;
- the previous in-memory `SeriesBucketAccumulator` and tick-based bucket assignment logic were removed from the series path;
- existing snapshot and window methods were left unchanged.

### Added

`tests/TradeOps.UnitTests/ExecutionMetricsSeriesPostgresTests.cs`

This is a PostgreSQL-backed integration test for the repository series query.

It executes automatically in GitHub Actions using the existing PostgreSQL 16 service.

For optional local execution, set:

`TRADEOPS_TEST_POSTGRES_ADMIN`

to an administrative PostgreSQL connection string. Without that variable, the test returns immediately outside GitHub Actions so the normal local unit-test suite does not require PostgreSQL.

The test creates an isolated temporary database, applies the real EF Core migrations, seeds facts, verifies the series query, and drops the database afterward.

## 7. PostgreSQL integration coverage added

The new test verifies against real PostgreSQL 16:

- `5m` fixed-bucket alignment;
- clipped first bucket;
- clipped last bucket;
- `[from,to)` exclusion at the exact `to` boundary;
- signal accepted/rejected/pending counters;
- lifecycle event counters;
- multiple lifecycle events for one order producing `ordersTouched = 1` in the bucket;
- fill counting;
- symbol filtering excluding ETH facts from a BTC series;
- a fully empty final bucket being returned with zero counters.

This test also validates that the actual PostgreSQL functions and Npgsql parameter mappings used by the production query execute successfully.

## 8. CI verification

GitHub Actions run:

- workflow: `build`
- run number: **105**
- run id: `36879170678`
- tested SHA: `95abfd0642db4517fb3793ca992dda365fc8a985`
- result: **success**

Successful steps included:

- Restore
- Build
- Unit tests
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

The PostgreSQL-backed series integration test ran inside the successful `Unit tests` step.

## 9. Database / migration status

No migration was required for v1.1.2.9.

The existing indexes from v1.1.2.7/v1.1.2.8 remain applicable, including time-window and symbol/time access paths for signals and persisted execution facts.

No schema or persisted-data semantics changed.

## 10. Safety and project boundaries that remain in force

Do not change these implicitly in the next stage:

- .NET 8;
- PostgreSQL 16;
- Mock exchange is the default runtime provider;
- Bybit Testnet is the only real venue adapter currently in scope;
- no mainnet / real-money execution;
- no blind retry of order placement after ambiguous exchange outcomes;
- no blind retry of cancellation after ambiguous exchange outcomes;
- no automatic position flattening;
- no strategy / alpha implementation;
- persistent risk-state semantics remain unchanged;
- recovery and reconciliation semantics remain unchanged.

## 11. Known limitations after v1.1.2.9

The original raw-fact materialization scalability issue for `/api/metrics/execution/series` is resolved at the application boundary, but several broader observability concerns are intentionally still outside this version:

- no grouped-by-symbol metrics endpoint;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboards;
- no dedicated materialized aggregates / rollup tables for extremely large historical datasets;
- no benchmark or load-test target defining a formal series-query latency SLO;
- signal outcome counters still represent current outcome of signals created in the selected bucket because outcome-transition timestamps are not persisted.

Do not silently reinterpret those limitations as bugs in the v1.1.2.9 contract.

## 12. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.10`

Recommended narrow scope:

**Execution metrics breakdown by symbol.**

A sensible next operator-facing capability is to expose a compact per-symbol breakdown for a selected persisted window, while reusing the current execution-metrics semantics and keeping the result bounded.

Before implementing it, define the API shape and explicit cardinality/limit rules. Do not automatically turn the existing series endpoint into an unbounded `symbol x bucket` matrix.

Alternative if the next priority is performance rather than operator capability: make v1.1.2.10 a focused query-plan/load-validation milestone (`EXPLAIN ANALYZE` fixtures + representative fact volume) without changing the API.

## 13. Instruction for the next chat

Use this file as the authoritative starting state:

`TradeOps-Handsoff-v1.1.2.9.md`

Continue from branch:

`TradeOps/v_1.1.2.9`

The verified implementation commit is:

`95abfd0642db4517fb3793ca992dda365fc8a985`

Do not reimplement the v1.1.2.9 series aggregation in application memory.
