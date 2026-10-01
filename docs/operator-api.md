# TradeOps operator API and runtime health

TradeOps is an execution and automation backend. It does not provide alpha, profitable strategies, signals, or return guarantees.

Mock remains the default exchange provider. The only real venue adapter in this version is Bybit testnet. Mainnet is not supported.

## OpenAPI / Swagger

When the API is running:

```text
Swagger UI:   /swagger
OpenAPI JSON: /swagger/v1/swagger.json
```

Swagger describes both exchange-facing operational routes and local PostgreSQL audit routes. The state source matters when interpreting responses.

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

### Risk and reconciliation

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
