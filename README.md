# TradeOps

C#/.NET trading execution and automation backend focused on reliable order handling rather than strategy research.

TradeOps is built for the case where a client already has trading rules, signals, or an existing bot and needs the engineering layer around execution: broker/exchange integration, order lifecycle, risk controls, persistence, reconciliation, recovery, logging and alerts.

> TradeOps does **not** provide a profitable strategy, alpha, signals, or return guarantees. Mock mode is the default. The only real venue integration currently present is explicitly restricted to **Bybit testnet**.

## What this demo proves

- configuration-driven exchange adapters behind `IExchangeClient`;
- deterministic `ClientOrderId` generation and idempotent signal retries;
- optional shared-secret authentication for external signal ingestion, rejected before persistence on authentication failure;
- optional PostgreSQL-backed timestamp/request-id replay protection for authenticated signal ingress;
- optional HMAC-SHA256 webhook signing that binds timestamp, request ID, method, path and exact request-body bytes;
- persistent webhook-attempt audit linking request ID to signal and local order correlation identifiers;
- independent operator API-key protection for mutating control, cancellation and reconciliation actions;
- PostgreSQL persistence with a unique constraint protecting against duplicate local orders;
- no blind retry after an ambiguous exchange timeout;
- guarded order state transitions and partial-fill handling;
- local order lifecycle history, fill audit and signal outcome audit;
- order and position reconciliation;
- persistent trading controls, emergency stop and bulk cancellation;
- restart recovery and persisted operational run status through background workers;
- Bybit testnet REST integration plus authenticated private WebSocket order/execution events;
- execution and signal-transition metrics, including window/series/by-symbol views;
- structured logging and optional fail-safe Telegram alerts;
- reproducible Docker demo;
- executable .NET signed-webhook client that demonstrates tamper rejection, replay rejection, execution, reconciliation and metrics end to end;
- CI build, unit tests and PostgreSQL-backed runtime/integration smoke tests.

## Stack

```text
C# / .NET 8
ASP.NET Core
Worker Service
EF Core 8
PostgreSQL 16
HttpClient / Bybit V5 REST
Docker / Docker Compose
GitHub Actions
```

## Solution

```text
TradeOps.sln

src/
├── TradeOps.Api
├── TradeOps.Application
├── TradeOps.Domain
├── TradeOps.Infrastructure
└── TradeOps.Worker
```

Responsibilities:

- **Domain** — entities, enums and order state rules.
- **Application** — execution use cases and exchange-independent contracts.
- **Infrastructure** — EF Core/PostgreSQL, mock exchange, Bybit testnet adapter and Telegram adapter.
- **Api** — signal input, exchange/account monitoring, order lookup/cancellation and manual reconciliation endpoints.
- **Worker** — restart recovery, reconnect and periodic reconciliation.

## Exchange architecture

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
 demo / CI            Bybit testnet
```

The selected adapter is controlled by configuration:

```json
{
  "Exchange": {
    "Provider": "Mock"
  }
}
```

Supported providers in the current public version:

```text
Mock
BybitTestnet
```

## Execution flow

```text
External signal
      |
      v
POST /api/signals
      |
      v
Optional shared-secret authentication
      |
      v
Optional timestamp + request-id replay protection
      |
      v
Optional HMAC-SHA256 request-body verification
      |
      v
Deterministic ClientOrderId
      |
      +---- existing order ----> return existing state
      |
      v
RiskEngine
      |
 allow / reject
      |
      v
Persist Created
      |
      v
Created -> Submitted
      |
      v
IExchangeClient.PlaceOrderAsync
      |
      v
exchange state / acknowledgement
      |
      v
Persist exchange state
      |
      +---- structured log
      +---- optional Telegram alert
```

A timeout does **not** trigger a blind second `PlaceOrderAsync`. TradeOps first looks up the exchange order by `ClientOrderId`; if the exchange state cannot be confirmed, the local order becomes `Unknown` and must be reconciled.

## Recovery flow

```text
TradeOps.Worker starts
      |
      v
EnsureConnectedAsync
      |
      +---- failure ---> alert ---> exponential backoff ---> retry
      |
      v
Load non-terminal persisted orders
      |
      v
Query exchange state
      |
      +---- valid ------> OrderStateMachine -> persist
      +---- missing ----> Unknown + issue
      +---- mismatch ---> issue; do not overwrite blindly
      |
      v
Periodic reconciliation
```

In mock mode, synthetic exchange IDs allow the separate Worker process to reconstruct mock state after restart. In Bybit testnet mode, the Worker queries Bybit through the same `IExchangeClient` contract.

## Quick start with Docker — mock mode

Requirements:

- Docker Engine / Docker Desktop;
- Docker Compose v2;
- `curl` only if you want to run the included scripts from the host.

Start the complete stack:

```bash
docker compose up --build -d
```

Mock is the default provider. This starts:

```text
PostgreSQL 16  -> localhost:54321
TradeOps.Api   -> http://localhost:8080
TradeOps.Worker
```

The API automatically applies the committed EF Core migration on startup.

Check health:

```bash
curl http://localhost:8080/health
```

Run the deterministic mock demo:

```bash
bash scripts/demo.sh
```

For the secured commercial-style flow, enable signal authentication, replay protection, HMAC signing and operator authentication, then run the .NET end-to-end client:

```bash
export TRADEOPS_SIGNAL_AUTH_ENABLED=true
export TRADEOPS_SIGNAL_API_KEY='local-demo-signal-api-key'
export TRADEOPS_SIGNAL_REPLAY_ENABLED=true
export TRADEOPS_SIGNAL_SIGNING_ENABLED=true
export TRADEOPS_SIGNAL_SIGNING_SECRET='local-demo-signing-secret-at-least-32-characters'
export TRADEOPS_OPERATOR_AUTH_ENABLED=true
export TRADEOPS_OPERATOR_API_KEY='local-demo-operator-api-key'

docker compose up --build -d

export TRADEOPS_DEMO_API_URL='http://localhost:8080'
export TRADEOPS_DEMO_SIGNAL_API_KEY='local-demo-signal-api-key'
export TRADEOPS_DEMO_SIGNING_SECRET='local-demo-signing-secret-at-least-32-characters'
export TRADEOPS_DEMO_OPERATOR_API_KEY='local-demo-operator-api-key'

dotnet run --project tools/TradeOps.SignedWebhookDemo
```

The final line should be `SIGNED WEBHOOK E2E DEMO: PASS`. See `docs/signed-webhook-e2e-demo.md` for the complete sequence.

Stop the stack:

```bash
docker compose down
```

Delete the demo database as well:

```bash
docker compose down -v
```

## Mock demo scenario

The script uses the fixed logical signal ID:

```text
d0f8625f-2ad4-44eb-a1ec-22acbfbb2e58
```

### 1. First submission

```json
{
  "symbol": "BTCUSDT",
  "side": "Buy",
  "quantity": 0.001,
  "source": "docker-demo",
  "signalId": "d0f8625f-2ad4-44eb-a1ec-22acbfbb2e58"
}
```

Expected mock state:

```text
RequestedQuantity = 0.001
FilledQuantity    = 0.0006
Status            = PartiallyFilled
```

### 2. Reconciliation

`POST /api/system/reconcile` queries the mock exchange again.

Expected state:

```text
FilledQuantity = 0.001
Status         = Filled
```

### 3. Idempotent retry

The exact same `signalId` is submitted again. TradeOps derives the same `ClientOrderId`, loads the existing persisted order and returns it instead of creating a duplicate.

## Bybit testnet

The Bybit adapter uses the V5 REST API and is deliberately restricted in code to:

```text
https://api-testnet.bybit.com
```

Mainnet URLs are rejected by `BybitExchangeClient` in this version.

Credentials are supplied only through environment/configuration. Never commit them.

For Docker Compose:

```bash
export TRADEOPS_EXCHANGE_PROVIDER=BybitTestnet
export BYBIT_TESTNET_API_KEY='YOUR_TESTNET_KEY'
export BYBIT_TESTNET_API_SECRET='YOUR_TESTNET_SECRET'
```

Then start the stack normally:

```bash
docker compose up --build -d
```

For a **read-only** authentication/integration check that does not place an order:

```bash
bash scripts/bybit-testnet-readonly-smoke.sh
```

The script queries:

```text
GET /api/account
GET /api/positions
GET /api/orders
```

The default Bybit configuration is:

```json
{
  "Exchange": {
    "Provider": "Mock",
    "Bybit": {
      "BaseUrl": "https://api-testnet.bybit.com",
      "ApiKey": "",
      "ApiSecret": "",
      "Category": "linear",
      "SettleCoin": "USDT",
      "AccountType": "UNIFIED",
      "RecvWindowMilliseconds": 5000,
      "HttpTimeoutSeconds": 10
    }
  }
}
```

The adapter currently implements the exchange contract for:

```text
GetAccountAsync
GetPositionsAsync
GetOpenOrdersAsync
GetOrderAsync
GetOrderByClientOrderIdAsync
PlaceOrderAsync
CancelOrderAsync
```

Connection readiness/recovery is handled separately through the exchange connection manager and background Worker. Bybit private WebSocket order/execution events are consumed through `IExchangeEventStream`.

Bybit order acknowledgements are asynchronous. A successful create/cancel HTTP acknowledgement is therefore not treated as proof of a fill or final cancellation; subsequent lookup/reconciliation confirms state.

## API

Swagger/OpenAPI is exposed by the running API. Main routes:

```text
GET    /health
GET    /health/live
GET    /health/ready

GET    /api/account
GET    /api/positions
GET    /api/positions/local

POST   /api/signals
GET    /api/signals
GET    /api/signals/{id}
GET    /api/signals/{id}/history
GET    /api/signal-ingress/requests/{requestId}

GET    /api/orders
GET    /api/orders/{exchangeOrderId}
GET    /api/orders/by-client/{clientOrderId}
DELETE /api/orders/{exchangeOrderId}

GET    /api/orders/local
GET    /api/orders/local/{idOrClientOrderId}
GET    /api/orders/local/{idOrClientOrderId}/history
POST   /api/orders/local/{idOrClientOrderId}/cancel
POST   /api/orders/local/cancel-all

GET    /api/fills
GET    /api/pnl/daily

GET    /api/risk
POST   /api/risk/trading-enabled
POST   /api/risk/emergency-stop
GET    /api/risk/events

POST   /api/system/reconcile
POST   /api/system/reconcile/positions
GET    /api/system/reconciliation/status
GET    /api/system/recovery/status
GET    /api/system/runs

GET    /api/metrics/execution
GET    /api/metrics/execution/window
GET    /api/metrics/execution/series
GET    /api/metrics/execution/by-symbol

GET    /api/metrics/signal-transitions/window
GET    /api/metrics/signal-transitions/series
GET    /api/metrics/signal-transitions/by-symbol
```

## Signal ingress authentication

`POST /api/signals` can be protected with a shared API key. Authentication is disabled by default for the local mock demo, but can be enabled without code changes.

Configuration:

```json
{
  "SignalIngress": {
    "Authentication": {
      "Enabled": true,
      "HeaderName": "X-TradeOps-Api-Key",
      "ApiKey": "YOUR_SECRET"
    }
  }
}
```

For Docker Compose:

```bash
export TRADEOPS_SIGNAL_AUTH_ENABLED=true
export TRADEOPS_SIGNAL_API_KEY='YOUR_SECRET'
docker compose up --build -d
```

Authenticated signal submission:

```bash
curl -X POST http://localhost:8080/api/signals \
  -H 'Content-Type: application/json' \
  -H 'X-TradeOps-Api-Key: YOUR_SECRET' \
  -d '{"symbol":"BTCUSDT","side":"Buy","quantity":0.001}'
```

When authentication is enabled, a missing or incorrect key returns HTTP `401` before request validation, persistence, risk evaluation or exchange execution. The middleware hashes both values and uses a fixed-time comparison; the configured secret is never written to logs or responses. Startup validation fails if authentication is enabled without a header name or API key.

### Replay protection

Authenticated signal ingress can additionally require a fresh timestamp and unique request ID:

```text
X-TradeOps-Timestamp: <Unix seconds>
X-TradeOps-Request-Id: <GUID>
```

Enable it in Docker Compose with:

```bash
export TRADEOPS_SIGNAL_AUTH_ENABLED=true
export TRADEOPS_SIGNAL_API_KEY='YOUR_SECRET'
export TRADEOPS_SIGNAL_REPLAY_ENABLED=true
docker compose up --build -d
```

The default acceptance window is ±300 seconds. Accepted request IDs are persisted in PostgreSQL before signal validation/execution, so duplicate delivery is rejected across API restarts and multiple API instances sharing the database. Receipts are retained for 600 seconds by default and expired rows are pruned opportunistically.

Replay protection prevents stale requests and reuse of the same request ID. HMAC signing can additionally bind the replay metadata to the exact request body.

### HMAC-signed webhook contract

Enable signing only together with signal API-key authentication and replay protection:

```bash
export TRADEOPS_SIGNAL_AUTH_ENABLED=true
export TRADEOPS_SIGNAL_API_KEY='YOUR_API_KEY'
export TRADEOPS_SIGNAL_REPLAY_ENABLED=true
export TRADEOPS_SIGNAL_SIGNING_ENABLED=true
export TRADEOPS_SIGNAL_SIGNING_SECRET='A_SEPARATE_SIGNING_SECRET_AT_LEAST_32_CHARACTERS'
docker compose up --build -d
```

A signed request includes:

```text
X-TradeOps-Api-Key: <API key>
X-TradeOps-Timestamp: <Unix seconds>
X-TradeOps-Request-Id: <GUID>
X-TradeOps-Signature: sha256=<HMAC-SHA256 hex digest>
```

The canonical HMAC input is:

```text
<timestamp>\n
<request-id D format>\n
<UPPERCASE method>\n
<request path>\n
<exact raw body bytes>
```

The signature is verified before the request ID is registered in PostgreSQL. A bad or tampered signature therefore cannot consume a nonce. The signing secret is separate from the API key. The default signed-body limit is 64 KiB.

See `docs/signal-ingress-hmac-signing.md` for the complete contract.

### Webhook request audit and correlation

For replay-protected requests, TradeOps records each authenticated HTTP attempt separately and correlates successful execution to the persisted signal and order:

```text
requestId
  -> ingress attempt outcome
  -> signalId
  -> orderId / ClientOrderId
  -> lifecycle / fills / reconciliation
```

Lookup all attempts for one request ID:

```text
GET /api/signal-ingress/requests/{requestId}
```

This preserves the original accepted attempt and later replay attempts as separate rows. The audit stores operational metadata only; request bodies, API keys, HMAC signatures and signing secrets are not persisted.

See `docs/signal-ingress-request-audit.md`.

## Operator API authentication

Mutating operator actions can be protected with a credential independent from the external signal-ingress key. Authentication is disabled by default for the local mock demo.

Protected routes:

```text
POST   /api/risk/trading-enabled
POST   /api/risk/emergency-stop
DELETE /api/orders/{exchangeOrderId}
POST   /api/orders/local/{idOrClientOrderId}/cancel
POST   /api/orders/local/cancel-all
POST   /api/system/reconcile
POST   /api/system/reconcile/positions
```

Configuration:

```json
{
  "OperatorApi": {
    "Authentication": {
      "Enabled": true,
      "HeaderName": "X-TradeOps-Operator-Key",
      "ApiKey": "YOUR_OPERATOR_SECRET"
    }
  }
}
```

For Docker Compose:

```bash
export TRADEOPS_OPERATOR_AUTH_ENABLED=true
export TRADEOPS_OPERATOR_API_KEY='YOUR_OPERATOR_SECRET'
docker compose up --build -d
```

Read-only monitoring endpoints remain accessible in this milestone. The operator and signal-ingress credentials are separate: neither credential implicitly authorizes the other boundary.

## Order lifecycle

Supported states:

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

The state machine validates transitions and fill invariants. For example, `PartiallyFilled` requires:

```text
0 < FilledQuantity < RequestedQuantity
```

and `Filled` requires the entire requested quantity to be filled.

Bybit statuses are mapped into this exchange-neutral lifecycle. Unknown/unmapped exchange statuses become `Unknown` instead of being silently treated as a successful terminal state.

## Duplicate-order protection

For retries of the same logical signal, the caller must reuse the same `signalId`.

TradeOps derives a deterministic client order ID and relies on both:

1. an application-level lookup; and
2. a PostgreSQL unique index on `ClientOrderId`.

The current format is:

```text
trd-{Guid:N}
```

which is exactly 36 characters and therefore fits the Bybit `orderLinkId` limit.

The database constraint is the final guard against concurrent duplicate requests racing each other.

## PostgreSQL

For local non-Docker development, configure:

```text
ConnectionStrings__TradeOpsDb
```

Example:

```bash
export ConnectionStrings__TradeOpsDb='Host=localhost;Port=5432;Database=tradeops;Username=postgres;Password=YOUR_PASSWORD'
```

The initial EF Core migration is committed under:

```text
src/TradeOps.Infrastructure/Persistence/Migrations
```

Apply migrations manually when needed:

```bash
dotnet ef database update \
  --project src/TradeOps.Infrastructure \
  --startup-project src/TradeOps.Api
```

## Telegram alerts

Telegram is disabled by default. Never commit the bot token or chat id.

Docker Compose reads optional host environment variables:

```bash
export TELEGRAM_ENABLED=true
export TELEGRAM_BOT_TOKEN='YOUR_BOT_TOKEN'
export TELEGRAM_CHAT_ID='YOUR_CHAT_ID'

docker compose up --build -d
```

The adapter is fail-safe: Telegram delivery errors are logged but do not fail the order execution path.

## Worker settings

Defaults:

```json
{
  "Worker": {
    "ReconciliationIntervalSeconds": 30,
    "InitialRetryDelaySeconds": 2,
    "MaxRetryDelaySeconds": 30
  }
}
```

Retry delay grows exponentially until `MaxRetryDelaySeconds`.

## Local .NET development

Build:

```bash
dotnet restore TradeOps.sln
dotnet build TradeOps.sln
```

Run API:

```bash
dotnet run --project src/TradeOps.Api
```

Run Worker in another terminal using the same PostgreSQL connection string:

```bash
dotnet run --project src/TradeOps.Worker
```

To run locally against Bybit testnet instead of the mock, provide the standard .NET environment variables:

```bash
export Exchange__Provider=BybitTestnet
export Exchange__Bybit__ApiKey='YOUR_TESTNET_KEY'
export Exchange__Bybit__ApiSecret='YOUR_TESTNET_SECRET'
```

## CI

GitHub Actions runs on `main` and `TradeOps/**` branches. The default pipeline remains credential-free and uses `MockExchangeClient`.

It:

1. restores and builds the complete .NET 8 solution;
2. runs the unit/integration test suite, including signal authentication, HMAC body binding, persistent replay protection and operator API authentication coverage;
3. starts PostgreSQL 16 and the API;
4. verifies liveness/readiness and the OpenAPI surface;
5. exercises signal execution, idempotent retry, reconciliation and order history;
6. verifies local cancellation, bulk cancellation and persistent emergency-stop behavior;
7. exercises fills, daily P&L, operational run status and execution metrics;
8. runs the signed webhook end-to-end demo against a secured Mock API instance;
9. validates Docker Compose and builds the API/Worker images.

Real Bybit testnet credentials are intentionally not required by ordinary CI.

## Safety boundary

- mock mode is the default;
- the Bybit implementation accepts only the official testnet host in this branch;
- no API key or secret is stored in the repository;
- optional external signal-ingress authentication uses configuration/environment secrets only;
- optional persistent replay protection rejects stale timestamps and repeated request IDs before execution;
- optional HMAC-SHA256 signing cryptographically binds replay metadata, HTTP route and exact request body;
- authenticated replay-protected attempts are persisted as correlation audit records without storing request bodies or secrets;
- mutating operator actions can use a separate configuration/environment credential;
- no secret/signing payload is logged;
- an ambiguous placement outcome is reconciled by deterministic client order ID instead of blindly resubmitting;
- real-money/mainnet trading remains out of scope for the current public version.
