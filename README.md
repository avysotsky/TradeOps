# TradeOps

C#/.NET trading execution and automation backend focused on reliable order handling rather than strategy research.

TradeOps is built for the case where a client already has trading rules, signals, or an existing bot and needs the engineering layer around execution: broker/exchange integration, order lifecycle, risk controls, persistence, reconciliation, recovery, logging and alerts.

> TradeOps does **not** provide a profitable strategy, alpha, signals, or return guarantees. Mock mode is the default. Execution-capable venue integrations are restricted to **Bybit testnet**, **Binance USD-M Futures testnet**, and **Hyperliquid testnet**. MEXC Futures is **live-host read-only** because no separate Contract API sandbox/testnet is documented.

## What this demo proves

- configuration-driven exchange adapters behind `IExchangeClient`;
- deterministic `ClientOrderId` generation and idempotent signal retries;
- optional shared-secret authentication for external signal ingestion, rejected before persistence on authentication failure;
- optional PostgreSQL-backed timestamp/request-id replay protection for authenticated signal ingress;
- optional HMAC-SHA256 webhook signing that binds timestamp, request ID, method, path and exact request-body bytes;
- persistent webhook-attempt audit linking request ID to signal and local order correlation identifiers;
- optional TradingView webhook adapter behind a trusted gateway, with deterministic event-id idempotency;
- production-like Nginx TradingView edge deployment with HTTPS, published source-IP allowlisting, client-certificate identity checks and internal credential injection;
- persistent TradingView delivery audit with event-level correlation, outcomes and processing-latency metrics;
- one-command Mock customer demo for the complete TradingView execution, idempotency, audit, reconciliation and health path;
- client integration starter kit for a controlled first paid TradingView pilot, including env template, preflight, alert template, runbook and acceptance checklist;
- read-only paid-pilot evidence exporter that produces JSON and Markdown handoff reports from persisted TradingView audit/correlation data;
- independent operator API-key protection for mutating control, cancellation and reconciliation actions;
- PostgreSQL persistence with a unique constraint protecting against duplicate local orders;
- no blind retry after an ambiguous exchange timeout;
- guarded order state transitions and partial-fill handling;
- local order lifecycle history, fill audit and signal outcome audit;
- order and position reconciliation;
- persistent trading controls, emergency stop and bulk cancellation;
- restart recovery and persisted operational run status through background workers;
- Bybit testnet REST integration plus authenticated private WebSocket order/execution events;
- Binance USD-M Futures testnet REST integration plus private user-data stream with mainnet host rejection;
- Hyperliquid testnet signed execution plus order/fill WebSocket integration with mainnet host rejection;
- MEXC Futures live-host read-only account, position and order-state integration with trading mutations disabled;
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
HttpClient / Bybit V5 REST / Binance USD-M Futures REST / Hyperliquid API / MEXC Contract API
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
- **Infrastructure** — EF Core/PostgreSQL, mock exchange, Bybit testnet adapter, Binance Futures testnet adapter, Hyperliquid testnet adapter, MEXC Futures read-only adapter and Telegram adapter.
- **Api** — signal input, exchange/account monitoring, order lookup/cancellation and manual reconciliation endpoints.
- **Worker** — restart recovery, reconnect and periodic reconciliation.

## Exchange architecture

```text
Application
    |
    v
IExchangeClient
    |
    +----------------------+---------------------------+--------------------------+
    |                      |                           |                          |
    v                      v                           v                          v
MockExchangeClient   BybitExchangeClient   BinanceFuturesExchangeClient   HyperliquidExchangeClient
    |                      |                           |                          |
 demo / CI              Bybit testnet          Binance Futures testnet      Hyperliquid testnet
                                                                              read-only
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
BinanceFuturesTestnet
HyperliquidTestnet
MexcFuturesReadOnly
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

## Binance USD-M Futures testnet

TradeOps v1.4.1.0 adds a REST adapter for Binance USD-M Futures testnet.

The adapter is restricted in code to approved non-production Binance Futures hosts. Production `fapi.binance.com` is rejected.

For Docker Compose:

```bash
export TRADEOPS_EXCHANGE_PROVIDER=BinanceFuturesTestnet
export BINANCE_FUTURES_TESTNET_API_KEY='YOUR_TESTNET_KEY'
export BINANCE_FUTURES_TESTNET_API_SECRET='YOUR_TESTNET_SECRET'
docker compose up --build -d
```

Run the read-only authentication/integration smoke test:

```bash
bash scripts/binance-futures-testnet-readonly-smoke.sh
```

The smoke test calls account, positions and open-orders endpoints only; it does not place an order.

The REST adapter implements:

```text
GetAccountAsync
GetPositionsAsync
GetOpenOrdersAsync
GetOrderAsync
GetOrderByClientOrderIdAsync
PlaceOrderAsync
CancelOrderAsync
```

Binance order lookup and cancellation require symbol context. TradeOps passes the persisted local order symbol through reconciliation and cancellation paths. For direct exchange API routes, provide `?symbol=BTCUSDT` when querying or cancelling a Binance order.

TradeOps v1.4.2.x also consumes the Binance USD-M Futures user-data stream through `IExchangeEventStream`. The Worker:

```text
creates/renews listenKey
-> connects to approved testnet/demo websocket host
-> receives ORDER_TRADE_UPDATE
-> maps order + execution updates
-> persists lifecycle/fills through ExchangeEventProcessor
-> reconnects through the existing Worker backoff loop on expiry/disconnect
```

Default stream settings:

```json
{
  "Exchange": {
    "Binance": {
      "PrivateWebSocketBaseUrl": "wss://stream.binancefuture.com",
      "ListenKeyKeepaliveMinutes": 30
    }
  }
}
```

Production Binance websocket hosts are rejected. The stream credentials are never embedded in the websocket URL; the temporary listen key is created through the Binance USER_STREAM lifecycle endpoint and closed on shutdown when possible.

## Hyperliquid testnet

TradeOps v1.5.x supports Hyperliquid testnet account/order reads and signed perpetual execution.

Read-side integration uses the public `/info` endpoint:

```text
clearinghouseState
frontendOpenOrders
orderStatus
meta
allMids
```

Signed execution uses the testnet `/exchange` endpoint with the Hyperliquid L1 signing scheme:

```text
ordered MessagePack action
-> nonce + null vault marker
-> Keccak-256 action hash
-> testnet phantom Agent
-> EIP-712 signature
-> /exchange
```

The C# signer is covered by official Hyperliquid Python SDK signature vectors, including an order with a deterministic client order id.

Configuration:

```bash
export TRADEOPS_EXCHANGE_PROVIDER=HyperliquidTestnet
export HYPERLIQUID_TESTNET_USER_ADDRESS='0xYOUR_MASTER_OR_SUBACCOUNT_ADDRESS'
export HYPERLIQUID_TESTNET_PRIVATE_KEY='0xYOUR_DEDICATED_TESTNET_API_WALLET_PRIVATE_KEY'
export HYPERLIQUID_MARKET_SLIPPAGE_PERCENT=5
docker compose up --build -d
```

Use a dedicated Hyperliquid API/agent wallet for automated signing. `UserAddress` remains the actual account address whose state is queried; the signing key may belong to an approved API wallet.

Never commit the private key.

Read-only smoke remains available:

```bash
bash scripts/hyperliquid-testnet-readonly-smoke.sh
```

Supported exchange contract:

```text
GetAccountAsync
GetPositionsAsync
GetOpenOrdersAsync
GetOrderAsync
GetOrderByClientOrderIdAsync
PlaceOrderAsync
CancelOrderAsync
```

For standard perpetuals, TradeOps resolves `asset` from the index in `meta.universe` and enforces `szDecimals`. Builder-deployed HIP-3 perps are intentionally excluded from this milestone because their asset-id encoding is different.

Hyperliquid has no native market-order primitive in this API path. TradeOps maps a TradeOps `Market` order to an aggressive IOC limit around the current mid price using the configured slippage percentage. A TradeOps `Limit` order maps to GTC.

TradeOps converts its arbitrary `ClientOrderId` into a deterministic 128-bit Hyperliquid `cloid`. This allows ambiguous placement recovery through `orderStatus` without blindly submitting the order again.

TradeOps v1.5.2.x also consumes the official Hyperliquid testnet websocket:

```text
wss://api.hyperliquid-testnet.xyz/ws
```

The Worker subscribes to:

```text
orderUpdates
userFills
```

and forwards normalized order/fill events through the existing `IExchangeEventStream -> ExchangeEventProcessor` pipeline.

Hyperliquid `userFills` does not include the client order id. TradeOps therefore resolves websocket events by local `ClientOrderId` when available and falls back to persisted `ExchangeOrderId` / Hyperliquid `oid`. Fill IDs use the globally unique combination of fill time, coin and Hyperliquid trade id.

The stream is configured with:

```json
{
  "Exchange": {
    "Hyperliquid": {
      "WebSocketUrl": "wss://api.hyperliquid-testnet.xyz/ws"
    }
  }
}
```

The Worker already reconnects event streams with exponential backoff when Hyperliquid disconnects. The mainnet websocket endpoint is rejected.

The adapter accepts only the official `https://api.hyperliquid-testnet.xyz` REST host and `wss://api.hyperliquid-testnet.xyz/ws` websocket endpoint. Mainnet endpoints are rejected.

## MEXC Futures read-only

TradeOps v1.6.0.0 adds a deliberately read-only MEXC Futures adapter.

MEXC's official Contract API documentation currently publishes the live API host:

```text
https://contract.mexc.com
```

and does not publish a separate Contract API sandbox/testnet. The documented place-order and cancel-order endpoints are also marked as under maintenance. For those reasons TradeOps does **not** expose MEXC order placement or cancellation in this milestone.

Provider:

```text
MexcFuturesReadOnly
```

The adapter supports:

```text
GetAccountAsync
GetPositionsAsync
GetOpenOrdersAsync
GetOrderAsync
GetOrderByClientOrderIdAsync (symbol required)
```

It intentionally rejects:

```text
PlaceOrderAsync
CancelOrderAsync
```

Use a MEXC API key with only the minimum read permissions required by the endpoints: account read and trade-information read. Do not grant transaction-modify permission for this adapter.

Configuration:

```bash
export TRADEOPS_EXCHANGE_PROVIDER=MexcFuturesReadOnly
export MEXC_FUTURES_API_KEY='YOUR_READ_ONLY_KEY'
export MEXC_FUTURES_API_SECRET='YOUR_READ_ONLY_SECRET'
docker compose up --build -d
```

Read-only smoke:

```bash
bash scripts/mexc-futures-readonly-smoke.sh
```

The smoke script reads account state, positions and open orders only.

MEXC contract quantities are expressed in contract volume. TradeOps converts them to base-asset quantity using the public `contractSize` metadata. Position mark price is taken from MEXC `fairPrice`.

Private GET requests use the documented MEXC Contract API signing scheme:

```text
ApiKey + Request-Time + sorted/url-encoded request parameters
-> HMAC-SHA256(secret)
-> lowercase hexadecimal Signature header
```

The adapter is restricted to the official `https://contract.mexc.com` host. This restriction does **not** make the host a test environment; it remains a live exchange endpoint, which is why mutation methods stay disabled.

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
POST   /api/integrations/tradingview
GET    /api/integrations/tradingview/operations/deliveries
GET    /api/integrations/tradingview/operations/metrics
GET    /api/integrations/tradingview/operations/health

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

## TradingView webhook adapter

TradeOps v1.2.0.0 adds an optional provider-specific adapter:

```text
POST /api/integrations/tradingview
```

It is disabled by default. The adapter is deliberately separate from the signed `/api/signals` ingress so the core HMAC/replay contract is not weakened for provider compatibility.

Recommended boundary:

```text
TradingView
  -> trusted HTTPS gateway / reverse proxy
  -> internal TradingView gateway credential
  -> TradeOps adapter
  -> canonical TradingSignal
  -> existing risk / execution / persistence / reconciliation
```

The adapter requires a stable `eventId`. TradeOps derives a deterministic signal ID from it, making provider redelivery idempotent. Reusing the same event ID with conflicting execution fields returns HTTP 409.

TradeOps v1.2.0.1 also includes a deployable Nginx edge for this boundary:

```text
deploy/tradingview-gateway/
├── nginx.conf.template
├── docker-compose.tradingview-gateway.yml
└── README.md
```

The public endpoint is `https://<host>/webhooks/tradingview`. It accepts only TradingView's published webhook source IPs, requires a presented client certificate whose subject identifies `webhook-server@tradingview.com`, overwrites the internal gateway credential, and proxies only to the internal adapter.

TradeOps v1.2.1.0 adds provider-delivery observability. Each authenticated delivery is stored separately with outcome, HTTP result, processing latency and signal/order correlation. The webhook response exposes `X-TradeOps-TradingView-Delivery-Id`.

Read recent or event-specific deliveries:

```text
GET /api/integrations/tradingview/operations/deliveries?eventId=<eventId>&limit=50
```

Read default 24-hour delivery metrics:

```text
GET /api/integrations/tradingview/operations/metrics
```

TradeOps v1.2.1.1 adds stateful health evaluation and transition-based operational alerts. The Worker persists the latest health state, suppresses repeated alerts while the state is unchanged, and emits a recovery notification only when health returns to `Healthy`.

```text
GET /api/integrations/tradingview/operations/health
```

Health monitoring is disabled by default. Enable it with `TRADEOPS_TRADINGVIEW_HEALTH_ENABLED=true`. Rate-based checks require a minimum sample size; no-success monitoring is separately opt-in.

For a customer-facing local demonstration, run:

```bash
bash scripts/tradingview-customer-demo.sh
```

The demo uses Mock execution only and leaves the stack running for inspection. It proves provider redelivery idempotency, event-id conflict protection, delivery correlation, protected reconciliation, lifecycle completion, metrics and Worker health. See `docs/customer-tradingview-demo.md`.

For a first paid pilot, start with the client-facing offer in `docs/paid-pilot-offer.md`, then use the integration kit:

```text
deploy/client-starter/
docs/client-pilot-runbook.md
docs/client-pilot-acceptance-criteria.md
scripts/client-pilot-preflight.sh
```

The starter kit keeps Mock as the first acceptance stage, supports an optional BybitTestnet phase only after Mock sign-off, and explicitly excludes strategy profitability/mainnet acceptance.

After the required test event has completed, v1.3.2.1 can generate an operator-authenticated client handoff evidence package:

```bash
export TRADEOPS_PILOT_EVENT_ID='THE_APPROVED_EVENT_ID'
export TRADEOPS_OPERATOR_API_KEY='THE_CONFIGURED_OPERATOR_KEY'
bash scripts/generate-pilot-evidence.sh
```

The exporter reads existing monitoring/audit endpoints and writes `pilot-evidence.json` plus `pilot-evidence.md`. Sensitive reads used by the evidence path require the operator credential when operator authentication is enabled. The exporter does not place, cancel or reconcile orders. See `docs/paid-pilot-evidence-handoff.md`.

See `docs/tradingview-webhook-adapter.md`, `docs/tradingview-delivery-audit.md`, `docs/tradingview-delivery-health-alerting.md` and `deploy/tradingview-gateway/README.md`.

## Operator API authentication

Operator actions and the sensitive paid-pilot evidence read path can be protected with a credential independent from the external signal-ingress key. Authentication is disabled by default for the local mock demo.

Protected routes:

```text
POST   /api/risk/trading-enabled
POST   /api/risk/emergency-stop
DELETE /api/orders/{exchangeOrderId}
POST   /api/orders/local/{idOrClientOrderId}/cancel
POST   /api/orders/local/cancel-all
POST   /api/system/reconcile
POST   /api/system/reconcile/positions
GET    /api/integrations/tradingview/operations/deliveries
GET    /api/integrations/tradingview/operations/metrics
GET    /api/integrations/tradingview/operations/health
GET    /api/signals/{id}
GET    /api/orders/local/{idOrClientOrderId}
GET    /api/orders/local/{idOrClientOrderId}/history
GET    /api/system/reconciliation/status
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

Health/readiness probes and read-only monitoring routes outside the paid-pilot evidence boundary remain accessible in this milestone. The operator and signal-ingress credentials are separate: neither credential implicitly authorizes the other boundary.

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
9. runs the isolated customer TradingView demo and generates/validates the paid-pilot evidence package against the same persisted event;
10. validates the client-pilot starter kit, rendered TradingView sample and merged Compose contract;
11. validates Docker Compose and the TradingView Nginx gateway configuration;
12. builds the API/Worker images.

Real Bybit testnet credentials are intentionally not required by ordinary CI.

## Safety boundary

- mock mode is the default;
- the Bybit implementation accepts only the official testnet host in this branch;
- no API key or secret is stored in the repository;
- optional external signal-ingress authentication uses configuration/environment secrets only;
- optional persistent replay protection rejects stale timestamps and repeated request IDs before execution;
- optional HMAC-SHA256 signing cryptographically binds replay metadata, HTTP route and exact request body;
- authenticated replay-protected attempts are persisted as correlation audit records without storing request bodies or secrets;
- the optional TradingView adapter normalizes provider payloads behind a separate trusted-gateway boundary without changing the signed core ingress;
- the v1.2.0.1 gateway deployment terminates HTTPS on port 443, filters TradingView source IPs and client-certificate identity, and injects the internal adapter credential;
- TradingView authenticated delivery attempts are persisted with provider outcome, HTTP result, latency and signal/order correlation without storing alert bodies or gateway secrets;
- optional TradingView delivery health monitoring is observational only: it persists health state and sends transition alerts but never changes trading controls, cancels orders or bypasses risk decisions;
- mutating operator actions can use a separate configuration/environment credential;
- no secret/signing payload is logged;
- an ambiguous placement outcome is reconciled by deterministic client order ID instead of blindly resubmitting;
- real-money/mainnet trading remains out of scope for the current public version.
