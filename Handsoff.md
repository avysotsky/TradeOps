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

Current v1.1.1.3 accounting-core commit:

```text
9a51782b46cfe3c53dba356cb8fedd4efcdc00bb
feat: add fee-aware net pnl accounting core
GitHub Actions #79: green
```

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

## 7. CURRENT NEXT TASK — Block 2: persistent signal audit

This is the next implementation block.

### Problem

`TradingSignal` exists as a domain/application concept but there is not yet a complete persistent execution audit record for every logical caller signal.

An operator must be able to answer:

```text
What logical signal/request was received?
When was it received?
What source supplied it?
Was it accepted or rejected?
Why was it risk-rejected?
Which local Order / ClientOrderId resulted?
Was the HTTP call an idempotent retry of an existing SignalId?
```

### Required behavior

Implement persistent logical signal audit with:

- caller-provided `SignalId` preserved as the logical identity;
- PostgreSQL unique constraint on signal identity;
- no duplicate signal record on HTTP retry;
- persist symbol, side, requested quantity, source and received time;
- persist accepted/rejected outcome;
- persist risk-rejection reasons;
- link accepted signal to local `Order` / `ClientOrderId` where practical;
- idempotent retry should return/reuse the existing logical execution rather than create a second audit row;
- keep transport DTOs out of Domain/Infrastructure boundaries;
- keep `OrderManager` no-blind-retry semantics intact.

Suggested outcome model may use an enum such as:

```text
Received
Accepted
Rejected
```

or another simple explicit representation if it fits the existing architecture better.

### Tests required for Block 2

At minimum:

- first signal is persisted;
- accepted signal links to resulting order/client ID;
- rejected signal persists rejection reason;
- same `SignalId` HTTP retry does not create a second signal;
- concurrent/duplicate persistence race is safe under DB uniqueness;
- existing order idempotency behavior does not regress.

### CI discipline

Build the whole Block 2 first, then make one feature commit and one Actions run. Use corrective commits only for real failures exposed by the final pipeline.

---

## 8. Remaining v1.1.1.3 blocks after signal audit

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

Still required:

- [ ] logical trading signals persisted idempotently;
- [ ] accepted/rejected signal outcome queryable;
- [ ] accepted signal linked to resulting order/client ID;
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

> Continue TradeOps on `TradeOps/v_1.1.1.3`. Read the current `Handsoff.md`. Block 1 (fee-aware net PnL accounting) is already complete at commit `9a51782b46cfe3c53dba356cb8fedd4efcdc00bb` with GitHub Actions #79 green. Implement Block 2 — persistent signal audit: idempotent signal persistence, accepted/rejected outcome, risk-rejection reasons, linkage to the resulting local order/client order ID, DB uniqueness/race safety, and focused tests. Preserve existing order placement/idempotency semantics. Use one feature commit and one final CI run for the block.

No additional context from the previous chat should be required beyond this file and the repository code.