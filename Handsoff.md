# TradeOps — Handoff for v1.1.1.2

## 1. Purpose of this handoff

This file is the development handoff from the completed `TradeOps/v_1.1.1.1_Init` branch into the next working branch:

```text
TradeOps/v_1.1.1.2
```

The previous branch established a working C#/.NET trading execution MVP using a deterministic mock exchange. The next branch must keep that architecture intact and replace only the exchange boundary with the first real **testnet** exchange adapter.

The project is still an engineering demo / reusable execution backend. It does **not** provide trading signals, alpha, profitable strategies, or profitability guarantees.

---

## 2. Baseline inherited from v1.1.1.1

The new branch starts from a working .NET 8 solution with these projects:

```text
TradeOps.sln
src/
 ├── TradeOps.Api
 ├── TradeOps.Application
 ├── TradeOps.Domain
 ├── TradeOps.Infrastructure
 └── TradeOps.Worker
```

Current implemented flow:

```text
POST /api/signals
        ↓
RiskEngine
        ↓
OrderManager
        ↓
ClientOrderId / idempotency
        ↓
OrderStateMachine
        ↓
IExchangeClient
        ↓
MockExchangeClient
        ↓
partial fill / fill
        ↓
PostgreSQL persistence
        ↓
reconciliation
        ↓
restart recovery
        ↓
structured logs / optional Telegram alerts
```

Implemented capabilities inherited from v1.1.1.1:

- .NET 8 solution and layered project structure;
- ASP.NET Core API;
- background Worker Service;
- PostgreSQL 16 + EF Core persistence;
- committed `InitialOrders` migration;
- automatic database migration on API startup;
- `IExchangeClient` abstraction;
- `MockExchangeClient`;
- `IRiskEngine` / `RiskEngine`;
- `IOrderManager` / `OrderManager`;
- deterministic `ClientOrderId` generation;
- PostgreSQL unique constraint for duplicate protection;
- no blind retry after an ambiguous order-placement timeout;
- explicit order state machine;
- partial fill handling;
- reconciliation of local and exchange state;
- `OrderStatus.Unknown` for unresolved exchange state;
- restart recovery Worker;
- reconnect/backoff abstraction;
- structured logging;
- optional Telegram alerts;
- Dockerfiles for API and Worker;
- `docker-compose.yml`;
- reproducible `scripts/demo.sh`;
- GitHub Actions CI with PostgreSQL runtime smoke test and Docker image build.

The final v1.1.1.1 CI pipeline is green and validates:

```text
Restore                   ✓
Build                     ✓
PostgreSQL 16             ✓
EF migration              ✓
API runtime               ✓
Signal -> PartiallyFilled ✓
Reconciliation -> Filled  ✓
Idempotent retry          ✓
Docker Compose validation ✓
API Docker image          ✓
Worker Docker image       ✓
```

---

## 3. Goal of v1.1.1.2

### Primary goal

Implement the first **real exchange testnet adapter** behind the existing `IExchangeClient` contract while keeping all existing execution, idempotency, risk, persistence, reconciliation and recovery logic unchanged.

The preferred first venue is **Bybit testnet**, subject to verification of the current official API documentation before implementation. If current API/access constraints make Bybit unsuitable, Binance testnet can be considered, but only one exchange adapter must be implemented in this version.

### Architectural rule

Do not rewrite the application layer around a specific exchange.

The desired structure is:

```text
Application
    |
    v
IExchangeClient
    |
    +--------------------+
    |                    |
    v                    v
MockExchangeClient   BybitExchangeClient
    |                    |
 demo / CI            testnet API
```

The existing `MockExchangeClient` stays in the project. It remains the deterministic adapter used for local demo and CI unless an integration job explicitly enables the testnet adapter.

---

## 4. Scope for v1.1.1.2

### 4.1 Exchange configuration

Add configuration that selects the active exchange implementation without changing controllers or application services.

Target configuration concept:

```json
{
  "Exchange": {
    "Provider": "Mock"
  }
}
```

Supported values for this version:

```text
Mock
BybitTestnet
```

Do not hardcode API credentials.

Expected environment variables for testnet credentials should follow .NET configuration conventions, for example:

```text
Exchange__Bybit__ApiKey
Exchange__Bybit__ApiSecret
```

Secrets must never be committed to Git.

---

### 4.2 Bybit testnet HTTP client

Create an Infrastructure implementation similar to:

```text
TradeOps.Infrastructure/
  Exchange/
    Bybit/
      BybitExchangeClient.cs
      BybitOptions.cs
      BybitAuthenticationHandler.cs   (or equivalent signing component)
      Contracts/
      Mapping/
```

Before coding, verify the current official Bybit testnet/API documentation and implement against the currently supported API version.

The adapter must use `HttpClient` through DI / `IHttpClientFactory` or a typed client. Do not instantiate a new `HttpClient` per request.

---

### 4.3 Required IExchangeClient operations

The real adapter must implement the existing exchange contract for at least:

```text
GetAccountAsync
GetPositionsAsync
GetOpenOrdersAsync
GetOrderAsync
GetOrderByClientOrderIdAsync
PlaceOrderAsync
CancelOrderAsync
```

If the existing `IExchangeClient` signature does not express a required exchange capability cleanly, prefer a minimal contract extension rather than leaking raw Bybit DTOs into Application or Domain.

Exchange-specific transport DTOs remain in Infrastructure.

---

### 4.4 Authentication and request signing

Implement testnet authentication/signing according to the current official exchange specification.

Requirements:

- API key and secret only from configuration/secrets;
- signing code isolated from business logic;
- UTC timestamps generated centrally;
- request-signing failures logged without exposing secrets;
- no API secret, raw signature material or authorization credentials in logs.

---

### 4.5 Domain mapping

Map exchange-specific DTOs to TradeOps domain/application models.

At minimum map:

```text
exchange order id
client order id
symbol
side
order type
requested quantity
filled quantity
average fill price
price
exchange status -> OrderStatus
```

Do not expose exchange status strings directly to `OrderManager`.

Unknown/unmapped exchange states must fail safely and should not silently become `Filled` or `Cancelled`.

---

### 4.6 Safe order placement

The current timeout rule must remain unchanged:

```text
PlaceOrder request
    |
 timeout / ambiguous response
    |
    X  DO NOT blindly submit another order
    |
    v
lookup by deterministic client order id
    |
    +---- found ----> reconcile returned state
    |
    +---- not confirmed ----> local OrderStatus.Unknown
```

The real exchange adapter must support the exchange-side client-order identifier needed for this workflow.

Idempotency is not optional.

---

### 4.7 Reconciliation with a real exchange

The existing `OrderReconciliationService` must continue to work unchanged or with only generic contract-level improvements.

The real adapter should provide sufficient exchange state for:

```text
Submitted
Accepted
PartiallyFilled
Filled
Cancelled
Rejected
Unknown / unresolved
```

Local PostgreSQL is not treated as the absolute source of truth for live exchange state.

After API/Worker restart, TradeOps must be able to load unfinished local orders and query the testnet exchange to recover the latest state.

---

### 4.8 Connection/recovery behavior

Integrate the testnet adapter with the existing `IExchangeConnectionManager` abstraction.

For a REST-first implementation, "connected" can mean that authenticated API access has been verified successfully.

The Worker must keep its current behavior:

```text
startup
  ↓
EnsureConnectedAsync
  ↓
reconcile unfinished orders
  ↓
periodic reconciliation
  ↓
transient failure -> exponential backoff -> reconnect -> reconcile
```

Do not build a complex connectivity framework in this version.

---

## 5. REST first, WebSocket second

The first milestone of v1.1.1.2 is a reliable REST testnet adapter.

Do not start with a full WebSocket implementation before these are working:

1. authentication;
2. account query;
3. positions query;
4. open-orders query;
5. place testnet order;
6. get order by exchange/client ID;
7. cancel testnet order;
8. persistence;
9. reconciliation after restart.

After the REST flow is stable, a private WebSocket/order-event stream may be added in this branch if it can be done without destabilizing the MVP. Otherwise it becomes the next version.

---

## 6. Tests required in v1.1.1.2

### Unit tests

Introduce/expand tests for generic application behavior where practical:

- exchange status mapping;
- order state transitions;
- partial fill mapping;
- unsupported exchange state handling;
- deterministic client order IDs;
- timeout -> lookup instead of duplicate placement;
- reconciliation updates;
- terminal-state protection.

### CI tests

CI must continue to run without exchange credentials.

Therefore:

```text
GitHub Actions default path -> MockExchangeClient
```

The existing PostgreSQL smoke test must remain green.

### Testnet integration tests

Real testnet tests must be opt-in and secret-driven. They must not fail ordinary pull requests merely because testnet credentials are unavailable.

Possible structure:

```text
TRADEOPS_RUN_TESTNET_TESTS=true
Exchange__Provider=BybitTestnet
Exchange__Bybit__ApiKey=...
Exchange__Bybit__ApiSecret=...
```

Do not commit credentials.

---

## 7. Docker requirements

The existing Docker setup must continue to work in mock mode:

```bash
docker compose up --build -d
bash scripts/demo.sh
```

Testnet mode should be activatable only by environment configuration, not by rebuilding application code.

Example concept:

```text
Exchange__Provider=BybitTestnet
Exchange__Bybit__ApiKey=...
Exchange__Bybit__ApiSecret=...
```

No secret values in `docker-compose.yml`.

---

## 8. Logging and alerts

For the real adapter add structured events for at least:

```text
ExchangeAuthenticationSucceeded
ExchangeAuthenticationFailed
ExchangeRequestFailed
OrderPlacementRequested
OrderPlacementConfirmed
OrderPlacementAmbiguous
OrderCancelled
ReconciliationMismatch
ExchangeDisconnected
ExchangeReconnected
```

Never log:

```text
API secret
raw authorization headers
private signing payload containing credentials
```

Existing Telegram delivery remains optional and fail-safe.

---

## 9. Explicitly out of scope for v1.1.1.2

Do not add the following in this version unless required to complete the real testnet execution flow:

- profitable trading strategy;
- signals/alpha research;
- ML market prediction;
- multi-exchange orchestration;
- production real-money trading;
- portfolio optimization;
- React/UI dashboard;
- mobile application;
- billing;
- SaaS multitenancy;
- Kubernetes;
- low-latency/HFT optimization;
- multiple exchange adapters at once.

The priority is one real, reliable testnet execution adapter.

---

## 10. Definition of Done for v1.1.1.2

The version is complete when all of the following are true:

- [ ] `MockExchangeClient` still works;
- [ ] active exchange provider is configuration-driven;
- [ ] Bybit testnet adapter authenticates successfully;
- [ ] account can be queried;
- [ ] positions can be queried;
- [ ] open orders can be queried;
- [ ] a testnet order can be placed through the existing `POST /api/signals` execution flow;
- [ ] exchange order ID and client order ID are persisted;
- [ ] partial/filled/rejected/cancelled states map correctly;
- [ ] an order can be fetched by client order ID after an ambiguous request;
- [ ] cancellation works;
- [ ] reconciliation works against real testnet state;
- [ ] restart recovery works for an unfinished testnet order;
- [ ] no blind duplicate order retry exists;
- [ ] secrets are not committed or logged;
- [ ] existing mock PostgreSQL CI smoke test remains green;
- [ ] Docker mock demo remains reproducible;
- [ ] testnet integration path is documented in README;
- [ ] `dotnet build` completes with zero errors;
- [ ] GitHub Actions is green.

---

## 11. Recommended development order

### Step 1 — verify current exchange documentation

Verify current official Bybit testnet REST/auth/order documentation before implementing transport DTOs or request signing.

### Step 2 — configuration/provider selection

Add `ExchangeOptions` and DI registration that switches between:

```text
MockExchangeClient
BybitExchangeClient
```

### Step 3 — authentication + account read

Implement signing/authentication and make one safe authenticated read operation work first.

### Step 4 — positions/open orders

Implement read-only mappings and validate decimal/status handling.

### Step 5 — order lookup

Implement lookup by exchange order ID and deterministic client order ID before enabling placement.

### Step 6 — testnet placement

Route the existing `OrderManager` through the real adapter and place a minimal testnet order.

### Step 7 — cancellation

Implement cancel and status reconciliation.

### Step 8 — restart recovery

Restart API/Worker with an unfinished testnet order in PostgreSQL and verify state recovery from the exchange.

### Step 9 — tests/README/CI

Preserve existing mock CI and document the optional testnet workflow.

### Step 10 — evaluate WebSocket

Only after the REST execution/recovery path is stable decide whether private order/fill WebSocket events belong in v1.1.1.2 or the next branch.

---

## 12. First implementation task for the next development session

Start here:

> Continue TradeOps from `TradeOps/v_1.1.1.2`. Read `Handsoff.md`. Verify the current official Bybit testnet API documentation. Then implement configuration-driven exchange provider selection and create the initial `BybitOptions` / `BybitExchangeClient` skeleton behind the existing `IExchangeClient`, without changing the existing application execution flow and without adding real-money trading.

---

## 13. Commercial purpose

This version moves the portfolio from a purely simulated execution engine toward a demonstrable broker/exchange integration project.

The commercial message remains:

```text
You provide the trading rules.
TradeOps provides the execution and automation engineering.
```

The project is intended to demonstrate C#/.NET skills relevant to jobs involving:

- broker/exchange API integration;
- trading execution backends;
- order lifecycle management;
- duplicate-order protection;
- partial fills;
- reconciliation;
- risk controls;
- recovery/reconnect logic;
- PostgreSQL persistence;
- Docker/Linux deployment;
- operational logging and alerts.
