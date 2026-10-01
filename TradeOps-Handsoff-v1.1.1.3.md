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

5498ef5943198e689c5f83b249f393bdad28d767
feat: add operator read APIs
GitHub Actions #83: green

440e2d343e38a209aa8d93e56f85207bba171b39
docs: add operator api and health guide

41bb973d62b7f0bf6a69ad7b22b9e3d6753b5f27
feat: add OpenAPI and runtime health probes
GitHub Actions #85: green
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

## 8. COMPLETED in v1.1.1.3 — Block 3: operator read APIs

Block 3 is finished. Do not reimplement it.

Commit:

```text
5498ef5943198e689c5f83b249f393bdad28d767
feat: add operator read APIs
```

Validation:

```text
GitHub Actions #83
Restore                     ✓
Build                       ✓
Unit tests                  ✓
API + PostgreSQL smoke      ✓
Docker Compose validation   ✓
Docker API/Worker images    ✓
```

Implemented read-only operator views:

```text
GET /api/signals/{id}
GET /api/signals?limit=...
GET /api/orders/local/{id-or-client-id}
GET /api/fills?symbol=...&limit=...
GET /api/pnl/daily
```

A dedicated `IOperatorReadRepository` / `EfOperatorReadRepository` isolates PostgreSQL read queries from execution repositories.
New operator endpoints use response DTOs rather than exposing EF entities. Existing `/api/orders` endpoints remain exchange-state reads; `/api/orders/local/...` is explicitly local PostgreSQL state.

Signal reads expose accepted/rejected outcome, risk-rejection reasons and order/client-order linkage. Fill reads support symbol filtering and bounded recent-result limits. Daily PnL exposes the shared accounting/risk snapshot fields without changing risk calculation semantics.

The CI runtime smoke validates accepted signal lookup/listing, local-order lookup, fills endpoint reachability, daily PnL response, and rejected signal audit. No execution semantics or database schema changed in Block 3.

---

## 9. COMPLETED in v1.1.1.3 — Block 4: Swagger/OpenAPI + health/readiness + docs

Block 4 is finished. Do not reimplement it.

Commits:

```text
440e2d343e38a209aa8d93e56f85207bba171b39
docs: add operator api and health guide

41bb973d62b7f0bf6a69ad7b22b9e3d6753b5f27
feat: add OpenAPI and runtime health probes
```

Validation:

```text
GitHub Actions #85
Restore                     ✓
Build                       ✓
Unit tests                  ✓
API + PostgreSQL smoke      ✓
Liveness                    ✓
Readiness                   ✓
OpenAPI JSON                ✓
Docker Compose validation   ✓
Docker API/Worker images    ✓
```

Implemented:

```text
GET /health/live
GET /health/ready
GET /health                  (legacy liveness alias)
Swagger UI:   /swagger
OpenAPI JSON: /swagger/v1/swagger.json
```

`Swashbuckle.AspNetCore 10.2.3` is used with the existing .NET 8 API.

Liveness intentionally performs no PostgreSQL or exchange operation.

Readiness verifies PostgreSQL connectivity through `TradeOpsDbContext` and safe DI resolution of the configured `IExchangeClient`. It does not call Mock/Bybit endpoints, authenticate remotely, place/cancel orders, or otherwise perform an exchange action. PostgreSQL or exchange-client resolution failure returns HTTP 503.

`docs/operator-api.md` documents the state-source boundary:

- exchange-facing routes query the configured `IExchangeClient`;
- signal/local-order/fill/PnL operator views read local PostgreSQL state;
- risk/reconciliation semantics are called out separately;
- Mock remains the default;
- Bybit remains testnet-only;
- mainnet remains unsupported.

The CI smoke now validates the new health endpoints and verifies that the generated OpenAPI document contains the operator API routes.

---

## 10. Definition of Done for v1.1.1.3 — COMPLETE

Completed:

- [x] gross realized PnL remains available;
- [x] settlement-currency fees are accounted explicitly;
- [x] net realized PnL is exposed;
- [x] unsupported/unconverted fee currencies are visible;
- [x] daily risk math uses the shared accounting source;
- [x] normal Mock/Bybit execution behavior is unchanged after accounting work;
- [x] logical trading signals persisted idempotently;
- [x] accepted/rejected signal outcome persisted;
- [x] accepted signal linked to resulting order/client ID;
- [x] accepted/rejected signal outcome exposed through operator read API;
- [x] fills queryable through operator API;
- [x] local order/audit view available;
- [x] daily accounting/risk snapshot available through dedicated operator API;
- [x] Swagger/OpenAPI documents operational APIs;
- [x] liveness/readiness endpoints exist;
- [x] operator API / state-source / health documentation exists;
- [x] Docker Compose still validates and API/Worker images build in CI;
- [x] Bybit testnet adapter code and safety boundary remain unchanged by Blocks 1-4;
- [x] no mainnet support introduced;
- [x] no secrets committed by v1.1.1.3 work;
- [x] final full build/tests/runtime smoke/Actions #85 green.

The credential-driven Bybit remote smoke remains intentionally manual and outside ordinary CI. No claim is made that Actions #85 authenticated to Bybit; it verifies the credential-free Mock/PostgreSQL runtime plus compilation of the complete solution.

---

## 11. Out of scope for v1.1.1.3

Do not add unless explicitly starting a new milestone/version:

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

## 12. CURRENT NEXT TASK — release or define the next milestone

The implementation scope and Definition of Done for `v1.1.1.3` are complete at code commit:

```text
41bb973d62b7f0bf6a69ad7b22b9e3d6753b5f27
GitHub Actions #85: fully green
```

Do not add another feature to `v1.1.1.3` without explicitly changing the milestone scope.

The next clean action is one of:

1. finalize/tag/release `v1.1.1.3`; or
2. define a new `v1.1.2` milestone/backlog and branch before implementing additional capabilities.

No release tag was created automatically in this session.

---

## 13. Instruction for the next chat

Start with:

> Continue TradeOps. Read the current `Handsoff.md` first. `v1.1.1.3` implementation is complete: Block 1 fee-aware accounting (`9a51782...`, Actions #79), Block 2 persistent signal audit (`119c3a8...` + `53e6d91...`, Actions #82), Block 3 operator read APIs (`5498ef5...`, Actions #83), and Block 4 Swagger/OpenAPI + liveness/readiness + operator documentation (`440e2d3...` + `41bb973...`, Actions #85). Actions #85 is fully green, including runtime PostgreSQL/API smoke, health probes, OpenAPI validation and Docker image builds. Do not reimplement Blocks 1-4. Decide whether to finalize/tag `v1.1.1.3` or define the next version/milestone before adding features. Preserve Mock-default, Bybit-testnet-only, no-mainnet and no-blind-retry safety boundaries.

No additional context from the previous chat should be required beyond this file and the repository code.
