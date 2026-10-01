# TradeOps — Handoff for v1.1.1.3

## 1. Purpose

This file is the development handoff from the completed branch:

```text
TradeOps/v_1.1.1.2
```

into the next working branch:

```text
TradeOps/v_1.1.1.3
```

`v_1.1.1.2` is considered complete and should remain unchanged as the stable Bybit-testnet execution milestone.

Final baseline commit:

```text
43774a3ad6a4705c818c09f0732eb2d452be99e0
```

Final validation:

```text
GitHub Actions #75
Restore                     ✓
Build                       ✓
49 Unit tests               ✓
PostgreSQL migrations       ✓
API runtime smoke           ✓
Persistent emergency stop   ✓
RiskRejected audit          ✓
Docker Compose validation   ✓
Docker API/Worker images    ✓
```

TradeOps remains an execution/automation engineering project. It does not provide alpha, profitable strategies, signals, or return guarantees.

---

## 2. Current architecture inherited from v1.1.1.2

```text
External trading rules / signal
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

---

## 3. Completed capabilities in v1.1.1.2

### Exchange boundary

- configuration-driven `Exchange:Provider`;
- `Mock` remains default for demo and CI;
- `BybitTestnet` adapter behind existing exchange-neutral contracts;
- Bybit V5 REST signing/authentication;
- testnet-only host restriction;
- account read;
- positions read;
- open orders read;
- order lookup by exchange ID;
- order lookup by deterministic client order ID;
- order placement;
- cancellation;
- instrument/order validation;
- no mainnet support;
- no secrets committed or logged.

### Safe execution

- deterministic client order IDs;
- PostgreSQL unique protection against duplicate local orders;
- no blind retry after ambiguous placement;
- timeout -> lookup by client ID -> reconcile or `Unknown`;
- explicit state machine;
- partial fills;
- terminal-state protection;
- reconciliation after restart.

### Bybit private WebSocket

- private testnet WebSocket connection;
- HMAC authentication;
- `order.linear` subscription;
- `execution.linear` subscription;
- heartbeat;
- reconnect with exponential backoff;
- normalized `ExchangeOrderUpdate` and `ExchangeExecutionUpdate`;
- idempotent duplicate event handling;
- order events update local order lifecycle;
- execution events persist fills.

### Persistent fills

`Fill` is persisted with:

```text
OrderId
ExchangeFillId
Quantity
Price
Fee
FeeCurrency
FilledAt
```

Database guarantees:

- unique `ExchangeFillId`;
- FK to `Orders`;
- duplicate execution events become no-op.

### Position / PnL

Local positions are reconstructed from persistent fills.

Implemented:

- long/short exposure;
- weighted average entry price;
- partial close;
- full close;
- long/short reversal;
- gross realized PnL;
- unrealized PnL when mark price is available;
- total PnL;
- persistent `PositionSnapshot`;
- `/api/positions/local`.

Important limitation:

```text
RealizedPnL is currently GROSS.
Fees are persisted but are not yet normalized into NetPnL.
```

### Position reconciliation

TradeOps compares fill-derived local exposure with exchange exposure.

Compared fields:

```text
Symbol
Side
Quantity
```

Average entry price and mark price are diagnostic only and do not create mismatch alarms by themselves.

Persistent `RiskEvent` lifecycle:

```text
first mismatch -> create active PositionMismatch + alert
repeated mismatch -> update same event, no alert spam
recovery -> resolve event + recovery alert
```

The stale-event edge case where both local and exchange later become flat is handled.

### Persistent operational risk controls

Operational state is stored in PostgreSQL and survives restart.

Implemented:

- persistent `TradingEnabled`;
- persistent `EmergencyStop`;
- persistent emergency-stop reason;
- active `PositionMismatch` blocks new orders;
- daily realized PnL is derived from fills rather than process memory;
- positions opened before the UTC day and closed today contribute correctly to today's realized PnL;
- risk rejection is stored as persistent `RiskRejected` audit event;
- operational risk checks occur before exchange placement.

API:

```text
GET  /api/risk
GET  /api/risk/events
POST /api/risk/trading-enabled
POST /api/risk/emergency-stop
```

### Operational infrastructure

- Worker restart recovery;
- order reconciliation;
- position reconciliation;
- position snapshot capture;
- structured logging;
- optional Telegram alerts;
- Docker Compose;
- PostgreSQL 16;
- automatic EF Core migrations in API startup;
- mock runtime smoke test;
- optional manual Bybit testnet smoke runner.

---

## 4. Goal of v1.1.1.3

### Primary goal

Move TradeOps from a reliable execution/testnet backend to a more complete **auditable trading operations service**.

The main missing area is accounting correctness and operator visibility.

The branch should focus on:

```text
fees -> normalized accounting -> NetPnL -> risk metrics -> audit trail -> operator API/docs
```

Do not add another exchange in this branch.

Do not add mainnet/real-money trading.

---

## 5. Scope for v1.1.1.3

### 5.1 Fee accounting and NetPnL

Current fills already persist:

```text
Fee
FeeCurrency
```

Implement explicit accounting semantics.

Requirements:

1. distinguish gross realized PnL from fees and net realized PnL;
2. do not silently subtract a fee whose currency is incompatible with the PnL currency;
3. for the current Bybit linear USDT scope, support quote/settle-currency fees first;
4. unsupported fee currencies must be represented explicitly as unresolved/unconverted rather than treated as zero;
5. expose both gross and net values in application models/API;
6. daily-loss risk should use a clearly defined metric, preferably net realized PnL when fee accounting is complete.

Suggested model concept:

```text
GrossRealizedPnL
FeeAmountInSettlementCurrency
NetRealizedPnL
UnconvertedFees
```

Do not build a general FX pricing engine unless required. A narrow, explicit settlement-currency rule is preferred for this branch.

---

### 5.2 Persistent signal/audit trail

`TradingSignal` currently exists as a domain object but is not persisted as an execution audit record.

Add persistence for incoming logical signals/requests so that an operator can answer:

```text
What signal was received?
When?
From which source?
Was it accepted or risk-rejected?
Which ClientOrderId/Order resulted?
Was this a duplicate/idempotent retry?
```

Requirements:

- preserve caller-provided `SignalId`;
- unique constraint on logical signal identity;
- do not create duplicate signals on HTTP retry;
- persist risk rejection outcome;
- link accepted signal to resulting local order where practical;
- avoid leaking transport DTOs into Domain/Infrastructure boundaries.

---

### 5.3 Execution/accounting audit API

Add read APIs useful for operations and portfolio demonstration.

Minimum useful endpoints/concepts:

```text
GET /api/signals/{id}
GET /api/signals?limit=...
GET /api/orders/local/{id-or-client-id}   (or equivalent local persisted view)
GET /api/fills?symbol=...&limit=...
GET /api/pnl/daily
```

Exact route design may be adjusted to fit the existing API cleanly.

Avoid exposing EF entities blindly if a response DTO is more appropriate.

---

### 5.4 Risk metrics

Expose a deterministic risk snapshot containing at least:

```text
TradingEnabled
EmergencyStop
EmergencyStopReason
CurrentDailyGrossRealizedPnL
CurrentDailyFees
CurrentDailyNetRealizedPnL
MaxDailyLoss
ActivePositionMismatchCount
OpenLocalPositions
CalculatedAt
```

The same application service should be used by API and `RiskEngine` so there is no duplicated risk math.

---

### 5.5 Operational API documentation

Add OpenAPI/Swagger for the current API.

Requirements:

- Swagger/OpenAPI available in development/demo mode;
- enum values readable as strings;
- document risk-control endpoints and their effects;
- document which endpoints query exchange state versus local PostgreSQL state;
- clearly mark Bybit integration as testnet-only;
- do not expose credentials in examples/configuration output.

---

### 5.6 Health/readiness

Current `/health` is basic.

Add useful readiness information without overengineering.

Suggested separation:

```text
/health/live
/health/ready
```

Readiness may verify:

- PostgreSQL connectivity;
- schema/migrations available;
- selected exchange adapter can be resolved;
- for `Mock`, no external dependency;
- for `BybitTestnet`, do not make every health request place orders or perform unsafe actions.

A lightweight authenticated/read-only exchange readiness check can be optional/configurable if needed.

---

## 6. Testing strategy

Keep ordinary CI credential-free and `Mock`-based.

Increase unit/integration coverage for:

- fee normalization;
- gross vs net realized PnL;
- unsupported fee currency behavior;
- daily net PnL across UTC boundary;
- persisted signal idempotency;
- accepted signal -> order linkage;
- rejected signal audit;
- operational risk snapshot;
- local fills/audit API behavior.

Do not run GitHub Actions after every small file change.

Preferred workflow:

1. build one meaningful feature block locally/in-memory through Git tree operations;
2. push one feature commit;
3. run one CI pipeline;
4. only use a corrective commit if the pipeline exposes a real issue.

The existing runtime smoke is valuable and should remain.

---

## 7. CI baseline that must not regress

The default pipeline must continue proving:

```text
Restore
Build
Unit tests
PostgreSQL migrations
API startup
Mock signal -> PartiallyFilled
Order reconciliation -> Filled
Idempotent retry
Persistent EmergencyStop
Risk rejection -> HTTP 422
RiskRejected audit persistence
EmergencyStop clear
Docker Compose validation
Docker API/Worker image build
```

Bybit credentials must remain optional and absent from normal CI.

---

## 8. Explicitly out of scope for v1.1.1.3

Do not add unless required for the above goals:

- second exchange adapter;
- Binance integration;
- mainnet trading;
- real-money deployment;
- trading strategies;
- alpha/signals generation;
- machine learning prediction;
- portfolio optimization;
- HFT/low-latency architecture;
- React dashboard;
- mobile app;
- SaaS multitenancy;
- billing;
- Kubernetes;
- generalized multi-currency FX conversion engine.

---

## 9. Definition of Done for v1.1.1.3

The branch is complete when:

- [ ] gross realized PnL remains available;
- [ ] supported settlement-currency fees are accounted explicitly;
- [ ] net realized PnL is exposed;
- [ ] unsupported/unconverted fee currencies are visible and not silently discarded;
- [ ] daily risk math has one shared source of truth;
- [ ] logical trading signals are persisted idempotently;
- [ ] signal outcome/audit can be queried;
- [ ] fills can be queried through an operator API;
- [ ] daily PnL/risk snapshot can be queried;
- [ ] risk rejection audit remains persistent;
- [ ] Swagger/OpenAPI documents the operational API;
- [ ] liveness/readiness endpoints exist;
- [ ] mock execution/reconciliation behavior remains unchanged;
- [ ] Bybit testnet adapter remains functional;
- [ ] no mainnet support is introduced;
- [ ] no secrets are committed/logged;
- [ ] Docker demo remains reproducible;
- [ ] build has zero errors;
- [ ] unit/integration tests are green;
- [ ] final GitHub Actions run is green.

---

## 10. Recommended development order

### Block 1 — accounting core

Implement fee accounting models and NetPnL calculation first.

Include:

```text
fee classification
settlement-currency fee handling
gross/net realized PnL
daily accounting snapshot
unit tests
```

Do not change exchange execution behavior in this block.

### Block 2 — persistent signal audit

Persist logical signals and execution outcomes with idempotency.

### Block 3 — operator read APIs

Expose fills, signals, daily PnL/risk snapshot and local audit views.

### Block 4 — Swagger + health/readiness + documentation

Finish operational visibility and update README/CI/runtime smoke where useful.

Use larger feature blocks and fewer commits/Actions runs.

---

## 11. First implementation task

Start here:

> Continue TradeOps from `TradeOps/v_1.1.1.3`. Read `Handsoff.md`. Implement the accounting core without modifying order-placement semantics: introduce explicit gross/fee/net PnL models, support settlement-currency fees for the existing Bybit linear USDT scope, represent unsupported fee currencies explicitly, update daily risk accounting to use the shared accounting service, and add focused unit tests. Keep Mock and Bybit testnet execution flows unchanged. Do not add mainnet or a second exchange.

---

## 12. Commercial purpose

The commercial positioning remains:

```text
You provide the trading rules.
TradeOps provides the execution and automation engineering.
```

`v_1.1.1.3` should make the project more credible for paid engineering work by demonstrating not only order execution, but also:

- operational auditability;
- accounting correctness;
- persistent risk controls;
- incident/reconciliation visibility;
- API documentation;
- restart-safe state;
- production-style observability boundaries.

The project must continue to avoid any claim that it generates profitable trading decisions.