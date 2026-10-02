# TradeOps — Handoff for v1.1.2.7

## 1. Current status

Active development branch:

```text
TradeOps/v_1.1.2.7
```

Stable predecessor:

```text
TradeOps/v_1.1.2.6
predecessor complete branch HEAD used for branching: b25b305b327c2a6834f3e5f109601506f6ffae43
predecessor authoritative tested code HEAD: 32722394edea186c883f4c57091ee9f7596bb4d4
```

`v1.1.2.7` tested commits:

```text
906161a6e90fdd2f88bf9ab116fb858b1ba3e243
feat: add symbol-scoped execution window metrics

6331d2bf91f2d81d16e8253097cf2b3340787527
test: cover symbol-scoped execution metrics
```

Authoritative tested code HEAD:

```text
6331d2bf91f2d81d16e8253097cf2b3340787527
```

GitHub Actions #102 is fully green:

```text
Restore                                      ✓
Build                                        ✓
Unit tests (70)                              ✓
API + PostgreSQL smoke                       ✓
Symbol-scoped window metrics smoke           ✓
BTC/ETH isolation smoke                      ✓
Symbol normalization smoke                   ✓
Empty-symbol-scope result smoke              ✓
OpenAPI validation                           ✓
Docker Compose validation                    ✓
Docker API/Worker images                     ✓
```

Run metadata:

```text
run number: 102
run id:     36872520046
job id:     110403618391
```

This handoff is added after the tested code HEAD with `[skip ci]` and is documentation-only.

TradeOps remains an execution and automation engineering project. It does not provide alpha, profitable strategies, signals, or return guarantees.

Commercial positioning remains:

```text
You provide the trading rules.
TradeOps provides the execution and automation engineering.
```

---

## 2. Safety and predecessor guarantees preserved

Still in force:

- .NET 8 / PostgreSQL 16;
- `Mock` remains the default exchange provider;
- the only real venue adapter remains `BybitTestnet`;
- no mainnet / real-money support;
- deterministic `ClientOrderId` remains placement identity;
- no blind placement retry after ambiguous outcome;
- no blind cancellation retry after ambiguous outcome;
- persisted order lifecycle history remains unchanged;
- persisted operational-run history remains unchanged;
- `OperationalRunStatuses` remains latest/current projection;
- `OperationalRuns` remains append-per-run history;
- point-in-time execution metrics semantics remain unchanged;
- time-window `[from,to)` semantics remain unchanged;
- persistent risk state remains unchanged;
- cancellation / EmergencyStop behavior remains unchanged;
- no automatic position flattening;
- no strategy / alpha generation;
- accounting, reconciliation and recovery behavior remains unchanged.

No exchange placement/cancellation implementation, risk-decision logic, recovery orchestration, or strategy code was changed in this milestone.

---

## 3. COMPLETED — optional symbol scope for execution-window metrics

Existing endpoint remains:

```text
GET /api/metrics/execution/window?from=...&to=...
```

`v1.1.2.7` adds optional:

```text
symbol
```

Example:

```text
GET /api/metrics/execution/window?from=2026-10-01T10:00:00Z&to=2026-10-01T11:00:00Z&symbol=BTCUSDT
```

Requests without `symbol` preserve the complete `v1.1.2.6` behavior.

The point-in-time endpoint is also unchanged:

```text
GET /api/metrics/execution
```

---

## 4. Symbol normalization

Controller semantics:

```text
symbol == null / empty / whitespace -> no symbol filter
otherwise:
    Trim()
    ToUpperInvariant()
```

Example:

```text
symbol= btcusdt 
```

is normalized to:

```text
BTCUSDT
```

A non-empty normalized symbol longer than 50 characters returns:

```text
HTTP 400 Bad Request
```

The normalized symbol is returned in the window response:

```json
"symbol": "BTCUSDT"
```

For an unscoped request:

```json
"symbol": null
```

A valid symbol with no persisted matching facts returns a normal HTTP 200 response with zero metrics; it is not treated as an error.

---

## 5. Historical semantics remain exact

The existing window remains:

```text
[from, to)
```

The symbol filter does not change the time semantics.

### Signals

Signals are selected by:

```text
TradingSignals.CreatedAt >= from
TradingSignals.CreatedAt <  to
TradingSignals.Symbol == symbol     // when symbol supplied
```

The outcome fields remain explicitly current persisted outcomes of signals received inside the window:

```text
acceptedCurrentOutcome
rejectedCurrentOutcome
pendingCurrentOutcome
```

No signal-outcome history is invented.

### Order lifecycle

Historical order metrics continue to come from:

```text
OrderLifecycleEvents
```

When `symbol` is supplied, lifecycle rows are filtered through an explicit persisted join:

```text
OrderLifecycleEvents.OrderId -> Orders.Id
Orders.Symbol == symbol
```

The current `Orders.Status` is NOT used to reconstruct historical status.

Status counts remain lifecycle-event counts by `OccurredAt` inside `[from,to)`.

### Fills

Fill rows continue to be selected by:

```text
Fills.FilledAt >= from
Fills.FilledAt <  to
```

When `symbol` is supplied, fills are filtered through:

```text
Fills.OrderId -> Orders.Id
Orders.Symbol == symbol
```

`fillsReceived` still means persisted Fill rows, not orders whose snapshot has non-zero filled quantity.

---

## 6. Repository behavior

`IExecutionMetricsRepository.GetWindowAsync` is now:

```text
GetWindowAsync(
    fromInclusive,
    toExclusive,
    symbol = null,
    cancellationToken)
```

`EfExecutionMetricsRepository` preserves two query paths conceptually:

```text
symbol == null
    -> existing time-window filters only

symbol != null
    -> time-window filters + symbol scope
```

The symbol-scoped lifecycle/fill paths use joins to persisted Orders.

The complete historical tables are not materialized into application memory; filtering/grouping remains EF/PostgreSQL-side.

---

## 7. Response shape

Representative scoped response:

```json
{
  "generatedAt": "2026-10-01T14:00:00Z",
  "fromInclusive": "2026-10-01T13:00:00Z",
  "toExclusive": "2026-10-01T14:00:00Z",
  "symbol": "BTCUSDT",
  "signals": {
    "received": 4,
    "acceptedCurrentOutcome": 3,
    "rejectedCurrentOutcome": 1,
    "pendingCurrentOutcome": 0
  },
  "orderLifecycle": {
    "events": 12,
    "ordersTouched": 3,
    "created": 3,
    "submitted": 3,
    "accepted": 0,
    "partiallyFilled": 3,
    "filled": 1,
    "cancelled": 2,
    "rejected": 0,
    "unknown": 0
  },
  "fillsReceived": 0
}
```

Numbers above are illustrative only.

Existing invariants remain:

```text
signals.received ==
    acceptedCurrentOutcome
  + rejectedCurrentOutcome
  + pendingCurrentOutcome
```

and:

```text
orderLifecycle.events == sum(all lifecycle status buckets)
```

---

## 8. Query indexes / migration

New migration:

```text
20261001144000_AddSymbolScopedExecutionMetricsIndexes
```

It adds only indexes:

```text
IX_Orders_Symbol
IX_TradingSignals_Symbol_CreatedAt
IX_Fills_OrderId_FilledAt
```

Existing time-only indexes from `v1.1.2.6` remain:

```text
IX_TradingSignals_CreatedAt
IX_OrderLifecycleEvents_OccurredAt
IX_Fills_FilledAt
```

Existing lifecycle composite index also remains:

```text
IX_OrderLifecycleEvents_OrderId_OccurredAt
```

No rows are rewritten. No table is removed or repurposed.

The intent is:

- unscoped window queries retain efficient time filtering;
- scoped signal queries can use `(Symbol, CreatedAt)`;
- Orders can be located by Symbol for lifecycle/fill joins;
- Fill lookup can use `(OrderId, FilledAt)`.

---

## 9. CI validation

Production commit `906161a6...` was first validated independently in Actions #101:

```text
Restore                         ✓
Build                           ✓
70 unit tests                   ✓
PostgreSQL migration/runtime    ✓
existing full smoke             ✓
Docker Compose                  ✓
Docker images                   ✓
```

Then test-only commit `6331d2bf...` extended the existing smoke with direct symbol behavior.

Actions #102 is fully green and validates:

```text
unscoped window -> symbol == null
symbol=" btcusdt " -> response symbol == BTCUSDT
BTCUSDT signal aggregation
ETHUSDT signal aggregation
BTCUSDT lifecycle isolation
ETHUSDT lifecycle isolation
unscoped signals == BTC signals + ETH signals in CI dataset
unscoped lifecycle events == BTC events + ETH events in CI dataset
SOLUSDT -> zero signals/lifecycle/fills
existing reversed-window -> HTTP 400
all predecessor smoke flows
Docker builds
```

---

## 10. Files changed

Production commit:

```text
src/TradeOps.Api/Controllers/MetricsController.cs
src/TradeOps.Application/Interfaces/IExecutionMetricsRepository.cs
src/TradeOps.Application/Models/ExecutionMetricsSnapshot.cs
src/TradeOps.Infrastructure/Persistence/Configurations/FillConfiguration.cs
src/TradeOps.Infrastructure/Persistence/Configurations/OrderConfiguration.cs
src/TradeOps.Infrastructure/Persistence/Configurations/TradingSignalConfiguration.cs
src/TradeOps.Infrastructure/Persistence/Migrations/20261001144000_AddSymbolScopedExecutionMetricsIndexes.cs
src/TradeOps.Infrastructure/Persistence/Migrations/TradeOpsDbContextModelSnapshot.cs
src/TradeOps.Infrastructure/Persistence/Repositories/EfExecutionMetricsRepository.cs
```

Test-only commit:

```text
.github/workflows/build.yml
```

---

## 11. Definition of Done

- [x] branch created from complete `v1.1.2.6` branch HEAD;
- [x] optional `symbol` added to existing window endpoint;
- [x] unscoped behavior preserved;
- [x] symbol trim/uppercase normalization added;
- [x] normalized symbol exposed in response;
- [x] empty/whitespace symbol behaves as unscoped request;
- [x] oversized symbol returns 400;
- [x] signal filtering uses persisted TradingSignals.Symbol;
- [x] lifecycle symbol filtering uses persisted Order join;
- [x] fill symbol filtering uses persisted Order join;
- [x] no current Orders.Status used for historical reconstruction;
- [x] `[from,to)` semantics preserved;
- [x] query indexes added;
- [x] migration/model snapshot updated;
- [x] 70 tests green;
- [x] PostgreSQL migration/runtime green;
- [x] direct BTC/ETH isolation smoke green;
- [x] normalization smoke green;
- [x] empty scope smoke green;
- [x] predecessor operational/cancellation/risk/recovery flows green;
- [x] Compose green;
- [x] Docker API/Worker images green;
- [x] Mock remains default;
- [x] Bybit remains testnet-only;
- [x] no mainnet introduced;
- [x] no position flattening introduced;
- [x] no strategy/alpha introduced;
- [x] Actions #102 fully green.

`v1.1.2.7` is functionally complete.

---

## 12. Out of scope for v1.1.2.7

Not implemented here:

- time buckets / histogram series;
- grouping by symbol across all symbols in one response;
- signal outcome transition history;
- historical risk-state timeline;
- historical EmergencyStop timeline;
- historical current-order snapshots at arbitrary instants;
- Prometheus exporter;
- Grafana dashboard;
- retention/rollup policy;
- second exchange adapter;
- Binance integration;
- mainnet / real-money execution;
- position flattening;
- trading strategies / alpha generation;
- ML prediction;
- dashboard/mobile UI;
- SaaS multitenancy/billing;
- Kubernetes/HFT architecture.

---

## 13. Recommended next version

Recommended next branch:

```text
TradeOps/v_1.1.2.8
```

Recommended narrow scope:

```text
Fixed time-bucket execution metrics for chart/dashboard consumption
```

Candidate API shape:

```text
GET /api/metrics/execution/series
    ?from=...
    &to=...
    &bucket=5m
    &symbol=BTCUSDT
```

Define bucket semantics before coding. In particular:

- keep `[from,to)` request semantics;
- define bucket boundaries deterministically in UTC;
- only aggregate persisted historical facts;
- do not infer historical Orders.Status from current Orders rows;
- decide a small fixed bucket whitelist such as `1m`, `5m`, `15m`, `1h`, `1d`;
- apply a maximum bucket count to protect the API/database from accidental huge series requests;
- preserve optional symbol scope using the same semantics as `v1.1.2.7`;
- do not add Prometheus/Grafana in the same milestone.

Alternative later scope is grouping one window by all symbols, but do not combine both features implicitly.

---

## 14. Instruction for the next chat

Start with:

> Continue TradeOps. Read `TradeOps-Handsoff-v1.1.2.7.md` first. `v1.1.2.7` is complete. The authoritative tested code HEAD is `6331d2bf91f2d81d16e8253097cf2b3340787527`; GitHub Actions #102 is fully green with 70 unit tests, PostgreSQL migration/runtime smoke, direct BTC/ETH symbol-isolation checks, symbol normalization, zero-result scope validation, OpenAPI/Compose validation and Docker builds. `GET /api/metrics/execution/window?from=...&to=...` now accepts optional `symbol`; symbol is trim/uppercase normalized and echoed in the response. Signals filter directly on TradingSignals.Symbol; lifecycle events and fills use explicit persisted Orders joins for symbol scope. Historical order metrics remain lifecycle-event facts by OccurredAt and never use current Orders.Status to reconstruct history. Migration `20261001144000_AddSymbolScopedExecutionMetricsIndexes` adds symbol/join indexes only. Preserve Mock-default, Bybit-testnet-only, no-mainnet, deterministic ClientOrderId, no-blind placement/cancellation retry, persistent risk state, operational-run history and no-position-flattening boundaries. Recommended next narrow scope is `v1.1.2.8` fixed time-bucket execution metrics with explicit UTC bucket semantics and a bounded bucket count.

No additional context from the previous chat should be required beyond this handoff and repository code.
