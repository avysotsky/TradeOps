# TradeOps handoff — v1.1.2.19

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.19`

Verified implementation commit:

`2a3b8775b96e67111be33a56f4392a0e3261b9bc`

Commit message:

`test: characterize wide transition query plans`

Main GitHub Actions validation:

- workflow: `build`
- run number: **119**
- run id: `36905436957`
- job id: `110514679098`
- result: **success**
- tests: **105 passed / 105 total**
- Build: **0 warnings / 0 errors**
- API + PostgreSQL smoke: success
- Docker Compose validation: success
- API/Worker Docker image build: success

Dedicated query-plan evidence validation:

- workflow: `query-plan-evidence`
- run number: **1**
- run id: `36905437104`
- job id: `110514678626`
- result: **success**
- query-plan tests: success
- artifact upload: success

Artifact:

`query-plan-reports-2a3b8775b96e67111be33a56f4392a0e3261b9bc`

Artifact id:

`11184685095`

Artifact retention through:

`2026-10-31`

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.19

The v1.1.2.18 recommendation is complete:

**Wider-window transition-metrics query-plan characterization plus durable CI evidence.**

No production API contract changed.

No runtime application/infrastructure code changed.

No database schema/index changed.

No migration was added.

## 3. Added 24-hour characterization test

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsWideWindowQueryPlanPostgresTests.cs`

Test:

`RepresentativeVolume_CharacterizesTwentyFourHourSeriesAndBySymbolPlans`

Fixture:

- PostgreSQL 16;
- real EF Core migration chain;
- 40,000 `TradingSignals`;
- 80,000 persisted `TradingSignalOutcomeEvents`;
- deterministic approximately 28-day distribution;
- 8 symbols;
- 2 outcome events per signal;
- `ANALYZE` after seeding.

Characterization window:

- event-time window: 24 hours;
- series bucket: 1 hour;
- by-symbol limit: 20.

Repository-level correctness is checked before plan characterization.

For the deterministic 24-hour fixture window the test confirms:

- all-symbol series: 24 buckets;
- 1,440 Received transitions;
- 720 Accepted transitions;
- 720 Rejected transitions;
- BTCUSDT series: 24 buckets;
- 180 Received transitions;
- 180 Accepted transitions;
- 0 Rejected transitions;
- by-symbol: 8 symbols;
- 2,880 total transitions across symbols;
- `isTruncated == false`.

## 4. Wider-window EXPLAIN evidence

The dedicated evidence workflow captured `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` against SQL matching production query shapes.

Observed PostgreSQL version:

`16.15 (Debian 16.15-1.pgdg13+2)`

Observed 24-hour plans on the CI fixture:

| Query shape | Planning ms | Execution ms | Root rows | Shared hit | Shared read | Indexes observed | Outcome-event Seq Scan |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| transition series, all symbols, 24h / 1h | 0.47 | 1.28 | 24 | 57 | 0 | `IX_TradingSignalOutcomeEvents_OccurredAt` | no |
| transition series, BTCUSDT, 24h / 1h | 0.61 | 3.99 | 24 | 822 | 0 | `IX_TradingSignalOutcomeEvents_OccurredAt`, `IX_TradingSignals_Symbol_CreatedAt` | no |
| transition by-symbol, 24h | 0.18 | 7.74 | 8 | 791 | 0 | `IX_TradingSignalOutcomeEvents_OccurredAt` | no |

These values are observations from one deterministic CI fixture. They are not a production SLO and must not be interpreted as guaranteed production latency.

The 24-hour test intentionally does not encode a universal `no Seq Scan` invariant: PostgreSQL may legitimately choose a sequential scan for lower-selectivity windows. The current observed plans happened to remain index-backed.

## 5. Selective-window baseline retained

The v1.1.2.18 selective 1-hour baseline was also retained in the artifact.

Observed repository median latency after warm-up on the CI fixture:

| Query | Median ms |
| --- | ---: |
| transition series: 1h / 15m, all symbols | 8.79 |
| transition series: 1h / 15m, BTCUSDT | 10.10 |
| transition by-symbol: 1h / limit 20 | 13.31 |

Observed `EXPLAIN ANALYZE` execution time:

| Query shape | Execution ms | Outcome-event Seq Scan |
| --- | ---: | --- |
| transition series, all symbols, 1h | 0.24 | no |
| transition series, BTCUSDT, 1h | 0.53 | no |
| transition by-symbol, 1h | 0.33 | no |

Again, these are CI-fixture observations, not production SLOs.

## 6. Index decision

No new database index was added.

The wider 24-hour characterization still uses the existing transition event-time access path effectively on this representative fixture.

Existing relevant index remains:

`IX_TradingSignalOutcomeEvents_OccurredAt`

from migration:

`20261001172500_AddSignalTransitionOccurredAtIndex`

There is still no evidence-based justification for another transition-metrics migration/index.

## 7. Dedicated evidence workflow

Added:

`.github/workflows/query-plan-evidence.yml`

Purpose:

- keep heavy plan evidence separate from the normal build workflow;
- run only tests whose fully-qualified names contain `QueryPlanPostgresTests`;
- use a real PostgreSQL 16 service;
- retain generated `*-query-plan-report.md` files as a downloadable GitHub Actions artifact.

Triggers:

- push to `TradeOps/**` when the dedicated workflow or a `*QueryPlanPostgresTests.cs` file changes;
- manual `workflow_dispatch`.

Action versions used by this dedicated workflow:

- `actions/checkout@v7`;
- `actions/setup-dotnet@v6`;
- `actions/upload-artifact@v7`.

Artifact retention:

- 30 days.

The existing main `build.yml` was deliberately left unchanged in this version.

## 8. Durable query-plan artifact contents

Evidence run #1 produced four Markdown reports:

- `metrics-query-plan-report.md`;
- `signal-transition-query-plan-report.md`;
- `signal-transition-aggregation-query-plan-report.md`;
- `signal-transition-wide-window-query-plan-report.md`.

Artifact metadata:

- artifact id: `11184685095`;
- size: 2,977 bytes;
- SHA-256 digest: `b3ed6b477e05704edc33dfebacbfe18aa0dacd4031eb4618363b276d5ddf6986`;
- expires: `2026-10-31T18:16:42Z`.

## 9. Main CI result

GitHub Actions build #119 validates final implementation commit:

`2a3b8775b96e67111be33a56f4392a0e3261b9bc`

Results:

- Restore: success;
- Build: success;
- warnings: 0;
- errors: 0;
- tests: **105/105 passed**;
- new 24-hour transition query-plan test: passed;
- existing selective transition aggregation plan test: passed;
- existing transition window plan fixture: passed;
- existing event-time index regression: passed;
- API integration test: passed;
- API + PostgreSQL runtime smoke: success;
- Docker Compose validation: success;
- Docker images: success.

The legacy main `build.yml` still emits GitHub's external Node.js 20 deprecation warning for `actions/checkout@v4` and `actions/setup-dotnet@v4`. This is not a C#/.NET build warning; the TradeOps build reports 0 warnings and 0 errors. The dedicated query-plan workflow already uses Node 24-compatible action majors.

## 10. Files changed in v1.1.2.19

Added:

- `.github/workflows/query-plan-evidence.yml`;
- `tests/TradeOps.UnitTests/SignalTransitionMetricsWideWindowQueryPlanPostgresTests.cs`.

No production source file changed.

No migration was added.

## 11. Transition metrics contracts remain unchanged

Window:

`GET /api/metrics/signal-transitions/window`

Series:

`GET /api/metrics/signal-transitions/series`

By symbol:

`GET /api/metrics/signal-transitions/by-symbol`

All remain read-only and use persisted `TradingSignalOutcomeEvents.OccurredAt` as the event-time axis.

## 12. Existing execution metrics remain unchanged

The following retain creation-time/current-outcome semantics:

- `GET /api/metrics/execution`;
- `GET /api/metrics/execution/window`;
- `GET /api/metrics/execution/series`;
- `GET /api/metrics/execution/by-symbol`.

Do not silently reinterpret these as transition-event metrics.

## 13. Historical coverage limitation remains

Transition history began in v1.1.2.12 without synthetic backfill.

Pre-v1.1.2.12 signals may have current terminal outcomes and no persisted transition history.

Do not reconstruct transition timestamps or historical counts from `TradingSignals.CreatedAt`, current projection outcome, migration time, or another surrogate timestamp.

## 14. Safety and project boundaries preserved

Still in force:

- .NET 8;
- PostgreSQL 16;
- Mock exchange default;
- Bybit Testnet only real venue adapter;
- no mainnet / real-money support;
- deterministic `ClientOrderId`;
- no blind placement retry after ambiguity;
- no blind cancellation retry after ambiguity;
- no automatic flattening;
- no strategy / alpha generation;
- no return guarantees;
- order lifecycle, reconciliation, recovery and risk semantics unchanged.

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 15. Known limitations after v1.1.2.19

- transition history is incomplete for pre-v1.1.2.12 legacy signals;
- plan evidence uses deterministic CI fixture data, not production telemetry;
- no formal latency SLO exists;
- dedicated artifacts expire after the configured retention period;
- no materialized reporting rollups;
- no Prometheus/OpenTelemetry export;
- no Grafana dashboard;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 16. Recommended next direction

The transition-metrics surface is now functionally and operationally well-covered. Avoid adding more metrics endpoints merely for completeness.

Recommended next version:

`TradeOps/v_1.1.2.20`

Before choosing a feature, inspect the current operator/API surface and choose a concrete product-facing gap in execution automation. Prefer something that improves a customer's ability to submit, audit, cancel, reconcile, or diagnose externally supplied trading instructions.

Do not expand strategy/alpha scope.

Do not add another transition index absent new representative evidence.

A small CI-only cleanup of the legacy Node 20 action warning is valid later, but should not displace a higher-value product-facing milestone unless it becomes operationally necessary.

## 17. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.19.md`

Continue from branch:

`TradeOps/v_1.1.2.19`

Verified implementation SHA:

`2a3b8775b96e67111be33a56f4392a0e3261b9bc`

Main build #119 (`36905436957`) is fully green with 105/105 tests.

Dedicated query-plan evidence #1 (`36905437104`) is fully green and produced artifact `11184685095`.

Do not change existing execution-metrics semantics.

Do not fabricate legacy transition history.

Do not add speculative indexes.
