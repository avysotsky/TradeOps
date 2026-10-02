# TradeOps handoff — v1.1.2.16

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.16`

Verified implementation commit:

`5527ec314726c59f4de1527007351fe666493609`

Commit message:

`feat: add signal transition metrics by symbol`

GitHub Actions validation:

- workflow: `build`
- run number: **113**
- run id: `36900505196`
- job id: `110498172107`
- result: **success**
- unit tests: **102 passed / 102 total**

Successful steps:

- Restore
- Build
- Unit tests, including PostgreSQL-backed signal-transition by-symbol coverage
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

Build completed with 0 warnings and 0 errors.

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.16

The narrow goal from `TradeOps-Handsoff-v1.1.2.15.md` is complete:

**Bounded signal-outcome transition metrics grouped by symbol, based only on persisted outcome-event time.**

No trading/execution behavior changed.

No established execution-metrics semantics changed.

No database migration or new index was added in v1.1.2.16.

## 3. New endpoint

Added:

`GET /api/metrics/signal-transitions/by-symbol?from=...&to=...&limit=...`

Required query parameters:

- `from`
- `to`

Optional:

- `limit`

Limit rules:

- default: `20`
- minimum: `1`
- maximum: `100`

Window semantics are `[from,to)` and timestamps are normalized to UTC.

## 4. Time-axis semantics

The by-symbol transition metrics use only persisted rows from:

`TradingSignalOutcomeEvents`

selected by:

`TradingSignalOutcomeEvents.OccurredAt`

The parent `TradingSignals` table is joined only to obtain `Symbol`.

The query does not use `TradingSignals.CreatedAt` as transition time.

The query does not reconstruct historical transitions from the current `TradingSignals.Outcome` projection.

## 5. Response contract

Each symbol item contains:

- `symbol`
- `totalTransitions`
- `receivedTransitions`
- `acceptedTransitions`
- `rejectedTransitions`

The top-level response contains:

- `generatedAt`
- `fromInclusive`
- `toExclusive`
- `limit`
- `isTruncated`
- `symbols`

Ordering is deterministic:

1. `totalTransitions` descending;
2. `symbol` ascending.

## 6. Bounded result / truncation semantics

The PostgreSQL query fetches:

`limit + 1`

rows.

If the extra row exists:

- `isTruncated = true`;
- the extra row is removed before returning the response.

Otherwise:

- `isTruncated = false`.

This avoids a separate count query while still giving the operator an explicit bounded-result signal.

## 7. PostgreSQL aggregation

`EfSignalTransitionMetricsRepository.GetBySymbolAsync(...)` performs aggregation in PostgreSQL.

Core query semantics:

```sql
SELECT
    signal."Symbol" AS symbol,
    COUNT(*) AS total_transitions,
    COUNT(*) FILTER (WHERE transition."Outcome" = 'Received') AS received,
    COUNT(*) FILTER (WHERE transition."Outcome" = 'Accepted') AS accepted,
    COUNT(*) FILTER (WHERE transition."Outcome" = 'Rejected') AS rejected
FROM "TradingSignalOutcomeEvents" AS transition
JOIN "TradingSignals" AS signal
    ON signal."Id" = transition."TradingSignalId"
WHERE transition."OccurredAt" >= @fromInclusive
  AND transition."OccurredAt" < @toExclusive
GROUP BY signal."Symbol"
ORDER BY total_transitions DESC, symbol ASC
LIMIT @fetchLimit;
```

Raw transition events are not materialized in application memory.

## 8. Controller validation

`SignalTransitionMetricsController` now also exposes `GetBySymbolAsync(...)`.

Controller coverage verifies:

- UTC window normalization;
- default limit `20`;
- limit `0` rejected;
- limit `101` rejected;
- repository is not called for invalid limits.

Window and series transition endpoints remain unchanged.

## 9. PostgreSQL integration coverage

Added:

`tests/TradeOps.UnitTests/SignalTransitionMetricsBySymbolPostgresTests.cs`

The test applies real migrations to an isolated PostgreSQL database and verifies:

- aggregation uses persisted outcome events in `[from,to)`;
- BTCUSDT with four events ranks first;
- ETHUSDT and SOLUSDT tie at two events and are ordered alphabetically;
- `limit=2` returns two rows and `isTruncated=true`;
- a larger limit returns all three populated symbols with `isTruncated=false`;
- an event immediately before `from` is excluded;
- an event exactly at `to` is excluded;
- a legacy XRPUSDT current `Accepted` projection with no outcome-history rows does not appear;
- deterministic received / accepted / rejected counts are preserved.

This test passed in GitHub Actions #113.

## 10. Documentation

Added:

`docs/SignalTransitionMetricsBySymbol.md`

It documents:

- endpoint and bounds;
- default/max limit;
- event-time source;
- per-symbol counts;
- deterministic ordering;
- `limit + 1` truncation contract;
- legacy historical incompleteness;
- separation from existing execution metrics.

## 11. Existing transition metrics remain available

Window:

`GET /api/metrics/signal-transitions/window`

Series:

`GET /api/metrics/signal-transitions/series`

By symbol:

`GET /api/metrics/signal-transitions/by-symbol`

All three use persisted `TradingSignalOutcomeEvents.OccurredAt` as their historical time axis.

## 12. Existing execution metrics remain unchanged

The established execution metrics still preserve their existing contracts:

- `GET /api/metrics/execution`
- `GET /api/metrics/execution/window`
- `GET /api/metrics/execution/series`
- `GET /api/metrics/execution/by-symbol`

Do not silently mix signal creation/current-outcome semantics with outcome-event-time transition metrics.

## 13. Historical coverage limitation remains

Outcome transition history was introduced in v1.1.2.12 without synthetic backfill.

Pre-v1.1.2.12 signals may have a current terminal projection while having no persisted outcome-history rows.

Therefore:

- absent symbols or zero counts mean no persisted event exists in the requested scope;
- they do not prove that no transition happened before truthful history persistence existed;
- do not fabricate legacy transition timestamps or counts from current projections.

## 14. Indexing/performance baseline remains

v1.1.2.14 added:

`IX_TradingSignalOutcomeEvents_OccurredAt`

plus representative PostgreSQL plan regression coverage.

That event-time index remains in force and all its regression tests pass in Actions #113.

No symbol-oriented transition index was added in v1.1.2.16 because no representative query-plan evidence justified one yet.

## 15. Files changed in the verified implementation commit

Modified:

- `src/TradeOps.Application/Interfaces/ISignalTransitionMetricsRepository.cs`
- `src/TradeOps.Application/Models/SignalTransitionMetricsWindowSnapshot.cs`
- `src/TradeOps.Api/Controllers/SignalTransitionMetricsController.cs`
- `src/TradeOps.Infrastructure/Persistence/Repositories/EfSignalTransitionMetricsRepository.cs`
- `tests/TradeOps.UnitTests/SignalTransitionMetricsControllerTests.cs`

Added:

- `tests/TradeOps.UnitTests/SignalTransitionMetricsBySymbolPostgresTests.cs`
- `docs/SignalTransitionMetricsBySymbol.md`

No migration was added.

## 16. Safety and project boundaries preserved

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

## 17. Known limitations after v1.1.2.16

- transition history remains incomplete for pre-v1.1.2.12 legacy signals;
- no `symbol x bucket` transition matrix;
- runtime CI OpenAPI/smoke assertions do not yet explicitly require and call all three transition-metrics routes;
- no formal production latency SLO;
- no materialized metrics rollups;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboard;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 18. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.17`

Recommended narrow scope:

**Runtime/OpenAPI CI hardening for the complete signal-transition metrics surface.**

Suggested work:

- require these routes in generated OpenAPI during CI:
  - `/api/metrics/signal-transitions/window`
  - `/api/metrics/signal-transitions/series`
  - `/api/metrics/signal-transitions/by-symbol`;
- exercise each route against the real CI PostgreSQL/API runtime after deterministic test signals have been persisted;
- validate JSON shape and nonnegative transition counters;
- validate series bucket structure and bound behavior without making brittle wall-clock assumptions;
- validate by-symbol response limit and item structure;
- preserve all existing execution-metrics smoke assertions;
- make no production API/schema changes unless the smoke exposes a real defect;
- do not combine this hardening milestone with Prometheus/Grafana or a new feature surface.

## 19. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.16.md`

Continue from branch:

`TradeOps/v_1.1.2.16`

Verified implementation SHA:

`5527ec314726c59f4de1527007351fe666493609`

GitHub Actions #113 (`36900505196`) is fully green with 102/102 tests passing.

Do not change existing execution-metrics semantics.

Do not fabricate legacy transition history.

Preserve the v1.1.2.14 event-time index and PostgreSQL query-plan regression guard.
