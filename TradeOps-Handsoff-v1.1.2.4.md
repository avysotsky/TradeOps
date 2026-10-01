# TradeOps — Handoff for v1.1.2.4

## 1. Current status

Active development branch:

```text
TradeOps/v_1.1.2.4
```

Stable predecessor:

```text
TradeOps/v_1.1.2.3
predecessor HEAD used for branching: 0583d37060f9ff2116525f3dbcccecd368008608
```

`v1.1.2.4` tested code commits:

```text
2c97258065b9f5c70adcae71eed01905e380cb02
feat: add execution metrics API

8c0e846c0e3d708c8e28db47373197415d0069b6
test: validate execution metrics smoke

22e123cade45551faecaa1399853c4e42cc6efd0
test: align metrics smoke with persisted fills
```

Authoritative tested code HEAD:

```text
22e123cade45551faecaa1399853c4e42cc6efd0
```

GitHub Actions #96 is fully green:

```text
Restore                         ✓
Build                           ✓
Unit tests (70)                 ✓
API + PostgreSQL smoke          ✓
Execution metrics smoke         ✓
OpenAPI validation              ✓
Docker Compose validation       ✓
Docker API/Worker images        ✓
```

This handoff is added after the tested code HEAD with `[skip ci]` and is documentation-only.

TradeOps remains an execution and automation engineering project. It does not provide alpha, profitable strategies, trading signals, or profitability guarantees.

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
- no secrets committed or logged;
- deterministic `ClientOrderId` remains the placement identity;
- no blind placement retry after ambiguous outcome;
- no blind cancellation retry after ambiguous outcome;
- persisted lifecycle history from `v1.1.2.3` remains unchanged;
- persisted reconciliation/recovery status from `v1.1.2.3` remains unchanged;
- single-order cancellation remains unchanged;
- bulk cancellation and EmergencyStop open-order cancellation remain unchanged;
- no automatic position flattening;
- no trading-strategy / alpha generation;
- existing accounting, risk, fill persistence, reconciliation and recovery semantics remain in force.

The exchange adapter and order-placement/cancellation execution paths were not redesigned in this milestone.

---

## 3. COMPLETED — execution / operational metrics API

New operator endpoint:

```text
GET /api/metrics/execution
```

The endpoint is read-only and derives a point-in-time operational snapshot from existing PostgreSQL state plus configured risk defaults where no persisted risk row exists.

No new database table or migration was required.

New application contract:

```text
IExecutionMetricsRepository
```

New EF implementation:

```text
EfExecutionMetricsRepository
```

New API controller:

```text
MetricsController
```

DI registration was added in `TradeOps.Api/Program.cs`.

---

## 4. Response shape

The endpoint returns:

```json
{
  "generatedAt": "...",
  "signals": {
    "received": 0,
    "accepted": 0,
    "rejected": 0,
    "pending": 0
  },
  "orders": {
    "total": 0,
    "created": 0,
    "submitted": 0,
    "accepted": 0,
    "partiallyFilled": 0,
    "filled": 0,
    "cancelled": 0,
    "rejected": 0,
    "unknown": 0
  },
  "fillsReceived": 0,
  "risk": {
    "tradingEnabled": true,
    "emergencyStop": false
  },
  "operations": {
    "lastReconciliationSucceeded": null,
    "lastRecoverySucceeded": null,
    "lastRecoveryPositionMismatches": null
  }
}
```

The actual values are computed at request time.

---

## 5. Signal metric semantics

Signal metrics are derived from persisted `TradingSignals` rows.

Persisted outcomes remain:

```text
Received
Accepted
Rejected
```

The API exposes:

```text
accepted = count(Outcome == Accepted)
rejected = count(Outcome == Rejected)
pending  = count(Outcome == Received)
received = accepted + rejected + pending
```

Therefore `received` is the total number of persisted signals, not only rows whose current outcome is literally `Received`.

Invariant:

```text
signals.received == signals.accepted + signals.rejected + signals.pending
```

---

## 6. Order metric semantics

Order metrics are derived from the **current persisted `Orders.Status` values**.

Buckets:

```text
Created
Submitted
Accepted
PartiallyFilled
Filled
Cancelled
Rejected
Unknown
```

Invariant:

```text
orders.total == sum(all status buckets)
```

These are current-state counts.

They are **not** counts of historical lifecycle transitions. For example, an order currently `Filled` contributes to `filled`, but does not also contribute to `created`, `submitted`, or `partiallyFilled` merely because it passed through those states historically.

Use the lifecycle-history endpoint when the historical sequence is needed:

```text
GET /api/orders/local/{idOrClientOrderId}/history
```

---

## 7. Fill metric semantics

```text
fillsReceived = count(Fills rows in PostgreSQL)
```

This is intentionally a persistence metric.

It does **not** mean "number of orders whose current snapshot contains a non-zero FilledQuantity".

Important tested caveat:

The `Mock` execution path can move persisted order snapshots through partial/final fill states without creating `Fill` rows unless the execution-event fill-persistence path is involved.

Therefore a valid runtime snapshot can contain, for example:

```text
orders.filled > 0
fillsReceived = 0
```

Actions #95 exposed an incorrect smoke-test assumption that the mock scenario had to persist four fills. The endpoint itself returned correct persisted data. The assertion was corrected in commit:

```text
22e123cade45551faecaa1399853c4e42cc6efd0
```

No production execution semantics were changed by that correction.

---

## 8. Risk metric semantics

The endpoint reads the persisted `OperationalRiskState` row with:

```text
Id = OperationalRiskState.DefaultId
```

and exposes:

```text
TradingEnabled
EmergencyStop
```

If no persisted risk-state row exists yet, the endpoint falls back to configured `RiskSettings` values.

The metrics request itself does not create or mutate risk state.

---

## 9. Reconciliation / recovery metric semantics

The endpoint reads the existing persisted `OperationalRunStatuses` rows for:

```text
OrderReconciliation
RecoveryCycle
```

and exposes:

```text
lastReconciliationSucceeded
lastRecoverySucceeded
lastRecoveryPositionMismatches
```

Before a corresponding run has ever been persisted, the success field is:

```text
null
```

`lastRecoveryPositionMismatches` is also nullable before a recovery run exists.

Important semantic boundary:

```text
lastRecoveryPositionMismatches
```

is the mismatch count recorded by the **last persisted recovery cycle**.

It is not guaranteed to be the number of position mismatches that are actively unresolved at the instant the metrics endpoint is called.

For that reason `v1.1.2.4` deliberately does **not** expose the misleading name:

```text
ActivePositionMismatchCount
```

without a persistence/read model that can support that stronger claim.

---

## 10. Database / migration impact

No migration was added in `v1.1.2.4`.

Existing tables are read as-is:

```text
TradingSignals
Orders
Fills
OperationalRiskState
OperationalRunStatuses
```

No predecessor table was removed, repurposed, or rewritten.

---

## 11. CI history for this milestone

### Actions #94

Production code built and all 70 unit tests passed.

The API/PostgreSQL smoke failed because the newly added Python assertion contained invalid multiline syntax.

This was a CI-test bug, not a production compile/runtime regression.

### Actions #95

Build and all 70 unit tests passed again.

The execution metrics endpoint itself returned a valid runtime snapshot:

```text
signals:  received=5, accepted=4, rejected=1, pending=0
orders:   total=4, filled=1, cancelled=3
fills:    fillsReceived=0
risk:     TradingEnabled=true, EmergencyStop=false
ops:      LastReconciliationSucceeded=true
          LastRecoverySucceeded=true
          LastRecoveryPositionMismatches=1
```

The smoke failed only because it incorrectly assumed:

```text
fillsReceived >= 4
```

That assumption contradicted the existing fill-persistence boundary for the Mock scenario.

### Actions #96 — authoritative

Fully green on tested code HEAD `22e123cade45551faecaa1399853c4e42cc6efd0`:

```text
Restore                     ✓
Build                       ✓
70 unit tests               ✓
API + PostgreSQL smoke      ✓
Metrics endpoint smoke      ✓
OpenAPI route validation    ✓
Docker Compose              ✓
Docker API/Worker images    ✓
```

---

## 12. Runtime coverage added

Actions #96 verifies that OpenAPI contains:

```text
/api/metrics/execution
```

After the existing full PostgreSQL smoke workflow has created accepted/rejected signals, filled/cancelled orders, persisted risk state, reconciliation status and Worker recovery status, CI calls:

```text
GET /api/metrics/execution
```

and verifies:

```text
generatedAt exists
received == accepted + rejected + pending
accepted >= 4
rejected >= 1
orders.total == sum(status buckets)
filled >= 1
cancelled >= 3
fillsReceived is a non-negative integer
EmergencyStop == false
TradingEnabled is boolean
LastReconciliationSucceeded == true
LastRecoverySucceeded == true
LastRecoveryPositionMismatches is integer
```

This tests the EF aggregation against a real PostgreSQL service rather than an in-memory approximation.

---

## 13. Files added / changed

Added:

```text
src/TradeOps.Application/Interfaces/IExecutionMetricsRepository.cs
src/TradeOps.Application/Models/ExecutionMetricsSnapshot.cs
src/TradeOps.Infrastructure/Persistence/Repositories/EfExecutionMetricsRepository.cs
src/TradeOps.Api/Controllers/MetricsController.cs
```

Changed:

```text
src/TradeOps.Api/Program.cs
.github/workflows/build.yml
```

No migration files were added.

---

## 14. Definition of Done for v1.1.2.4 — COMPLETE

- [x] branch created from complete `v1.1.2.3` HEAD;
- [x] read-only execution metrics endpoint exists;
- [x] signal counters derive from persisted TradingSignals;
- [x] pending signal count is explicit;
- [x] total signal invariant is defined and tested;
- [x] current persisted order-status buckets are exposed;
- [x] order total invariant is defined and tested;
- [x] persisted Fill-row count is exposed;
- [x] persistent risk state is exposed without write side effects;
- [x] configured risk defaults are used when no persisted row exists;
- [x] last reconciliation success is exposed;
- [x] last recovery success is exposed;
- [x] last recovery mismatch count is exposed with accurate semantics;
- [x] misleading active-mismatch claim was avoided;
- [x] no database migration required;
- [x] OpenAPI includes the endpoint;
- [x] runtime PostgreSQL smoke covers the endpoint;
- [x] 70 unit tests green;
- [x] predecessor execution/cancellation/risk/recovery smoke remains green;
- [x] Docker Compose validates;
- [x] API/Worker Docker images build;
- [x] Mock remains default;
- [x] Bybit remains testnet-only;
- [x] no mainnet support introduced;
- [x] no Prometheus/Grafana introduced;
- [x] no automatic position flattening introduced;
- [x] no trading strategy introduced;
- [x] Actions #96 fully green.

`v1.1.2.4` is functionally complete. Do not add additional capabilities to this version without explicitly changing its milestone scope.

---

## 15. Out of scope for v1.1.2.4

Not implemented here:

- Prometheus exporter;
- Grafana dashboard;
- time-series metrics storage;
- time-window filters such as last hour/day;
- historical list of every reconciliation/recovery run;
- guaranteed active/unresolved position-mismatch count;
- automatic position flattening;
- second exchange adapter;
- Binance integration;
- mainnet / real-money execution;
- trading strategies / alpha generation;
- ML prediction;
- generalized FX fee conversion;
- dashboard/mobile UI;
- SaaS multitenancy/billing;
- Kubernetes/HFT architecture.

---

## 16. CURRENT NEXT TASK — define v1.1.2.5 before coding

Recommended next version name:

```text
TradeOps/v_1.1.2.5
```

Do not automatically expand the snapshot endpoint into Prometheus/Grafana or time-series infrastructure.

A reasonable next narrow milestone, if operational observability remains the priority, is to define one of these explicitly before coding:

```text
A) historical operational-run query API
or
B) time-windowed execution metrics with clearly defined persistence semantics
```

Do not implement both implicitly in one milestone.

Any future "active position mismatch" metric requires a source that represents current unresolved mismatch state; the last recovery-cycle mismatch count is not sufficient for that claim.

---

## 17. Instruction for the next chat

Start with:

> Continue TradeOps. Read `TradeOps-Handsoff-v1.1.2.4.md` first. `v1.1.2.4` is complete. Branch `TradeOps/v_1.1.2.4` adds the read-only `GET /api/metrics/execution` endpoint backed by PostgreSQL aggregates over TradingSignals, current Orders statuses, persisted Fills, OperationalRiskState and OperationalRunStatuses. The authoritative tested code HEAD is `22e123cade45551faecaa1399853c4e42cc6efd0`; Actions #96 is fully green with 70 unit tests, real PostgreSQL API/metrics smoke, Compose validation and Docker image builds. Order metrics are current-state buckets, not lifecycle transition counts. `fillsReceived` counts persisted Fill rows and can legitimately remain zero in a Mock order-state scenario. `lastRecoveryPositionMismatches` is a last-run metric, not a guaranteed current active-mismatch count. No migration, Prometheus/Grafana, exchange/mainnet, flattening, or strategy logic was added. Preserve all predecessor execution safety boundaries. Define `v1.1.2.5` narrowly before coding.

No additional context from the previous chat should be required beyond this handoff and repository code.
