# TradeOps — Handoff for v1.1.2.3

## 1. Current status

Active development branch:

```text
TradeOps/v_1.1.2.3
```

Stable predecessor:

```text
TradeOps/v_1.1.2.2
predecessor HEAD used for branching: 1405ae31afcdf1dd2b11415d29f73e875b838080
```

`v1.1.2.3` tested code commits:

```text
70497658a4fc1a290ebf8c92e96e5c191d3145a7
feat: add order lifecycle and operational run visibility

eb30ba8977bab41d66843d9ad566aa80af4b2b73
test: adapt recovery fixtures for operational visibility

4293b35051b20183f6ce7a3f35715d85053f1fb3
test: cover lifecycle and recovery visibility

47d5b3e952a027f6d01345bf8e31f4996c3449e3
test: accept zero local position snapshots in recovery smoke
```

Authoritative tested code HEAD:

```text
47d5b3e952a027f6d01345bf8e31f4996c3449e3
```

GitHub Actions #93 is fully green:

```text
Restore                         ✓
Build                           ✓
Unit tests (70)                 ✓
API + PostgreSQL smoke          ✓
Lifecycle history smoke         ✓
Reconciliation status smoke     ✓
Worker recovery status smoke    ✓
Single cancellation regression  ✓
Bulk cancellation regression    ✓
EmergencyStop regression        ✓
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
- single-order cancellation from `v1.1.2.1` remains unchanged;
- bulk cancellation and EmergencyStop open-order cancellation from `v1.1.2.2` remain unchanged;
- no automatic position flattening;
- no trading-strategy / alpha generation;
- existing accounting, risk, fill persistence, reconciliation and recovery semantics remain in force.

The Bybit adapter and order-placement execution path were not redesigned in this milestone.

---

## 3. COMPLETED — persisted order lifecycle history

New operator endpoint:

```text
GET /api/orders/local/{idOrClientOrderId}/history
```

`idOrClientOrderId` may be either:

- local PostgreSQL `Order.Id` (`Guid`); or
- deterministic `ClientOrderId`.

New persistence table:

```text
OrderLifecycleEvents
```

Each row contains a persisted order-state snapshot:

```text
Id
OrderId
ClientOrderId
PreviousStatus
Status
FilledQuantity
AverageFillPrice
ExchangeOrderId
Source
OccurredAt
```

The audit is append-only at the repository boundary.

---

## 4. Lifecycle persistence semantics

`EfOrderRepository` now records lifecycle events when:

1. an order is first persisted through `TryAddAsync`;
2. `UpdateAsync` observes a meaningful change in one or more of:

```text
Status
FilledQuantity
AverageFillPrice
ExchangeOrderId
```

A newly persisted order produces an initial event:

```text
Status = Created
Source = Created
```

Subsequent meaningful persisted changes use:

```text
Source = PersistenceUpdate
```

Typical tested flow:

```text
Created
  ↓
Submitted
  ↓
PartiallyFilled
  ↓
Filled
```

or:

```text
Created
  ↓
Submitted
  ↓
PartiallyFilled
  ↓
Cancelled
```

Important limitation: lifecycle history records TradeOps **persisted snapshots**. It is not a guarantee that every exchange-side micro-event or transient state was observed. TradeOps does not invent exchange events that were never received or persisted.

---

## 5. LegacySnapshot behavior

Orders created before lifecycle auditing existed may have no rows in `OrderLifecycleEvents`.

For such an existing local order, the history endpoint returns one explicit synthetic current-state record:

```text
Id     = 00000000-0000-0000-0000-000000000000
Source = LegacySnapshot
```

The timestamp is the order's current persisted `UpdatedAt`.

This is deliberately **not** presented as historical reconstruction. It means only:

> this is the current persisted state of a legacy order for which TradeOps has no lifecycle audit rows.

If the local order itself does not exist, the endpoint returns HTTP `404`.

---

## 6. COMPLETED — persisted operational run visibility

The API and Worker are separate processes, so run visibility is persisted in PostgreSQL rather than stored only in memory.

New table:

```text
OperationalRunStatuses
```

This table stores the current/last known status for each run type.

Current run types:

```text
OrderReconciliation
RecoveryCycle
```

This is a **last/current status table**, not an append-only historical list of every past run.

Fields:

```text
RunType
StartedAt
CompletedAt
IsRunning
Succeeded
OrdersScanned
OrdersUpdated
OrderIssues
OrdersMissingOnExchange
PositionsCompared
PositionMismatches
PositionSnapshots
ErrorMessage
UpdatedAt
```

---

## 7. Order reconciliation status

New API:

```text
GET /api/system/reconciliation/status
```

`OrderReconciliationService` now persists:

```text
MarkStarted
    ↓
run reconciliation
    ↓
MarkCompleted(metrics)
```

or, on non-cancellation failure:

```text
MarkFailed(error)
```

Successful metrics include:

```text
OrdersScanned
OrdersUpdated
OrderIssues
OrdersMissingOnExchange
```

Both manual:

```text
POST /api/system/reconcile
```

and Worker-driven order reconciliation update the same persisted `OrderReconciliation` status record.

Before any reconciliation status has ever been persisted:

```text
GET /api/system/reconciliation/status -> 404
```

---

## 8. Recovery cycle status

New API:

```text
GET /api/system/recovery/status
```

The Worker persists one `RecoveryCycle` status around its complete recovery cycle:

```text
MarkStarted(RecoveryCycle)
        ↓
EnsureConnectedAsync
        ↓
Order reconciliation
        ↓
Position reconciliation
        ↓
Position snapshot capture
        ↓
MarkCompleted(metrics)
```

On failure the Worker persists:

```text
Succeeded   = false
IsRunning   = false
ErrorMessage = truncated exception message
```

and then continues the pre-existing alert/backoff/retry behavior.

Successful recovery metrics include:

```text
OrdersScanned
OrdersUpdated
OrderIssues
OrdersMissingOnExchange
PositionsCompared
PositionMismatches
PositionSnapshots
```

Before the Worker has persisted a recovery cycle:

```text
GET /api/system/recovery/status -> 404
```

The endpoint is read-only. It does not start recovery, reconnect to the exchange, place orders, or cancel orders.

---

## 9. PositionSnapshots count caveat

`RecoveryCycle.PositionSnapshots` represents local position snapshots actually produced by `PositionService.CaptureSnapshotsAsync`.

`PositionService` reconstructs local positions from **persistent fills**. Therefore this count may legitimately be:

```text
0
```

even when:

```text
PositionsCompared > 0
```

because position reconciliation can compare an exchange position against a locally reconstructed flat/no-fill state.

Actions #92 exposed an incorrect CI assumption that `PositionSnapshots` had to be at least one. Runtime itself completed successfully. The test assertion was corrected in commit `47d5b3e...`; Actions #93 is fully green.

---

## 10. Persistence migration

New EF Core migration:

```text
20261001102000_AddLifecycleAndOperationalVisibility
```

Adds:

```text
OrderLifecycleEvents
OperationalRunStatuses
```

`TradeOpsDbContextModelSnapshot` was updated together with the migration.

No predecessor table was removed or repurposed.

Existing Orders, Fills, PositionSnapshots, RiskEvents, OperationalRiskState and TradingSignals remain intact.

---

## 11. API routes added in v1.1.2.3

```text
GET /api/orders/local/{idOrClientOrderId}/history
GET /api/system/reconciliation/status
GET /api/system/recovery/status
```

All are operator/read endpoints.

They do not directly mutate exchange state.

Existing mutation routes remain unchanged, including:

```text
POST /api/orders/local/{idOrClientOrderId}/cancel
POST /api/orders/local/cancel-all
POST /api/risk/emergency-stop
POST /api/system/reconcile
```

---

## 12. CI history for this milestone

### Actions #90

The first production commit compiled all production projects but the test project failed because existing test doubles had not yet implemented the expanded operator-read/status dependencies.

This was a test-fixture integration issue, not a production runtime failure.

Fixed in:

```text
eb30ba8977bab41d66843d9ad566aa80af4b2b73
```

### Actions #91

Fully green with the production implementation and corrected test fixtures.

### Actions #92

Build and all 70 unit tests passed. The expanded runtime smoke reached a successfully completed cross-process `RecoveryCycle`, but the smoke contained an invalid assertion requiring at least one local position snapshot.

Observed successful recovery state included:

```text
Succeeded          = true
IsRunning          = false
PositionsCompared  = 1
PositionSnapshots  = 0
```

The assertion was corrected without changing production runtime behavior.

### Actions #93 — authoritative

Fully green:

```text
Restore                         ✓
Build                           ✓
70 unit tests                   ✓
API + PostgreSQL smoke          ✓
Lifecycle audit smoke           ✓
Reconciliation status smoke     ✓
Cross-process Worker status     ✓
Predecessor execution regressions ✓
Docker Compose                  ✓
Docker API/Worker images        ✓
```

---

## 13. Runtime coverage added

Actions #93 verifies on a real PostgreSQL service:

### Lifecycle creation

```text
POST signal
    ↓
Created
    ↓
Submitted
    ↓
PartiallyFilled
```

and checks the history endpoint contains those persisted states.

### Reconciliation visibility

```text
POST /api/system/reconcile
    ↓
order becomes Filled
    ↓
GET /api/system/reconciliation/status
```

The status is checked for:

```text
RunType = OrderReconciliation
Succeeded = true
IsRunning = false
OrdersScanned = 1
OrdersUpdated = 1
OrderIssues = 0
OrdersMissingOnExchange = 0
CompletedAt != null
```

The order history is then checked to end in `Filled`.

### Cancellation lifecycle

The existing single-cancel scenario is checked and the lifecycle endpoint must end in:

```text
Cancelled
```

A repeated cancel remains idempotent.

### Recovery visibility across processes

CI starts `TradeOps.Worker` as a separate process using the same PostgreSQL database, then polls:

```text
GET /api/system/recovery/status
```

until it observes:

```text
RunType = RecoveryCycle
Succeeded = true
IsRunning = false
CompletedAt != null
```

This validates that the API can observe a Worker-written recovery status through shared persistence rather than process-local memory.

---

## 14. Definition of Done for v1.1.2.3 — COMPLETE

- [x] branch created from actual complete `v1.1.2.2` HEAD;
- [x] persisted order lifecycle entity exists;
- [x] lifecycle events are written through the persistence boundary;
- [x] initial Created event is persisted;
- [x] meaningful order updates append lifecycle snapshots;
- [x] history can be queried by local Guid;
- [x] history can be queried by deterministic ClientOrderId;
- [x] legacy orders do not receive fabricated history;
- [x] explicit `LegacySnapshot` fallback exists;
- [x] persisted operational run-status entity exists;
- [x] reconciliation start/success/failure status is persisted;
- [x] Worker recovery start/success/failure status is persisted;
- [x] API exposes reconciliation status;
- [x] API exposes recovery status;
- [x] run status works across API/Worker process boundary;
- [x] migration added and model snapshot updated;
- [x] 70 unit tests green;
- [x] lifecycle runtime smoke green;
- [x] reconciliation visibility smoke green;
- [x] Worker recovery visibility smoke green;
- [x] predecessor single cancellation green;
- [x] predecessor bulk cancellation green;
- [x] predecessor EmergencyStop green;
- [x] Docker Compose validates;
- [x] API/Worker Docker images build;
- [x] Mock remains default;
- [x] Bybit remains testnet-only;
- [x] no mainnet support introduced;
- [x] no position flattening introduced;
- [x] no trading strategy introduced;
- [x] Actions #93 fully green.

`v1.1.2.3` is functionally complete. Do not add additional capabilities to this version without explicitly changing its milestone scope.

---

## 15. Out of scope for v1.1.2.3

Not implemented here:

- historical list of every reconciliation/recovery run;
- Prometheus metrics/exporter;
- Grafana dashboard;
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

## 16. CURRENT NEXT TASK — define v1.1.2.4 before coding

Recommended next version:

```text
TradeOps/v_1.1.2.4
```

Recommended narrow scope:

```text
Execution / operational metrics API
```

Candidate operator metrics:

```text
SignalsReceived
SignalsAccepted
SignalsRejected

OrdersCreated / Submitted
OrdersFilled
OrdersCancelled
OrdersRejected
OrdersUnknown

FillsReceived

ActivePositionMismatchCount

TradingEnabled
EmergencyStop

LastReconciliationSucceeded
LastRecoverySucceeded
```

Candidate API:

```text
GET /api/metrics/execution
```

Recommended boundaries for `v1.1.2.4`:

- derive metrics from existing PostgreSQL state/audit data where possible;
- do not introduce Prometheus/Grafana unless explicitly re-scoped;
- no new exchange;
- no mainnet;
- no strategy/alpha logic;
- no position flattening;
- preserve all existing execution safety behavior.

---

## 17. Instruction for the next chat

Start with:

> Continue TradeOps. Read `TradeOps-Handsoff-v1.1.2.3.md` first. `v1.1.2.3` is complete. Branch `TradeOps/v_1.1.2.3` adds persisted order lifecycle history through `GET /api/orders/local/{idOrClientOrderId}/history`, persisted order-reconciliation status through `GET /api/system/reconciliation/status`, and cross-process Worker recovery visibility through `GET /api/system/recovery/status`. The authoritative tested code HEAD is `47d5b3e952a027f6d01345bf8e31f4996c3449e3`; Actions #93 is fully green with 70 unit tests, expanded PostgreSQL runtime smoke, Worker recovery status, Compose validation and Docker builds. Lifecycle events are persisted TradeOps snapshots, not fabricated exchange micro-events; old unaudited orders receive only an explicit `LegacySnapshot`. Preserve Mock-default, Bybit-testnet-only, no-mainnet, deterministic ClientOrderId, no-blind placement/cancellation retry, persistent risk state and no-position-flattening boundaries. Before coding, create/define `v1.1.2.4`; recommended scope is an execution/operational metrics API, initially without Prometheus/Grafana.

No additional context from the previous chat should be required beyond this handoff and repository code.
