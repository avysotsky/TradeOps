# TradeOps — Handoff for v1.1.2.6

## 1. Current status

Active development branch:

```text
TradeOps/v_1.1.2.6
```

Stable predecessor:

```text
TradeOps/v_1.1.2.5
predecessor branch HEAD used for branching: f5b5e0e344bcda46294b69e7b9b7153eeb299ee7
predecessor authoritative tested code HEAD: d8e078ce6279439b0dcbfb65cede4caa1924cce3
```

`v1.1.2.6` tested production commit:

```text
32722394edea186c883f4c57091ee9f7596bb4d4
feat: add time-windowed execution metrics
```

Authoritative tested code HEAD:

```text
32722394edea186c883f4c57091ee9f7596bb4d4
```

GitHub Actions #99 is fully green:

```text
Restore                              ✓
Build                                ✓
Unit tests (70)                      ✓
API + PostgreSQL smoke               ✓
Time-windowed metrics smoke          ✓
Invalid-window HTTP 400 smoke        ✓
OpenAPI validation                   ✓
Docker Compose validation            ✓
Docker API/Worker images             ✓
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
- persisted operational-run history from `v1.1.2.5` remains unchanged;
- `OperationalRunStatuses` remains latest/current projection;
- `OperationalRuns` remains append-per-run history;
- existing point-in-time execution metrics endpoint remains unchanged;
- persistent risk state remains unchanged;
- single/bulk cancellation and EmergencyStop behavior remain unchanged;
- no automatic position flattening;
- no strategy / alpha generation;
- existing accounting, reconciliation and recovery behavior remains in force.

No exchange placement/cancellation implementation, risk-decision logic, recovery orchestration, or strategy code was changed in this milestone.

---

## 3. COMPLETED — time-windowed execution metrics

New read-only endpoint:

```text
GET /api/metrics/execution/window?from=...&to=...
```

Example:

```text
GET /api/metrics/execution/window?from=2026-10-01T10:00:00Z&to=2026-10-01T11:00:00Z
```

The existing endpoint is intentionally preserved without semantic changes:

```text
GET /api/metrics/execution
```

Do not merge the two meanings in future work:

```text
/api/metrics/execution        = point-in-time/current-state snapshot
/api/metrics/execution/window = persisted facts inside a historical time window
```

---

## 4. Window boundary semantics

Both query parameters are required:

```text
from
to
```

They are parsed as `DateTimeOffset` and normalized to UTC before querying PostgreSQL.

The window is a half-open interval:

```text
[from, to)
```

Meaning:

```text
timestamp >= from
timestamp <  to
```

This is deliberate so adjacent windows can be combined without double-counting a record exactly on a boundary.

Invalid input:

```text
from is missing
or
to is missing
or
from >= to
```

returns:

```text
HTTP 400 Bad Request
```

There is no hidden default such as “last 24 hours”.

There is no arbitrary maximum historical range in this version.

---

## 5. Why current Orders.Status is NOT used

`v1.1.2.4` point-in-time metrics count current persisted `Orders.Status` buckets.

That is correct for a current snapshot, but it is not historically valid for an earlier time window. The current state of an order cannot prove what its state was at a prior instant.

Therefore `v1.1.2.6` deliberately does **not** query `Orders.Status` for windowed order metrics.

Instead it uses persisted:

```text
OrderLifecycleEvents.OccurredAt
```

This keeps the historical claim aligned with facts that TradeOps actually persisted.

---

## 6. Signal window semantics

Signal inclusion is based on:

```text
TradingSignals.CreatedAt >= from
TradingSignals.CreatedAt <  to
```

The response exposes:

```text
signals.received
signals.acceptedCurrentOutcome
signals.rejectedCurrentOutcome
signals.pendingCurrentOutcome
```

Invariant:

```text
received == acceptedCurrentOutcome
          + rejectedCurrentOutcome
          + pendingCurrentOutcome
```

Important semantic boundary:

The time window selects signals by when the signal row was created/received.

TradeOps does not currently persist a separate timestamped signal-outcome transition history. Therefore the Accepted/Rejected/Pending breakdown is explicitly named **CurrentOutcome**: it is the current persisted outcome of signals that were received inside the requested window.

Do not rename these fields to imply that TradeOps knows the exact timestamp at which a signal outcome changed unless outcome-history persistence is added later.

---

## 7. Order lifecycle window semantics

Order metrics are derived from:

```text
OrderLifecycleEvents
```

filtered by:

```text
OccurredAt >= from
OccurredAt <  to
```

The response exposes:

```text
orderLifecycle.events
orderLifecycle.ordersTouched
orderLifecycle.created
orderLifecycle.submitted
orderLifecycle.accepted
orderLifecycle.partiallyFilled
orderLifecycle.filled
orderLifecycle.cancelled
orderLifecycle.rejected
orderLifecycle.unknown
```

`events` is the total count of persisted lifecycle-event rows in the window.

Each status bucket is the count of lifecycle events whose resulting persisted `Status` equals that bucket.

Invariant:

```text
orderLifecycle.events ==
    created
  + submitted
  + accepted
  + partiallyFilled
  + filled
  + cancelled
  + rejected
  + unknown
```

`ordersTouched` is:

```text
COUNT(DISTINCT OrderId)
```

for lifecycle events inside the window.

One order may contribute multiple lifecycle events and multiple status buckets in the same window.

The lifecycle audit records persisted TradeOps state changes. It does not claim that every exchange-side micro-event was observed.

---

## 8. Fill window semantics

Fill metric inclusion is based on:

```text
Fills.FilledAt >= from
Fills.FilledAt <  to
```

The response exposes:

```text
fillsReceived
```

and its meaning remains:

```text
count of persisted Fill rows in the requested window
```

This is a persistence metric, not a count of orders with non-zero `FilledQuantity`.

The existing Mock caveat remains valid: order snapshots can move through filled states without a persisted `Fill` row unless the fill-persistence path is involved.

Therefore this can be valid:

```text
orderLifecycle.filled > 0
fillsReceived = 0
```

---

## 9. Response shape

Representative shape:

```json
{
  "generatedAt": "2026-10-01T11:00:01Z",
  "fromInclusive": "2026-10-01T10:00:00Z",
  "toExclusive": "2026-10-01T11:00:00Z",
  "signals": {
    "received": 5,
    "acceptedCurrentOutcome": 4,
    "rejectedCurrentOutcome": 1,
    "pendingCurrentOutcome": 0
  },
  "orderLifecycle": {
    "events": 20,
    "ordersTouched": 4,
    "created": 4,
    "submitted": 4,
    "accepted": 0,
    "partiallyFilled": 4,
    "filled": 1,
    "cancelled": 3,
    "rejected": 0,
    "unknown": 0
  },
  "fillsReceived": 0
}
```

Numbers above are illustrative; runtime values depend on persisted data.

Risk/current operational status fields are intentionally not copied into the historical-window response because their existing persistence models describe current/latest state rather than historical state across arbitrary windows.

---

## 10. Application / infrastructure changes

`IExecutionMetricsRepository` now contains:

```text
GetAsync(...)
GetWindowAsync(fromInclusive, toExclusive, ...)
```

New application models:

```text
WindowSignalExecutionMetrics
WindowOrderLifecycleMetrics
ExecutionMetricsWindowSnapshot
```

`EfExecutionMetricsRepository.GetWindowAsync(...)` performs all filtering/aggregation in PostgreSQL through EF Core.

It does not materialize the complete signal/lifecycle/fill tables into application memory.

`MetricsController` exposes the new route and validates required/range semantics before querying the repository.

No additional service/DI registration was required because the existing `IExecutionMetricsRepository` registration is reused.

---

## 11. Migration / query indexes

New migration:

```text
20261001133000_AddExecutionMetricsWindowIndexes
```

It adds only indexes:

```text
IX_TradingSignals_CreatedAt
IX_OrderLifecycleEvents_OccurredAt
IX_Fills_FilledAt
```

No existing rows are rewritten.

No table is removed or repurposed.

The corresponding EF configurations and `TradeOpsDbContextModelSnapshot` were updated.

These indexes directly support the new time-range predicates and avoid making growing historical metric queries depend on full-table scans.

---

## 12. CI validation

GitHub Actions run:

```text
#99
run id: 36869972748
job id: 110394934308
```

Tested code HEAD:

```text
32722394edea186c883f4c57091ee9f7596bb4d4
```

Validated successfully:

```text
Restore
Build
70 unit tests
EF migration startup against PostgreSQL 16
existing signal/order/fill/risk/reconciliation/recovery smoke
existing operational-run-history smoke
OpenAPI contains /api/metrics/execution/window
valid [from,to) execution-window query
signal CurrentOutcome invariant
order lifecycle event-bucket invariant
ordersTouched historical aggregation
fill window aggregation
invalid reversed window -> HTTP 400
Docker Compose validation
API Docker image build
Worker Docker image build
```

Actions #99 is fully green.

---

## 13. Files changed in tested code commit

```text
.github/workflows/build.yml
src/TradeOps.Api/Controllers/MetricsController.cs
src/TradeOps.Application/Interfaces/IExecutionMetricsRepository.cs
src/TradeOps.Application/Models/ExecutionMetricsSnapshot.cs
src/TradeOps.Infrastructure/Persistence/Configurations/FillConfiguration.cs
src/TradeOps.Infrastructure/Persistence/Configurations/OrderLifecycleEventConfiguration.cs
src/TradeOps.Infrastructure/Persistence/Configurations/TradingSignalConfiguration.cs
src/TradeOps.Infrastructure/Persistence/Migrations/20261001133000_AddExecutionMetricsWindowIndexes.cs
src/TradeOps.Infrastructure/Persistence/Migrations/TradeOpsDbContextModelSnapshot.cs
src/TradeOps.Infrastructure/Persistence/Repositories/EfExecutionMetricsRepository.cs
```

---

## 14. Definition of Done

- [x] `TradeOps/v_1.1.2.6` created from complete `v1.1.2.5` branch HEAD;
- [x] existing point-in-time metrics preserved;
- [x] historical semantics defined before coding;
- [x] no historical order state inferred from current `Orders.Status`;
- [x] new window endpoint added;
- [x] `from` and `to` required;
- [x] UTC normalization added;
- [x] `[from,to)` semantics added;
- [x] invalid/missing boundaries return 400;
- [x] signals filtered by `CreatedAt`;
- [x] signal current-outcome naming explicit;
- [x] order metrics derived from lifecycle events;
- [x] lifecycle status-bucket invariant defined;
- [x] distinct orders-touched metric added;
- [x] fills filtered by `FilledAt`;
- [x] time-query indexes added;
- [x] migration/model snapshot updated;
- [x] OpenAPI route validation green;
- [x] PostgreSQL runtime smoke green;
- [x] 70 unit tests green;
- [x] predecessor operational-history smoke green;
- [x] predecessor cancellation/risk/recovery flows green;
- [x] Docker Compose green;
- [x] API/Worker images green;
- [x] Mock remains default;
- [x] Bybit remains testnet-only;
- [x] no mainnet support introduced;
- [x] no automatic position flattening introduced;
- [x] no strategy/alpha logic introduced;
- [x] Actions #99 fully green.

`v1.1.2.6` is functionally complete.

---

## 15. Out of scope for v1.1.2.6

Not implemented here:

- time buckets / histogram series;
- optional symbol filtering for window metrics;
- signal outcome transition history;
- historical risk-state timeline;
- historical EmergencyStop timeline;
- historical current-order snapshots at arbitrary instants;
- Prometheus exporter;
- Grafana dashboard;
- retention/rollup policy;
- cursor pagination for metrics;
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

## 16. Recommended next version

Recommended next branch:

```text
TradeOps/v_1.1.2.7
```

Recommended narrow scope:

```text
Symbol-scoped execution window metrics
```

Possible extension:

```text
GET /api/metrics/execution/window?from=...&to=...&symbol=BTCUSDT
```

If implemented, keep the same historical semantics:

- signals can filter directly by `TradingSignals.Symbol`;
- lifecycle events need an explicit persisted-order join to obtain symbol rather than guessing it from current status;
- fills need an order join for symbol;
- do not add time buckets, Prometheus/Grafana, or a second exchange in the same milestone unless explicitly re-scoped.

Alternative future scope, but not in the same milestone, is fixed time-bucket aggregation for dashboard/chart consumption.

---

## 17. Instruction for the next chat

Start with:

> Continue TradeOps. Read `TradeOps-Handsoff-v1.1.2.6.md` first. `v1.1.2.6` is complete. The authoritative tested code HEAD is `32722394edea186c883f4c57091ee9f7596bb4d4`; GitHub Actions #99 is fully green with 70 unit tests, PostgreSQL migration/runtime smoke, direct time-windowed execution metrics checks, invalid-window validation, OpenAPI validation, Compose validation and Docker builds. `GET /api/metrics/execution` keeps its current point-in-time semantics. New `GET /api/metrics/execution/window?from=...&to=...` uses required UTC-normalized `[from,to)` boundaries; signals are selected by `CreatedAt` and grouped by current persisted outcome with explicit CurrentOutcome naming; historical order metrics are lifecycle-event counts by `OccurredAt`, never inferred from current `Orders.Status`; fills are persisted Fill rows selected by `FilledAt`. Migration `20261001133000_AddExecutionMetricsWindowIndexes` adds time-column indexes only. Preserve Mock-default, Bybit-testnet-only, no-mainnet, deterministic ClientOrderId, no-blind placement/cancellation retry, persistent risk state, operational-run history and no-position-flattening boundaries. Recommended next narrow scope is `v1.1.2.7` symbol-scoped execution window metrics.

No additional context from the previous chat should be required beyond this handoff and repository code.
