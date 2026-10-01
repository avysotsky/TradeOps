# TradeOps — Handoff for v1.1.1.3

## 1. Current status

Active development branch:

```text
TradeOps/v_1.1.1.3
```

Stable completed predecessor:

```text
TradeOps/v_1.1.1.2
final HEAD: 43774a3ad6a4705c818c09f0732eb2d452be99e0
GitHub Actions #75: green
```

Current v1.1.1.3 completed implementation commits:

```text
9a51782b46cfe3c53dba356cb8fedd4efcdc00bb
feat: add fee-aware net pnl accounting core
GitHub Actions #79: green

119c3a84928d46bbb0aff3b200b8b829cf35bcd9
feat: add persistent trading signal audit

53e6d91b99449253666374b23409897601a9a444
fix: register trading signal audit migration
GitHub Actions #82: green
```

The Block 2 corrective commit only registered the hand-written EF Core migration with the same
`DbContext` / `Migration` attributes used by the existing migrations. No execution semantics changed.

This file is the authoritative development handoff for the next chat/session.

TradeOps is an execution and automation engineering project. It does not provide alpha, profitable strategies, trading signals, or profitability guarantees.

Commercial positioning remains:

```text
You provide the trading rules.
TradeOps provides the execution and automation engineering.
```

---

## 2. Architecture already implemented

```text
External trading rules / caller signal
        ↓
POST /api/signals
        ↓
RiskEngine
        ↓
Persistent RiskControlService
        ↓
OrderManager
        ↓
Deterministic ClientOrderId
        ↓
OrderStateMachine
        ↓
IExchangeClient
        ↓
MockExchangeClient OR BybitExchangeClient
        ↓
REST order lifecycle + private WebSocket events
        ↓
Orders / Fills / PositionSnapshots / RiskEvents
        ↓
PostgreSQL
        ↓
Reconciliation / restart recovery / alerts
```

Solution:

```text
TradeOps.sln
src/
 ├── TradeOps.Api
 ├── TradeOps.Application
 ├── TradeOps.Domain
 ├── TradeOps.Infrastructure
 └── TradeOps.Worker

tests/
 └── TradeOps.UnitTests

tools/
 └── TradeOps.BybitSmoke
```

The project targets .NET 8 and PostgreSQL 16.

---

## 3. Completed execution capabilities inherited from v1.1.1.2

### Exchange boundary

- configuration-driven `Exchange:Provider`;
- `Mock` is the default for local demo and normal CI;
- `BybitTestnet` adapter behind exchange-neutral application contracts;
- Bybit V5 REST signing/authentication;
- testnet-only host restriction;
- account, positions and open-orders reads;
- order lookup by exchange order ID;
- order lookup by deterministic client order ID;
- `/v5/order/realtime` -> `/v5/order/history` fallback;
- order placement and cancellation;
- instrument filters / quantity / tick-size validation;
- no mainnet support;
- no committed/logged exchange secrets.

### Safe execution / idempotency

- deterministic `ClientOrderId` compatible with Bybit's 36-character limit;
- PostgreSQL uniqueness protection;
- no blind retry after ambiguous placement;
- timeout/network ambiguity -> lookup by client ID -> reconcile or `Unknown`;
- duplicate Bybit request recovery;
- explicit order state machine;
- partial fills;
- terminal-state protection;
- restart reconciliation.

### Bybit private WebSocket

- testnet private WebSocket;
- HMAC authentication;
- `order.linear` subscription;
- `execution.linear` subscription;
- heartbeat;
- reconnect/backoff;
- normalized order/execution events;
- duplicate order-event idempotency;
- execution events persist fills.

### Manual Bybit smoke

A credential-driven manual testnet smoke tool exists under:

```text
tools/TradeOps.BybitSmoke
```

It is intentionally outside normal credential-free CI and can perform read-only validation or an explicitly confirmed testnet order lifecycle.

---

## 4. Persistence and recovery already implemented

### Orders

Orders persist deterministic client IDs, exchange IDs, side/type, requested and filled quantity, prices, status and timestamps.

### Fills

Persistent `Fill` contains:

```text
OrderId
ExchangeFillId
Quantity
Price
Fee
FeeCurrency
FilledAt
```

Database guarantees include unique `ExchangeFillId` and FK to `Orders`, so duplicate execution events become no-op.

### Positions / PnL

Local positions are reconstructed from persistent fills, supporting:

- long and short exposure;
- weighted average entry;
- partial/full close;
- reversal;
- gross realized PnL;
- unrealized PnL when mark price exists;
- total PnL;
- persistent position snapshots;
- `GET /api/positions/local`.

### Position reconciliation

TradeOps compares local fill-derived exposure against exchange exposure by:

```text
Symbol
Side
Quantity
```

Mismatch lifecycle:

```text
first mismatch -> persistent PositionMismatch + alert
repeat -> update existing event, no alert spam
recovery -> resolve event + recovery alert
```

Stale mismatches are resolved even when both sides later become flat.

---

## 5. Persistent operational risk controls already implemented

Operational risk state survives process restart.

Implemented:

- persistent `TradingEnabled`;
- persistent `EmergencyStop` and reason;
- active `PositionMismatch` blocks new orders;
- risk rejections persist as `RiskRejected` events;
- operational checks run before exchange placement;
- UTC daily realized accounting is derived from fills rather than process memory.

Current API:

```text
GET  /api/risk
GET  /api/risk/events
POST /api/risk/trading-enabled
POST /api/risk/emergency-stop
```

The runtime CI smoke verifies activation of persistent emergency stop, HTTP 422 signal rejection, persisted `RiskRejected`, and emergency-stop clear.

---

## 6. COMPLETED in v1.1.1.3 — Block 1: fee-aware accounting core

Block 1 is finished. Do not reimplement it.

Commit:

```text
9a51782b46cfe3c53dba356cb8fedd4efcdc00bb
feat: add fee-aware net pnl accounting core
```

Validation:

```text
GitHub Actions #79
Restore                     ✓
Build                       ✓
Unit tests                  ✓
API + PostgreSQL smoke      ✓
Docker Compose validation   ✓
Docker API/Worker images    ✓
```

### Accounting behavior

`PositionFill` now carries optional:

```text
Fee
FeeCurrency
```

A separate accounting layer calculates daily:

```text
GrossRealizedPnL
SettlementCurrency
SettlementFees
NetRealizedPnL
UnconvertedFees
IsComplete
```

Current settlement currency default:

```text
Accounting:SettlementCurrency = USDT
```

Rules:

1. position math remains gross and unchanged;
2. fees in the configured settlement currency are explicitly included;
3. negative fees/rebates are supported;
4. unsupported fee currencies are not silently treated as zero;
5. if unsupported/unconverted fees exist, net PnL is considered incomplete;
6. risk checks use net realized PnL when accounting is complete;
7. incomplete accounting is fail-closed and blocks new orders;
8. a position opened before the UTC day and closed today contributes correctly to today's realized PnL;
9. only relevant daily fees are included in daily accounting.

`GET /api/risk` now exposes accounting fields including:

```text
SettlementCurrency
DailyGrossRealizedPnL
DailySettlementFees
DailyNetRealizedPnL
UnconvertedFees
IsDailyAccountingComplete
```

Execution semantics were intentionally not changed by Block 1.

---

## 7. COMPLETED in v1.1.1.3 — Block 2: persistent signal audit

Block 2 is finished. Do not reimplement it.

Commits:

```text
119c3a84928d46bbb0aff3b200b8b829cf35bcd9
feat: add persistent trading signal audit

53e6d91b99449253666374b23409897601a9a444
fix: register trading signal audit migration
```

Validation:

```text
GitHub Actions #82
Restore                     ✓
Build                       ✓
Unit tests (60)             ✓
API + PostgreSQL smoke      ✓
Docker Compose validation   ✓
Docker API/Worker images    ✓
```

### Persistent signal model

`TradingSignal` is now a persistent logical execution audit record.

Persisted fields include the existing request data plus:

```text
Outcome = Received | Accepted | Rejected
RiskRejectionReasons[]
OrderId?
ClientOrderId?
```

`SignalId` remains the caller-visible logical identity and is the PostgreSQL primary key.

Additional database guarantees:

- unique `ClientOrderId` when present;
- unique linked `OrderId` when present;
- FK from accepted signal audit to local `Orders`;
- duplicate `SignalId` insertion resolves through PostgreSQL uniqueness rather than creating another row.

### Execution / retry behavior

A new application-level `SignalExecutionService` owns signal-audit orchestration and delegates actual
order execution to the existing `OrderManager`.

Behavior:

1. first logical signal is persisted with `Received`;
2. normal execution continues through the unchanged `OrderManager`;
3. accepted execution is updated to `Accepted` and linked to the resulting local `OrderId` and `ClientOrderId`;
4. risk rejection is updated to `Rejected` with all rejection reasons;
5. retry of an already accepted `SignalId` returns the current linked local order instead of executing again;
6. retry of an already rejected `SignalId` returns the persisted rejection instead of rerunning risk/execution;
7. a persisted `Received` signal can resume through the existing idempotent `OrderManager` path;
8. a concurrent duplicate signal insert reloads and reuses the winning persisted logical signal;
9. incoming retry payload does not overwrite the original completed audit record.

`OrderManager` itself was not modified, so its deterministic client-order ID, duplicate-order protection,
ambiguous-placement reconciliation and no-blind-retry behavior remain intact.

### Focused tests

Added `SignalExecutionServiceTests` covering:

- first signal persistence and accepted-order linkage;
- rejected signal and persisted risk reasons;
- accepted `SignalId` retry reuse;
- duplicate persistence race resolution;
- rejected `SignalId` retry reuse.

The existing order recovery/idempotency tests continue to pass.

### CI correction encountered

The first feature pipeline (#81) compiled and passed all 60 unit tests but failed the PostgreSQL smoke
because the new hand-written migration lacked `[DbContext]` / `[Migration]` registration attributes.
The corrective commit added those attributes only. Actions #82 then passed the complete pipeline.

---

## 8. CURRENT NEXT TASK — Block 3: operator read APIs

This is the next implementation block.

### Block 3 — operator read APIs

Expose useful persisted/audit views, likely including:

```text
GET /api/signals/{id}
GET /api/signals?limit=...
GET /api/orders/local/{id-or-client-id}
GET /api/fills?symbol=...&limit=...
GET /api/pnl/daily
```

Exact route design may be adjusted to fit the project cleanly. Prefer response DTOs over blindly exposing EF entities.

### Block 4 — Swagger/OpenAPI + health/readiness + docs

Add:

```text
/health/live
/health/ready
Swagger / OpenAPI
```

Readiness should check PostgreSQL and safe dependency resolution without performing unsafe exchange actions.

Document clearly which APIs reflect exchange state versus local PostgreSQL state and mark Bybit as testnet-only.

---

## 9. Definition of Done for v1.1.1.3

Completed already:

- [x] gross realized PnL remains available;
- [x] settlement-currency fees are accounted explicitly;
- [x] net realized PnL is exposed;
- [x] unsupported/unconverted fee currencies are visible;
- [x] daily risk math uses the shared accounting source;
- [x] normal Mock/Bybit execution behavior is unchanged after accounting work.

Completed in Block 2:

- [x] logical trading signals persisted idempotently;
- [x] accepted/rejected signal outcome persisted;
- [x] accepted signal linked to resulting order/client ID.

Still required:

- [ ] accepted/rejected signal outcome exposed through operator read API;
- [ ] fills queryable through operator API;
- [ ] local order/audit view available;
- [ ] daily accounting/risk snapshot query available through dedicated operator API if useful;
- [ ] Swagger/OpenAPI documents operational APIs;
- [ ] liveness/readiness endpoints exist;
- [ ] README/docs updated;
- [ ] Docker demo remains reproducible;
- [ ] Bybit testnet adapter remains functional;
- [ ] no mainnet support introduced;
- [ ] no secrets committed/logged;
- [ ] final build/tests/Actions green.

---

## 10. Out of scope for v1.1.1.3

Do not add unless strictly required by the above milestone:

- second exchange adapter;
- Binance integration;
- mainnet / real-money trading;
- trading strategy or alpha generation;
- ML prediction;
- portfolio optimization;
- HFT/low-latency architecture;
- React dashboard;
- mobile app;
- SaaS multitenancy/billing;
- Kubernetes;
- generalized multi-currency FX conversion engine.

---

## 11. Instruction for the next chat

Start with:

> Continue TradeOps on `TradeOps/v_1.1.1.3`. Read the current `Handsoff.md`. Block 1 (fee-aware net PnL accounting) is complete at `9a51782b46cfe3c53dba356cb8fedd4efcdc00bb`. Block 2 (persistent signal audit) is complete at feature commit `119c3a84928d46bbb0aff3b200b8b829cf35bcd9` plus migration-registration fix `53e6d91b99449253666374b23409897601a9a444`; GitHub Actions #82 is fully green. Implement Block 3 — operator read APIs: persisted signal lookup/listing, local order lookup, fill querying, and a clean daily PnL/accounting read view where useful. Prefer response DTOs and local PostgreSQL audit views; do not expose EF entities blindly or alter exchange execution semantics.

No additional context from the previous chat should be required beyond this file and the repository code.
