# TradeOps handoff — v1.1.2.11

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.11`

Verified implementation commit:

`7f946b8ac62ac406ace454595e9198600afc2158`

Commit message:

`test: add execution metrics query-plan fixture`

GitHub Actions validation:

- workflow: `build`
- run number: **107**
- run id: `36882877735`
- job id: `110438781142`
- result: **success**

Successful steps:

- Restore
- Build
- Unit tests, including the PostgreSQL 16 representative-volume query-plan fixture
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

The handoff commit itself is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.11

The narrow goal from `TradeOps-Handsoff-v1.1.2.10.md` is complete:

**Execution metrics query-plan and representative-volume validation.**

No public API contract was changed.

No production repository semantics were changed.

No database migration was added.

The purpose of this version was to measure and validate the existing metrics access paths before adding any further indexes.

## 3. Representative PostgreSQL fixture

Added:

`tests/TradeOps.UnitTests/ExecutionMetricsQueryPlanPostgresTests.cs`

The test creates an isolated temporary PostgreSQL database, applies the real EF Core migrations and loads deterministic data using PostgreSQL `generate_series`.

Fixture volume:

- 20,000 `Orders`
- 40,000 `TradingSignals`
- 60,000 `OrderLifecycleEvents`
- 20,000 `Fills`

That is 140,000 persisted execution facts plus 20,000 parent orders.

The facts are distributed across approximately 28 days and eight symbols:

- BTCUSDT
- ETHUSDT
- SOLUSDT
- XRPUSDT
- ADAUSDT
- DOGEUSDT
- LINKUSDT
- AVAXUSDT

After loading, the fixture runs PostgreSQL `ANALYZE` on all involved tables before measuring query plans.

## 4. Repository latency observations

The fixture warms the real repositories, then records median elapsed time over five measured calls for:

- `GetWindowAsync(...)`: 1-hour BTCUSDT window;
- `GetSeriesAsync(...)`: 6-hour BTCUSDT series with `5m` buckets;
- `EfExecutionMetricsBySymbolRepository.GetAsync(...)`: 24-hour window with `limit=50`.

The resulting values are emitted into the GitHub Actions step summary as a Markdown baseline report.

These values are environment-specific CI observations, not a production SLO and not a production-capacity claim.

The test also writes the same report to:

`metrics-query-plan-report.md`

inside `GITHUB_WORKSPACE` for the duration of the CI run.

## 5. EXPLAIN ANALYZE coverage

The fixture executes:

`EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)`

for these query shapes:

- symbol-scoped window signal aggregation;
- symbol-scoped window lifecycle aggregation;
- symbol-scoped window distinct `ordersTouched` aggregation;
- symbol-scoped window fill count;
- symbol-scoped fixed-bucket series aggregation;
- bounded by-symbol aggregation.

The report captures:

- planning time;
- execution time;
- root actual rows;
- shared hit blocks;
- shared read blocks;
- index names selected by PostgreSQL;
- execution fact tables using sequential scans.

The fixture does not manipulate planner settings such as `enable_seqscan=off`.

## 6. Structural plan guardrail

For the deliberately selective test windows, a sequential scan of any execution fact table fails the test:

- `TradingSignals`
- `OrderLifecycleEvents`
- `Fills`

This is intentionally narrower than banning every sequential scan in the complete plan. PostgreSQL may legitimately scan a relatively small parent/reference relation depending on join strategy.

GitHub Actions #107 passed this guardrail on PostgreSQL 16.

Therefore none of the validated selective query shapes used a sequential scan on the three execution fact tables.

## 7. Evidence-driven index decision

No new index was added in v1.1.2.11.

This is deliberate.

The existing access paths already passed the representative selective-plan guardrail. Relevant existing indexes include:

- `IX_TradingSignals_CreatedAt`
- `IX_TradingSignals_Symbol_CreatedAt`
- `IX_OrderLifecycleEvents_OccurredAt`
- `IX_OrderLifecycleEvents_OrderId_OccurredAt`
- `IX_Fills_FilledAt`
- `IX_Fills_OrderId_FilledAt`
- `IX_Orders_Symbol`

Adding another speculative index would add write/storage/maintenance cost without evidence from the measured fixture that it is required.

If future representative volume or query shape causes a plan regression, the fixture now provides a concrete place to demonstrate that before adding an index.

## 8. Documentation added

Added:

`docs/ExecutionMetricsQueryPlanFixture.md`

It documents:

- fixture volume;
- data distribution;
- query shapes;
- structural regression rule;
- existing indexes being evaluated;
- GitHub Actions baseline output;
- optional local execution.

## 9. Local execution

The performance fixture executes automatically under GitHub Actions.

For optional local execution, set:

`TRADEOPS_TEST_POSTGRES_ADMIN`

to an administrative PostgreSQL connection string.

The fixture creates and drops its own temporary database.

Without that variable, it returns immediately outside GitHub Actions so ordinary local test runs do not require PostgreSQL.

## 10. Production/runtime changes

None.

Specifically, v1.1.2.11 did not change:

- `/api/metrics/execution`;
- `/api/metrics/execution/window`;
- `/api/metrics/execution/series`;
- `/api/metrics/execution/by-symbol`;
- repository metric semantics;
- bucket semantics;
- `[from,to)` semantics;
- symbol normalization;
- signal current-outcome caveat;
- lifecycle history semantics;
- fill semantics;
- risk state;
- reconciliation/recovery behavior.

No migration was added.

## 11. Safety and project boundaries preserved

Still in force:

- .NET 8;
- PostgreSQL 16;
- Mock exchange remains the default provider;
- Bybit Testnet remains the only real venue adapter;
- no mainnet / real-money support;
- no blind order-placement retry after ambiguous outcome;
- no blind cancellation retry after ambiguous outcome;
- no automatic position flattening;
- no strategy / alpha implementation;
- no return guarantees;
- persistent risk-state semantics unchanged;
- recovery/reconciliation semantics unchanged.

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 12. Known limitations after v1.1.2.11

Still intentionally outside the current scope:

- the representative fixture is not a production load test;
- no formal production latency SLO;
- no materialized metrics rollups for very large historical datasets;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboard;
- no persisted signal outcome transition timestamps/history;
- no `symbol x bucket` matrix endpoint;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

The query-plan fixture should not be interpreted as proof that arbitrary production volumes will have the same latency.

## 13. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.12`

Recommended narrow scope:

**Persisted trading-signal outcome transition history.**

Rationale:

The remaining historical-metrics caveat is now more important than further speculative query tuning: `TradingSignals.Outcome` stores only the current outcome, so window/series/by-symbol accepted/rejected/pending counters are the current outcome of signals created in the selected interval, not a timeline of outcome transitions.

A focused next milestone can add append-only signal outcome events with transition timestamps while preserving the current `TradingSignals` projection and existing endpoint contracts initially.

Suggested boundaries for v1.1.2.12:

- add an append-only `TradingSignalOutcomeEvents` history table;
- persist the initial `Received` event when a signal is accepted for processing;
- persist terminal `Accepted` / `Rejected` outcome events at the same transactional/logical points where the current `TradingSignals.Outcome` projection is updated;
- preserve `TradingSignals` as the latest/current projection;
- add read/history coverage and migration tests;
- do not silently change existing metrics endpoint semantics in the same version unless explicitly scoped and tested;
- no strategy logic, mainnet or second exchange.

An alternative narrow scope, if runtime observability is prioritized instead, is a low-cardinality OpenTelemetry/Prometheus export milestone. Do not combine both scopes.

## 14. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.11.md`

Continue from branch:

`TradeOps/v_1.1.2.11`

Verified implementation commit:

`7f946b8ac62ac406ace454595e9198600afc2158`

GitHub Actions #107 (`36882877735`) is fully green.

Do not add speculative execution-metrics indexes unless a measured plan demonstrates the need.

Do not undo PostgreSQL-side series aggregation from v1.1.2.9 or bounded PostgreSQL-side by-symbol aggregation from v1.1.2.10.
