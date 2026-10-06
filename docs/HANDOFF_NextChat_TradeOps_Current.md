# TradeOps handoff — current state

## Source of truth

Repository:

```text
avysotsky/TradeOps
```

Default branch:

```text
main
```

Current `main` merge commit:

```text
e9695adc0c50f8ffd9b409c98587b8d4f347ab56
```

Latest completed milestone:

```text
TradeOps v2.4.0.0 — Deribit Private Event Stream
PR #42
CI #529: success
```

There is no unfinished implementation branch to resume. Start from `main`.

## User workflow preference

Work in medium-sized bounded chunks.

Preferred cadence:

```text
one meaningful milestone
-> tests / CI
-> merge
-> next milestone
```

Do not report every minute or every small tool call.

Do not make a single request so large that it risks tool-delivery timeout.

The user explicitly asked to keep work between the previous two extremes:
- not microscopic;
- not several large venue implementations in one request.

## Product positioning

TradeOps is an execution/integration/reliability backend.

It is not a trading-strategy product.

Do not claim or implement as product positioning:

- alpha generation;
- profitable strategies;
- return guarantees;
- signal prediction;
- HFT / ultra-low-latency specialization unless a concrete client later requires it.

Commercial value is:

- webhook / signal ingestion;
- broker and exchange integration;
- authentication and signing;
- deterministic client/order identity;
- idempotency;
- duplicate/replay protection;
- order lifecycle handling;
- reconciliation;
- private execution events;
- monitoring / health;
- audit / evidence;
- paid-pilot handoff.

## Core product baseline

Existing commercial path remains:

```text
Paid Pilot Offer
-> Client Demo Script
-> Client Intake Questionnaire
-> FIT decision
-> Paid Pilot Scope
-> Starter Kit / Runbook
-> Mock
-> optional venue test/demo stage
-> Acceptance Criteria
-> Evidence Export
-> Client Sign-off / Handoff
```

The evidence exporter remains read-only.

Sensitive operator reads remain protected by independent operator API authentication.

## Current exchange providers

TradeOps currently declares these providers:

```text
Mock
BybitTestnet
BinanceFuturesTestnet
HyperliquidTestnet
MexcFuturesReadOnly
DeribitTestnet
OkxDemo
BitgetDemo
GateFuturesTestnet
KrakenFuturesReadOnly
KuCoinFuturesReadOnly
CoinbaseIntxSandbox
```

### Capability matrix

| Provider | Venue | Environment | Execution | Private stream | Live trading host |
|---|---|---|---:|---:|---:|
| Mock | Mock | Mock | yes | no | no |
| BybitTestnet | Bybit | Testnet | yes | yes | no |
| BinanceFuturesTestnet | Binance USD-M Futures | Testnet | yes | yes | no |
| HyperliquidTestnet | Hyperliquid | Testnet | yes | yes | no |
| MexcFuturesReadOnly | MEXC Futures | LiveReadOnly | no | no | yes |
| DeribitTestnet | Deribit | Testnet | yes | yes | no |
| OkxDemo | OKX | Demo | yes | no | yes, protected by simulated-trading header |
| BitgetDemo | Bitget | Demo | yes | no | yes, protected by demo header |
| GateFuturesTestnet | Gate Futures | Testnet | yes | no | no |
| KrakenFuturesReadOnly | Kraken Futures | LiveReadOnly | no | no | yes |
| KuCoinFuturesReadOnly | KuCoin Futures | LiveReadOnly | no | no | yes |
| CoinbaseIntxSandbox | Coinbase International Exchange | Sandbox | yes | no | no |

This matrix is now represented in code by:

```text
IExchangeCapabilityCatalog
ExchangeCapabilityCatalog
ExchangeCapabilityProfile
```

API endpoints:

```text
GET /api/exchange/capabilities
GET /api/exchange/capabilities/current
```

The capability catalog is covered by cross-provider contract tests.

## Venue details

### Bybit Testnet

Supports REST execution and private WebSocket events.

Mainnet is rejected.

### Binance USD-M Futures Testnet

Supports REST execution and private user-data stream.

Includes listenKey lifecycle, keepalive and reconnect.

Mainnet is rejected.

### Hyperliquid Testnet

Supports:

- account;
- positions;
- open orders;
- order lookup by oid / cloid;
- signed PlaceOrder;
- signed CancelOrder;
- orderUpdates WebSocket;
- userFills WebSocket.

Hyperliquid L1 signing is implemented in C# and verified against official Hyperliquid Python SDK vectors.

Signing flow:

```text
ordered MessagePack action
-> nonce + null vault marker
-> Keccak-256 action hash
-> testnet phantom Agent
-> EIP-712
-> r/s/v
```

TradeOps arbitrary `ClientOrderId` is mapped deterministically to a 128-bit Hyperliquid `cloid`.

For Hyperliquid fills, local order resolution can fall back to persisted `ExchangeOrderId`, because `userFills` does not expose the original TradeOps client order ID.

Use a dedicated testnet API/agent wallet for signing.

Mainnet is rejected.

### MEXC Futures

Provider:

```text
MexcFuturesReadOnly
```

Official Contract API does not provide a suitable persistent sandbox/testnet for TradeOps lifecycle testing.

Uses live host only for reads:

```text
https://contract.mexc.com
```

Supported:

- account;
- positions;
- open orders;
- order lookup.

Blocked locally:

- PlaceOrderAsync;
- CancelOrderAsync.

Use read-only API permissions.

### Deribit

Provider:

```text
DeribitTestnet
```

Endpoint:

```text
https://test.deribit.com/api/v2
```

Supports:

- OAuth client_credentials;
- account;
- futures positions;
- open orders;
- lookup by order ID / label;
- buy / sell;
- cancel;
- authenticated private WebSocket order updates;
- authenticated private WebSocket user-trade/fill updates.

Private WebSocket endpoint:

```text
wss://test.deribit.com/ws/api/v2
```

Subscriptions:

```text
user.orders.future.any.raw
user.trades.future.any.100ms
```

Private WebSocket authentication uses `public/auth` with `client_credentials`, followed by `private/subscribe`. Production WebSocket hosts are rejected locally.

Important semantics:

- `Symbol` is native Deribit instrument name, e.g. `BTC-PERPETUAL`;
- `Quantity` is Deribit contracts;
- production host is rejected.

### OKX

Provider:

```text
OkxDemo
```

Uses official OKX REST host with required demo header:

```text
x-simulated-trading: 1
```

Execution is demo-only.

### Bitget

Provider:

```text
BitgetDemo
```

Uses Bitget Demo Trading and required:

```text
paptrading: 1
```

Current adapter is UTA v3 Futures REST.

Supports authenticated reads, place/cancel and deterministic clientOid.

### Gate

Provider:

```text
GateFuturesTestnet
```

Endpoint:

```text
https://api-testnet.gateapi.io/api/v4
```

Supports account, positions, open orders, lookup, place/cancel and ambiguous-placement recovery.

Uses native Gate contracts such as:

```text
BTC_USDT
```

Quantity is a whole number of contracts.

Production Gate hosts are rejected.

### Kraken Futures

Provider:

```text
KrakenFuturesReadOnly
```

Kraken's previously documented public Futures demo was scheduled for decommissioning on 2026-07-14.

Because no replacement persistent public demo lifecycle is currently documented, TradeOps uses the official live Futures REST endpoint only for reads.

Supported:

- account;
- positions;
- open orders;
- open-order lookup.

Blocked locally:

- PlaceOrderAsync;
- CancelOrderAsync.

### KuCoin Futures

Provider:

```text
KuCoinFuturesReadOnly
```

KuCoin suspended its persistent Sandbox API.

The current `/orders/test` endpoint validates requests but does not create a queryable order lifecycle, so it is insufficient for TradeOps reconciliation guarantees.

Supported:

- account overview;
- positions;
- active orders;
- lookup by order ID / clientOid.

Blocked locally:

- PlaceOrderAsync;
- CancelOrderAsync.

### Coinbase International Exchange

Provider:

```text
CoinbaseIntxSandbox
```

This is Coinbase International Exchange perpetual futures, not Coinbase Advanced Trade spot.

Sandbox REST endpoint:

```text
https://api-n5e1.coinbase.com/api/v1
```

Supports:

- portfolio account state;
- perpetual positions;
- open orders;
- order lookup;
- market / limit order placement;
- cancellation;
- ambiguous-placement recovery through client_order_id and fills.

Production INTX REST endpoint is rejected.

REST is implemented; FIX/drop-copy/private streaming is not yet implemented.

## v2.3.0.0 capability-contract milestone

PR #40 added:

```text
src/TradeOps.Application/Models/ExchangeCapabilityProfile.cs
src/TradeOps.Application/Interfaces/IExchangeCapabilityCatalog.cs
src/TradeOps.Infrastructure/Exchange/ExchangeCapabilityCatalog.cs
src/TradeOps.Api/Controllers/ExchangeCapabilitiesController.cs
tests/TradeOps.UnitTests/ExchangeCapabilityCatalogTests.cs
```

It also updated:

```text
src/TradeOps.Infrastructure/Exchange/ExchangeServiceRegistration.cs
README.md
.github/workflows/build.yml
```

Contract tests verify:

- every declared ExchangeProviders constant has exactly one capability profile;
- LiveReadOnly providers cannot advertise mutations;
- only Bybit/Binance/Hyperliquid/Deribit currently advertise private event streams;
- execution is never advertised for LiveReadOnly environments;
- every provider resolves through DI;
- actual IExchangeEventStream wiring matches capability metadata.

PR #40 head:

```text
c990acd0228d46865e310d78fef9f662c8ba580d
```

Merge commit:

```text
29f9d959ff0c949db0e58335ab2a8eaf3a39a343
```

CI:

```text
#518 success
```

## v2.4.0.0 Deribit private-event milestone

PR #42 added authenticated Deribit Testnet private execution events without changing the live-execution safety boundary.

Added:

```text
src/TradeOps.Infrastructure/Exchange/Deribit/DeribitPrivateWebSocketStream.cs
src/TradeOps.Infrastructure/Exchange/Deribit/DeribitWebSocketMessageParser.cs
tests/TradeOps.UnitTests/DeribitWebSocketMessageParserTests.cs
```

Updated:

```text
src/TradeOps.Infrastructure/Exchange/Deribit/DeribitOptions.cs
src/TradeOps.Infrastructure/Exchange/ExchangeServiceRegistration.cs
src/TradeOps.Infrastructure/Exchange/ExchangeCapabilityCatalog.cs
tests/TradeOps.UnitTests/DeribitAdapterTests.cs
tests/TradeOps.UnitTests/ExchangeCapabilityCatalogTests.cs
tests/TradeOps.UnitTests/ExchangeServiceRegistrationTests.cs
README.md
```

The stream:

- connects only to the official Deribit Testnet WebSocket endpoint;
- authenticates with `public/auth` / `client_credentials`;
- subscribes to futures user-order and user-trade channels;
- normalizes order lifecycle events into `ExchangeOrderUpdate`;
- normalizes fills into `ExchangeExecutionUpdate`;
- preserves the existing Deribit contract-quantity conversion semantics;
- allows fill correlation to fall back to persisted `ExchangeOrderId` when a trade notification has no label;
- rejects production WebSocket endpoints.

PR #42 head:

```text
c6866e4ef85187cf102301aac1cb8499c7b03f0c
```

Merge commit:

```text
e9695adc0c50f8ffd9b409c98587b8d4f347ab56
```

CI:

```text
#528 push: success
#529 pull_request: success
```

## Recent venue PR history

```text
#29 Binance Futures User Data Stream
#30 Hyperliquid Testnet Read-Only
#31 Hyperliquid Signing & Execution
#32 Hyperliquid User Stream
#33 MEXC Futures Read-Only
#34 Deribit Testnet REST
#35 OKX Demo REST
#36 Bitget Demo UTA Futures REST
#37 Gate Futures TestNet REST
#38 Kraken + KuCoin Futures Read-Only
#39 Coinbase INTX Sandbox REST
#40 Exchange Capability Contracts
#42 Deribit Private Event Stream
```

All are merged.

## Current development state

There is no active unfinished venue implementation.

Do not restart any of the venue work above.

The repository is now at the point where adding more REST adapters has diminishing value.

## Recommended next technical work

The highest-value next step is exchange-event parity, not another venue.

Recommended order:

1. Continue private event streams / streaming adapters for execution-capable venues that are still REST-only:
   - OKX Demo;
   - Bitget Demo;
   - Gate Futures TestNet;
   - Coinbase INTX Sandbox (likely FIX/drop-copy rather than a simple REST poller).

   Deribit Testnet private order/trade streaming is complete in v2.4.0.0.

2. Extend the capability contract only when a real distinction is needed, for example:
   - order event stream;
   - fill stream;
   - transport type;
   - symbol semantics;
   - quantity semantics.

3. Add a common adapter conformance test harness where practical:
   - read operations;
   - invalid mutation behavior for read-only providers;
   - deterministic client identity;
   - production-host guard;
   - ambiguous placement recovery contract.

4. Do not enable live execution for:
   - MEXC;
   - Kraken;
   - KuCoin;
   unless a safe persistent test environment with queryable order lifecycle is available and verified first.

## Commercial rule

Do not keep adding backend merely to make the repository larger.

New development should be justified by at least one of:

1. a real client / paid-pilot requirement;
2. a reproducible reliability/security issue;
3. a clear exchange-parity gap that materially improves the sellable execution-integration product.

## Upwork acquisition rule

For TradeOps client acquisition:

- prioritize C#/.NET;
- language-neutral jobs are acceptable only if C# is genuinely acceptable;
- reject Python-only / Node-only / Pine-only / MQL-only work;
- reject strategy/alpha/profit-prediction work;
- reject HFT/low-latency specialization unless explicitly chosen as a separate product.

User currently prefers not to waste Connects on poor-fit jobs.

## How to continue in a new chat

Start with:

```text
Продолжаем TradeOps с docs/HANDOFF_NextChat_TradeOps_Current.md.
Текущий main: e9695adc0c50f8ffd9b409c98587b8d4f347ab56.
Продолжай со следующего технического milestone после v2.4.0.0 Deribit Private Event Stream.
```

Before editing code:

1. read this handoff;
2. verify current `main`;
3. inspect recent PR / CI state;
4. do not redo merged milestones.
