# TradeOps handoff — v1.1.2.17

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.17`

Feature/test-hardening commit:

`6130c89e2c5649906d0f2390f21ff3341cf1419d`

Commit message:

`test: harden signal transition api surface`

Verified corrective/final implementation commit:

`d889cb12135adfae129f8b5039d18b51d0f48b43`

Commit message:

`fix: expose api entry point for integration tests`

The corrective commit changes only the empty `Program` partial-class syntax required by `WebApplicationFactory`; it does not change runtime behavior.

GitHub Actions validation:

- workflow: `build`
- run number: **115**
- run id: `36901598073`
- job id: `110501838660`
- result: **success**
- unit/integration tests: **103 passed / 103 total**

Successful steps:

- Restore
- Build — 0 warnings, 0 errors
- Unit/integration tests
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.17

The narrow goal from `TradeOps-Handsoff-v1.1.2.16.md` is complete:

**Runtime/OpenAPI integration hardening for the complete signal-transition metrics surface.**

The implementation adds a real ASP.NET Core integration test that launches the API with `WebApplicationFactory`, applies the actual migrations to an isolated PostgreSQL database, seeds deterministic transition history, validates Swagger/OpenAPI and calls all three transition-metrics routes through HTTP.

No trading/execution semantics changed.

No metrics semantics changed.

No database schema/index changed.

## 3. ASP.NET Core functional-test dependency

Added test dependency:

`Microsoft.AspNetCore.Mvc.Testing 8.0.31`

The package targets .NET 8 and provides `WebApplicationFactory` / TestServer support for functional API tests.

No runtime production package reference was added to the API project.

## 4. API entry point exposure for tests

`src/TradeOps.Api/Program.cs` now ends with:

```csharp
public partial class Program
{
}
```

This is the standard minimal-hosting test seam that allows `WebApplicationFactory<Program>` to reference the application entry point.

It adds no endpoint, service, configuration, middleware or execution behavior.

## 5. New integration test

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsApiIntegrationTests.cs`

Test:

`Api_ExposesOpenApiAndAllSignalTransitionMetricRoutesAgainstPostgres`

The test:

1. creates an isolated PostgreSQL database;
2. starts the actual TradeOps API via `WebApplicationFactory<Program>`;
3. supplies the isolated `ConnectionStrings:TradeOpsDb` configuration;
4. forces `Exchange:Provider=Mock`;
5. disables Telegram;
6. lets application startup apply the real EF Core migration chain;
7. seeds deterministic `TradingSignals` and `TradingSignalOutcomeEvents`;
8. calls the generated Swagger document;
9. calls all three signal-transition metric routes via HTTP;
10. validates their JSON contracts and deterministic values;
11. disposes the application and drops the isolated database.

## 6. OpenAPI validation

The integration test fetches:

`GET /swagger/v1/swagger.json`

and requires all of:

- `/api/metrics/signal-transitions/window`
- `/api/metrics/signal-transitions/series`
- `/api/metrics/signal-transitions/by-symbol`

This closes the gap where the existing shell runtime smoke validated Swagger generally but did not explicitly require the transition-metrics paths.

The check is now part of ordinary `dotnet test`, so no additional GitHub Actions workflow was introduced.

## 7. Runtime HTTP validation — window

The test calls:

`GET /api/metrics/signal-transitions/window`

against seeded PostgreSQL data.

Expected deterministic counts:

- `receivedTransitions = 2`
- `acceptedTransitions = 1`
- `rejectedTransitions = 1`
- `symbol = null`

The seeded legacy XRPUSDT current projection has no outcome-history rows and therefore contributes no fabricated transition.

## 8. Runtime HTTP validation — series

The test calls:

`GET /api/metrics/signal-transitions/series?...&bucket=15m`

for a one-hour window.

It verifies:

- HTTP 200;
- returned bucket name `15m`;
- four stable buckets;
- BTC `Received + Accepted` events appear in the first bucket;
- ETH `Received + Rejected` events appear in the second bucket;
- later empty buckets are present explicitly with zero counts.

The route continues to use only `TradingSignalOutcomeEvents.OccurredAt`.

## 9. Runtime HTTP validation — by symbol

The test calls:

`GET /api/metrics/signal-transitions/by-symbol?...&limit=1`

It verifies:

- HTTP 200;
- `limit = 1`;
- `isTruncated = true` because two populated symbols exist;
- one item is returned;
- BTCUSDT wins the equal-total tie through deterministic symbol ordering;
- BTC counts are `total=2`, `received=1`, `accepted=1`, `rejected=0`.

## 10. Real migration coverage through application startup

The integration-test application startup successfully applied the complete real migration chain through:

`20261001172500_AddSignalTransitionOccurredAtIndex`

before serving requests.

This validates not only repository-level PostgreSQL behavior but also actual API startup/migration wiring.

## 11. CI result

GitHub Actions #115 validates final commit:

`d889cb12135adfae129f8b5039d18b51d0f48b43`

Results:

- Restore: success
- Build: success
- warnings: 0
- errors: 0
- tests: **103/103 passed**
- `SignalTransitionMetricsApiIntegrationTests.Api_ExposesOpenApiAndAllSignalTransitionMetricRoutesAgainstPostgres`: passed
- existing transition window PostgreSQL test: passed
- existing transition series PostgreSQL test: passed
- existing transition by-symbol PostgreSQL test: passed
- existing transition query-plan fixture: passed
- existing transition `OccurredAt` index regression: passed
- API + PostgreSQL runtime smoke: success
- Docker Compose validation: success
- API/Worker Docker images: success

## 12. Existing transition metrics contracts remain unchanged

Window:

`GET /api/metrics/signal-transitions/window`

Series:

`GET /api/metrics/signal-transitions/series`

By symbol:

`GET /api/metrics/signal-transitions/by-symbol`

All use persisted `TradingSignalOutcomeEvents.OccurredAt` as the transition time axis.

## 13. Existing execution metrics remain unchanged

The following continue to use their established contracts:

- `GET /api/metrics/execution`
- `GET /api/metrics/execution/window`
- `GET /api/metrics/execution/series`
- `GET /api/metrics/execution/by-symbol`

Do not silently reinterpret creation-time/current-outcome metrics as transition-event metrics.

## 14. Historical coverage limitation remains

Outcome transition history began in v1.1.2.12 without synthetic backfill.

Pre-v1.1.2.12 signals may have current terminal outcomes and no persisted history.

The integration test intentionally includes such a legacy-style signal and confirms it contributes no fabricated historical transition.

Do not reconstruct transition timestamps or counts from current projection state.

## 15. Index/performance baseline remains

v1.1.2.14 added:

`IX_TradingSignalOutcomeEvents_OccurredAt`

plus representative plan regression coverage for transition-window access.

No new index was added in v1.1.2.17.

## 16. Files changed in v1.1.2.17

Modified:

- `src/TradeOps.Api/Program.cs`
- `tests/TradeOps.UnitTests/TradeOps.UnitTests.csproj`

Added:

- `tests/TradeOps.UnitTests/SignalTransitionMetricsApiIntegrationTests.cs`

No migration was added.

## 17. Safety and project boundaries preserved

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

## 18. Known limitations after v1.1.2.17

- transition history remains incomplete for pre-v1.1.2.12 legacy signals;
- transition-window query plans have representative regression coverage, but transition-series and transition-by-symbol query plans are not yet explicitly measured on the representative-volume fixture;
- no `symbol x bucket` transition matrix;
- no formal production latency SLO;
- no materialized metrics rollups;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboard;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 19. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.18`

Recommended narrow scope:

**Representative PostgreSQL query-plan validation for the transition-series and transition-by-symbol queries introduced after v1.1.2.14.**

Suggested work:

- reuse the 40,000-signal / 80,000-outcome-event representative fixture discipline;
- apply real migrations and run `ANALYZE`;
- inspect `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` for:
  - unscoped transition series;
  - symbol-scoped transition series;
  - transition by-symbol aggregation/ranking;
- confirm selective event-time access through `IX_TradingSignalOutcomeEvents_OccurredAt` where appropriate;
- record repository/SQL observations as CI evidence, not a production SLO;
- add a new index only if the representative plan demonstrates a concrete access-path problem;
- if an index is justified, add a real migration and plan regression guard;
- make no API contract changes;
- do not combine this performance-validation milestone with a new metrics surface, Prometheus or Grafana.

## 20. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.17.md`

Continue from branch:

`TradeOps/v_1.1.2.17`

Verified final implementation SHA:

`d889cb12135adfae129f8b5039d18b51d0f48b43`

GitHub Actions #115 (`36901598073`) is fully green with 103/103 tests passing.

Do not change existing execution-metrics semantics.

Do not fabricate legacy transition history.

Preserve the v1.1.2.14 event-time index and PostgreSQL query-plan regression guard.
