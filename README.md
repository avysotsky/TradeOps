# TradeOps

C#/.NET trading execution and automation backend focused on reliable order handling rather than strategy research.

TradeOps is built for the case where a client already has trading rules, signals, or an existing bot and needs the engineering layer around execution: broker/exchange integration, order lifecycle, risk controls, persistence, reconciliation, recovery, logging and alerts.

> TradeOps does **not** provide a profitable strategy, alpha, signals, or return guarantees. Mock mode is the default. The only real venue integration currently present is explicitly restricted to **Bybit testnet**.

## What this demo proves

- configuration-driven exchange adapters behind `IExchangeClient`;
- deterministic `ClientOrderId` generation and idempotent signal retries;
- PostgreSQL persistence with a unique constraint protecting against duplicate local orders;
- no blind retry after an ambiguous exchange timeout;
- guarded order state transitions;
- partial-fill handling;
- reconciliation between local state and exchange state;
- restart recovery through a background Worker;
- exchange reconnect abstraction with exponential backoff;
- structured logging;
- optional fail-safe Telegram alerts;
- reproducible Docker demo;
- CI build + PostgreSQL runtime smoke test;
- Bybit V5 HMAC request signing and testnet REST adapter.

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

Supported providers in this branch:

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

The adapter currently implements the existing exchange contract for:

```text
GetAccountAsync
GetPositionsAsync
GetOpenOrdersAsync
GetOrderAsync
GetOrderByClientOrderIdAsync
PlaceOrderAsync
CancelOrderAsync
EnsureConnectedAsync
```

Bybit order acknowledgements are asynchronous. A successful create/cancel HTTP acknowledgement is therefore not treated as proof of a fill or final cancellation; subsequent lookup/reconciliation confirms state.

## API

```text
GET    /health
GET    /api/account
GET    /api/positions
GET    /api/orders
GET    /api/orders/{exchangeOrderId}
GET    /api/orders/by-client/{clientOrderId}
DELETE /api/orders/{exchangeOrderId}
GET    /api/risk
POST   /api/signals
POST   /api/system/reconcile
```

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

GitHub Actions runs on `main` and `TradeOps/**` branches. The default pipeline remains credential-free and uses `MockExchangeClient`. It:

1. restores dependencies;
2. builds the complete .NET 8 solution;
3. starts PostgreSQL 16;
4. starts the API against that database;
5. verifies `/health`;
6. submits a mock signal and verifies a partial fill;
7. reconciles and verifies the update;
8. retries the same signal and verifies idempotency;
9. validates/builds the Docker Compose images.

Real Bybit testnet credentials are intentionally not required by ordinary CI.

## Safety boundary

- mock mode is the default;
- the Bybit implementation accepts only the official testnet host in this branch;
- no API key or secret is stored in the repository;
- no secret/signing payload is logged;
- an ambiguous placement outcome is reconciled by deterministic client order ID instead of blindly resubmitting;
- real-money/mainnet trading remains out of scope for v1.1.1.2.
