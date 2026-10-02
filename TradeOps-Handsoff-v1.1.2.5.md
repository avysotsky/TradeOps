# TradeOps — Handoff for v1.1.2.5

## 1. Current status

Active development branch:

```text
TradeOps/v_1.1.2.5
```

Stable predecessor:

```text
TradeOps/v_1.1.2.4
predecessor branch HEAD used for branching: d9f368b762103ecbf994ff6e1a5a74be5cfa3fbb
predecessor authoritative tested code HEAD: 22e123cade45551faecaa1399853c4e42cc6efd0
```

`v1.1.2.5` tested code commits:

```text
a8f3bdc7dc9bc2fa953bc7c1505f8eba1ccce84b
feat: add operational run history API

d8e078ce6279439b0dcbfb65cede4caa1924cce3
test: cover operational run history API
```

Authoritative tested code HEAD:

```text
d8e078ce6279439b0dcbfb65cede4caa1924cce3
```

GitHub Actions #98 is fully green:

```text
Restore                              ✓
Build                                ✓
Unit tests (70)                      ✓
API + PostgreSQL smoke               ✓
Operational run history API smoke    ✓
Operational run filtering smoke      ✓
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
- no secrets committed or logged;
- deterministic `ClientOrderId` remains the placement identity;
- no blind placement retry after ambiguous outcome;
- no blind cancellation retry after ambiguous outcome;
- persisted order lifecycle history from `v1.1.2.3` remains unchanged;
- persisted latest reconciliation/recovery status from `v1.1.2.3` remains available;
- execution/operational metrics API from `v1.1.2.4` remains unchanged;
- single-order cancellation remains unchanged;
- bulk cancellation and EmergencyStop open-order cancellation remain unchanged;
- no automatic position flattening;
- no trading-strategy / alpha generation;
- existing accounting, risk, fill persistence, reconciliation and recovery semantics remain in force.

No exchange placement/cancellation code, risk-decision code, or trading strategy was changed in this milestone.

---

## 3. COMPLETED — historical operational-run persistence

`v1.1.2.5` adds a durable historical record for reconciliation and recovery runs.

New PostgreSQL table:

```text
OperationalRuns
```

New domain entity:

```text
OperationalRunRecord
```

Each run record contains:

```text
Id
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

The table stores one row per run started through `IOperationalRunStatusRepository`.

The existing table:

```text
OperationalRunStatuses
```

is retained as the latest/current projection per `RunType`.

This separation is intentional:

```text
OperationalRunStatuses = latest/current state
OperationalRuns        = historical run records
```

Do not replace one concept with the other in future work.

---

## 4. Run lifecycle persistence

`EfOperationalRunStatusRepository.MarkStartedAsync(runType)` now:

1. creates a new `OperationalRunRecord` with a new `Guid`;
2. persists it as `IsRunning=true`, `Succeeded=null`;
3. updates/creates the existing latest `OperationalRunStatus` row;
4. stores the new run ID in `OperationalRunStatus.LatestRunId`.

On successful completion:

```text
MarkCompletedAsync
```

updates both:

```text
OperationalRunStatuses current/latest projection
OperationalRuns exact historical row referenced by LatestRunId
```

with:

```text
CompletedAt != null
IsRunning = false
Succeeded = true
operational metrics copied to both records
```

On failure:

```text
MarkFailedAsync
```

updates both with:

```text
CompletedAt != null
IsRunning = false
Succeeded = false
ErrorMessage = truncated to 1000 chars
```

A compatibility fallback exists when an old/current status row has no `LatestRunId`: completion/failure can create a corresponding run record instead of crashing.

---

## 5. Important historical boundary

The migration does **not** invent historical runs that occurred before `v1.1.2.5`.

Existing `OperationalRunStatuses` rows are preserved, but their new nullable field initially remains:

```text
LatestRunId = null
```

until a new run starts.

Therefore:

```text
GET /api/system/runs
```

is a real persisted history beginning with runs captured by the new history mechanism. It is not a fabricated reconstruction of older runtime activity.

This is the same design principle used for legacy order lifecycle history: do not claim historical facts that were never persisted.

---

## 6. New operator API

New read-only endpoint:

```text
GET /api/system/runs
```

Optional query parameters:

```text
runType
limit
```

Examples:

```text
GET /api/system/runs
GET /api/system/runs?limit=20
GET /api/system/runs?runType=OrderReconciliation&limit=20
GET /api/system/runs?runType=RecoveryCycle&limit=10
```

Semantics:

- `runType` is optional;
- when present, it is trimmed and matched to the persisted run type;
- `limit` defaults to `50`;
- `limit` is clamped to `1..200`;
- results are ordered newest-first by `StartedAt`, then `UpdatedAt`;
- the endpoint reads PostgreSQL only;
- it does not start a run;
- it does not connect to an exchange;
- it does not place or cancel orders;
- it does not change risk state.

New application read contract:

```text
IOperationalRunHistoryRepository
```

New EF implementation:

```text
EfOperationalRunHistoryRepository
```

The existing `IOperationalRunStatusRepository` interface was deliberately kept unchanged so existing services/test doubles continue to use the same lifecycle contract.

---

## 7. API response shape

Each historical item returned by `/api/system/runs` exposes:

```text
id
runType
startedAt
completedAt
isRunning
succeeded
ordersScanned
ordersUpdated
orderIssues
ordersMissingOnExchange
positionsCompared
positionMismatches
positionSnapshots
errorMessage
updatedAt
```

Possible states:

### Running

```text
isRunning = true
succeeded = null
completedAt = null
```

### Successful

```text
isRunning = false
succeeded = true
completedAt != null
```

### Failed

```text
isRunning = false
succeeded = false
completedAt != null
errorMessage may be populated
```

---

## 8. Existing status endpoints remain unchanged

These routes still expose only the latest/current record for their run type:

```text
GET /api/system/reconciliation/status
GET /api/system/recovery/status
```

They are not aliases for historical `/runs`.

Use:

```text
/status -> current/latest operational state
/runs   -> historical operational audit trail
```

`GET /api/metrics/execution` from `v1.1.2.4` also continues to derive its `lastReconciliationSucceeded`, `lastRecoverySucceeded`, and `lastRecoveryPositionMismatches` fields from the latest `OperationalRunStatuses` projection, not by scanning history.

---

## 9. Migration

New EF Core migration:

```text
20261001110000_AddOperationalRunHistory
```

Adds:

```text
OperationalRuns
OperationalRunStatuses.LatestRunId (nullable uuid)
```

New index:

```text
IX_OperationalRuns_RunType_StartedAt
```

No predecessor table was removed or repurposed.

Existing Orders, OrderLifecycleEvents, Fills, PositionSnapshots, RiskEvents, OperationalRiskStates, TradingSignals and OperationalRunStatuses remain intact.

---

## 10. Runtime coverage

Actions #98 validates on real PostgreSQL:

1. API startup applies all migrations including `AddOperationalRunHistory`;
2. normal API reconciliation runs successfully;
3. its latest `/reconciliation/status` remains correct;
4. Worker starts as a separate process;
5. Worker recovery completes successfully;
6. Worker-driven order reconciliation also executes;
7. `GET /api/system/runs?limit=10` returns persisted historical rows;
8. the returned history contains both:

```text
OrderReconciliation
RecoveryCycle
```

9. returned completed rows have non-empty IDs and completed successful states;
10. `GET /api/system/runs?runType=RecoveryCycle&limit=1` returns exactly one recovery row;
11. OpenAPI contains `/api/system/runs`;
12. predecessor metrics/risk/cancellation/lifecycle behavior remains green;
13. Docker Compose validates;
14. API and Worker images build.

The CI scenario naturally produces at least three historical records:

```text
manual/API OrderReconciliation
Worker-driven OrderReconciliation
Worker RecoveryCycle
```

---

## 11. Tested commits and CI

### Production implementation

```text
a8f3bdc7dc9bc2fa953bc7c1505f8eba1ccce84b
feat: add operational run history API
```

Actions #97 was fully green and proved the new migration/write path did not break the existing runtime smoke.

### Explicit history API coverage

```text
d8e078ce6279439b0dcbfb65cede4caa1924cce3
test: cover operational run history API
```

Actions #98 is the authoritative run and is fully green, including direct `/api/system/runs` reads and filtering.

---

## 12. Definition of Done for v1.1.2.5 — COMPLETE

- [x] branch created from complete `v1.1.2.4` branch HEAD;
- [x] historical operational-run entity added;
- [x] append-per-run PostgreSQL table added;
- [x] current/latest status projection retained;
- [x] exact active/latest run linked through `LatestRunId`;
- [x] start persists historical row;
- [x] successful completion updates historical row;
- [x] failure updates historical row;
- [x] compatibility fallback for status rows without a run link exists;
- [x] no fabricated pre-migration history;
- [x] `GET /api/system/runs` added;
- [x] optional `runType` filter added;
- [x] bounded `limit` added;
- [x] newest-first ordering added;
- [x] migration and model snapshot updated;
- [x] old status endpoints preserved;
- [x] metrics endpoint semantics preserved;
- [x] 70 unit tests green;
- [x] PostgreSQL migration/write smoke green;
- [x] direct history API smoke green;
- [x] history filtering smoke green;
- [x] OpenAPI validation green;
- [x] predecessor cancellation/risk/lifecycle flows green;
- [x] Docker Compose green;
- [x] API/Worker Docker images green;
- [x] Mock remains default;
- [x] Bybit remains testnet-only;
- [x] no mainnet support introduced;
- [x] no position flattening introduced;
- [x] no strategy/alpha logic introduced;
- [x] Actions #98 fully green.

`v1.1.2.5` is functionally complete.

---

## 13. Out of scope for v1.1.2.5

Not implemented here:

- cursor/page-based operational-run pagination;
- date/time filtering for operational-run history;
- retention/archival policy for `OperationalRuns`;
- Prometheus exporter;
- Grafana dashboard;
- time-windowed execution metrics;
- automatic position flattening;
- second exchange adapter;
- Binance integration;
- mainnet / real-money execution;
- trading strategies / alpha generation;
- ML prediction;
- dashboard/mobile UI;
- SaaS multitenancy/billing;
- Kubernetes/HFT architecture.

---

## 14. Recommended next version

Recommended next branch:

```text
TradeOps/v_1.1.2.6
```

Recommended narrow scope:

```text
Time-windowed execution metrics
```

Possible extension of the existing endpoint:

```text
GET /api/metrics/execution?from=...&to=...
```

Important design issue before coding: current order metrics in `v1.1.2.4` are current-state buckets. A historical time window must not pretend that current `Orders.Status` values describe the state that existed during an earlier window. For time-windowed order metrics, use persisted lifecycle/audit facts where the requested metric requires historical semantics.

Do not add Prometheus/Grafana, another exchange, mainnet support, position flattening or strategy logic unless explicitly re-scoped.

---

## 15. Instruction for the next chat

Start with:

> Continue TradeOps. Read `TradeOps-Handsoff-v1.1.2.5.md` first. `v1.1.2.5` is complete. The authoritative tested code HEAD is `d8e078ce6279439b0dcbfb65cede4caa1924cce3`; GitHub Actions #98 is fully green with 70 unit tests, real PostgreSQL migration/runtime smoke, direct operational-history API checks, OpenAPI validation, Compose validation and Docker builds. `v1.1.2.5` adds append-per-run `OperationalRuns` persistence plus `GET /api/system/runs?runType=...&limit=...`, while existing `OperationalRunStatuses` remains the latest/current projection and `/reconciliation/status` plus `/recovery/status` keep their previous semantics. `LatestRunId` links the projection to the current/latest historical row. Historical runs before this persistence existed are not fabricated. Preserve Mock-default, Bybit-testnet-only, no-mainnet, deterministic ClientOrderId, no-blind placement/cancellation retry, persistent risk state and no-position-flattening boundaries. Recommended next narrow scope is `v1.1.2.6` time-windowed execution metrics, but define its historical semantics before coding.

No additional context from the previous chat should be required beyond this handoff and repository code.
