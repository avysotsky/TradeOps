# TradeOps operator API and runtime health

TradeOps is an execution and automation backend. It does not provide alpha, profitable strategies, signals, or return guarantees.

Mock remains the default exchange provider. The only real venue adapter in this version is Bybit testnet. Mainnet is not supported.

## OpenAPI / Swagger

When the API is running:

```text
Swagger UI:   /swagger
OpenAPI JSON: /swagger/v1/swagger.json
```

Swagger describes exchange-facing operational routes, local PostgreSQL audit routes, and local-order execution controls. The state source matters when interpreting responses.

## State-source boundary

### Exchange-state reads

These routes query the configured `IExchangeClient` and therefore describe exchange-adapter state:

```text
GET    /api/account
GET    /api/positions
GET    /api/orders
GET    /api/orders/{exchangeOrderId}
GET    /api/orders/by-client/{clientOrderId}
DELETE /api/orders/{exchangeOrderId}
```

In Mock mode they query `MockExchangeClient`. In `BybitTestnet` mode they query Bybit testnet. They are not PostgreSQL audit views.

The legacy `DELETE /api/orders/{exchangeOrderId}` route remains exchange-facing and accepts an exchange order ID directly. It is retained for compatibility; it is not the preferred persisted local-order cancellation workflow.

### Local PostgreSQL audit/read views

These routes read persisted local state:

```text
GET /api/signals/{id}
GET /api/signals?limit=...
GET /api/orders/local/{idOrClientOrderId}
GET /api/fills?symbol=...&limit=...
GET /api/pnl/daily
```

Signal reads expose persisted `Received` / `Accepted` / `Rejected` outcome, risk-rejection reasons, and accepted-order linkage. The local order route returns the current PostgreSQL order record, not a fresh exchange lookup.

`GET /api/pnl/daily` uses the same daily accounting source as risk controls: gross realized PnL, settlement-currency fees, net realized PnL when accounting is complete, and any unconverted-fee gaps.

## Local-order execution control

### Single persisted order cancellation

Preferred single-order cancellation workflow:

```text
POST /api/orders/local/{idOrClientOrderId}/cancel
```

The identifier may be the local PostgreSQL order `Guid` or deterministic `ClientOrderId`.

Behavior:

- the persisted local order is loaded first;
- an already `Cancelled` order returns success without issuing another exchange cancel request;
- `Filled` and `Rejected` orders are terminal and return conflict rather than sending a cancel;
- a locally persisted `Created` order can be cancelled before exchange submission;
- active orders are cancelled through the configured `IExchangeClient` and then reconciled back into local PostgreSQL state;
- if the exchange cancel call fails with an ambiguous outcome, TradeOps performs an exchange lookup and reconciles instead of blindly retrying the cancel call;
- if the exchange acknowledges cancellation but final state is not observable yet, the API returns `202 Accepted` with outcome `CancellationRequested`;
- if exchange identity/state cannot be resolved safely, the API returns `503 Service Unavailable` with outcome `Unresolved`.

### Bulk persisted open-order cancellation

```text
POST /api/orders/local/cancel-all
POST /api/orders/local/cancel-all?symbol=BTCUSDT
```

Bulk cancellation begins from PostgreSQL and selects only locally cancellable states:

```text
Created
Submitted
Accepted
PartiallyFilled
Unknown
```

Each candidate is passed through the same `IOrderCancellationService` used by the single-order endpoint. Bulk cancellation therefore does not introduce a second exchange-cancellation implementation.

The response contains aggregate counts plus the per-order cancellation result:

```text
candidateCount
cancelledCount
alreadyCancelledCount
cancellationRequestedCount
notCancellableCount
notFoundCount
unresolvedCount
isComplete
results[]
```

`isComplete=false` when one or more candidates remain pending (`CancellationRequested`), become unresolvable, or disappear during processing. A terminal `Filled`/`Rejected` race is reported as `NotCancellable` but does not by itself make the bulk operation incomplete because the order is no longer open.

The optional `symbol` scope is normalized to uppercase. Repeating a completed bulk request is idempotent at the persisted-order level: orders already persisted as `Cancelled`, `Filled`, or `Rejected` are no longer selected as candidates.

Bulk processing is sequential and returns partial-failure information instead of pretending the whole set succeeded when an individual order cannot be resolved.

## Emergency stop and open orders

```text
POST /api/risk/emergency-stop
```

When `enabled=true`, TradeOps now performs the following sequence:

```text
persist EmergencyStop = true
        ↓
new signals are risk-blocked
        ↓
load all locally cancellable open orders
        ↓
cancel each through IOrderCancellationService
        ↓
return risk state + bulk cancellation summary
```

The persistent emergency-stop state is written **before** cancellation begins. This prevents a cancellation failure from leaving new signal execution enabled.

The response keeps the existing top-level risk fields and additionally returns `orderCancellation` when enabling emergency stop. When clearing emergency stop (`enabled=false`), `orderCancellation` is `null` and no order cancellation is performed.

If emergency-stop cancellation is incomplete, TradeOps returns the per-order failure/pending details and emits an `EmergencyStopCancellationIncomplete` critical alert through the configured alert service.

Emergency stop in this milestone does **not** flatten, reduce, reverse, or otherwise alter positions. It only blocks new orders through the existing risk control and attempts to cancel currently open orders.

Repeated `enabled=true` calls scan the current persisted candidate set again. Orders already in terminal local states are not selected again; any still-active or unresolved orders remain visible for subsequent reconciliation/operations.

## Risk and reconciliation

```text
GET  /api/risk
GET  /api/risk/events?limit=...
POST /api/risk/trading-enabled
POST /api/risk/emergency-stop
POST /api/system/reconcile
```

Risk state is persistent local operational state combined with locally derived accounting and reconciliation data. Reconciliation may read exchange state and update local PostgreSQL state.

## Runtime health

### Liveness

```text
GET /health/live
```

Returns success when the API process is alive. It deliberately does not query PostgreSQL or the exchange.

Legacy compatibility alias:

```text
GET /health
```

### Readiness

```text
GET /health/ready
```

Readiness verifies:

- PostgreSQL can be reached through the configured `TradeOpsDbContext`;
- the configured `IExchangeClient` can be resolved from dependency injection.

Readiness does **not** call an exchange endpoint, authenticate to Bybit, place/cancel an order, or otherwise perform a trading action. A PostgreSQL failure returns HTTP `503 Service Unavailable`.

## Safety boundary

- `Mock` is the default provider.
- The real exchange adapter is `BybitTestnet` only.
- The Bybit adapter rejects mainnet URLs in this version.
- Secrets remain external configuration and must not be committed.
- Health/readiness probes never place or cancel orders.
- Ambiguous placement recovery continues to use deterministic `ClientOrderId` reconciliation rather than blind resubmission.
- Single and bulk cancellation reuse the same cancellation orchestration; there is no separate blind bulk-cancel retry path.
- Emergency stop persists the blocking state before attempting open-order cancellation.
- Emergency stop does not flatten positions.
