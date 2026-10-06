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
b1523346988c705d9ae36576473a204f059770a4
```

Latest completed milestone:

```text
TradeOps v2.6.0.0 — Broker-Neutral Research Decision Contracts
PR #46
CI #552: success
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

## New commercial direction — broker + research automation

A current Upwork lead surfaced a concrete adjacent use case:

```text
global stock universe
-> earnings calls / filings / company data
-> structured research interpretation
-> trading / portfolio decision
-> TradeOps risk and execution
-> Interactive Brokers Paper
-> small live rollout only after validation
```

This changes near-term development priority.

Do not continue adding crypto venue private streams merely for parity while this broker/research opportunity is being prepared. Bitget / Gate / Coinbase streaming remain valid backlog items, but they are no longer the immediate next milestones.

TradeOps must remain the execution / risk / reliability boundary. Do not turn TradeOps itself into the earnings-transcript ingestion or LLM reasoning service.

Preferred architecture:

```text
Earnings calls / filings / transcripts
        |
        v
Research ingestion / AI service
(DocFlow-like capability; separate bounded context)
        |
        v
Structured ResearchDecision
        |
        v
TradeOps
        |
        +--> deterministic validation
        +--> portfolio / risk rules
        +--> broker-neutral instrument resolution
        +--> execution / reconciliation / audit
        |
        v
Interactive Brokers Paper
        |
        v
Live only after explicit validated rollout
```

The external research/AI layer may identify facts, scores, commentary changes or proposed portfolio targets. It must not bypass deterministic TradeOps validation and risk controls.

### New roadmap

Immediate milestones:

```text
v2.6.0.0 — Broker-Neutral Instruments & Research Decision Contract
v2.7.0.0 — Interactive Brokers Paper Adapter
v2.8.0.0 — Portfolio Target & Rebalancing Engine
v2.9.0.0 — Research -> TradeOps intake / evidence workflow
```

#### v2.6.0.0 — Broker-Neutral Instruments & Research Decision Contract

Purpose:

- define instrument identity without assuming crypto symbols;
- support stock identity needed by brokers such as IBKR;
- define a structured contract between research/AI and TradeOps;
- keep research output distinct from executable orders;
- add deterministic validation before later portfolio/execution translation;
- avoid breaking existing crypto adapters and signal execution.

Expected primitives:

```text
AssetClass
InstrumentReference
ResearchDecisionAction
ResearchDecision
ResearchDecisionValidationResult / validator
```

An instrument reference should be able to carry:

```text
Symbol
AssetClass
Currency
VenueInstrumentId
Exchange
```

For IBKR, `VenueInstrumentId` is expected to carry a broker contract identifier such as `conid` when known, while `Exchange` can represent routing such as SMART.

A research decision should support machine-readable provenance and intent, for example:

```json
{
  "decisionId": "AAPL-Q4-2026-guidance",
  "strategyId": "earnings-quality-v1",
  "instrument": {
    "symbol": "AAPL",
    "assetClass": "Stock",
    "currency": "USD",
    "venueInstrumentId": "265598",
    "exchange": "SMART"
  },
  "action": "SetTargetWeight",
  "targetWeight": 0.04,
  "confidence": 0.82,
  "sourceEventId": "AAPL-Q4-2026",
  "reason": "guidance-raised"
}
```

No AI or research decision is allowed to directly bypass risk / portfolio / order lifecycle controls.

#### v2.7.0.0 — Interactive Brokers Paper Adapter

Prepare a paper-first IBKR execution path:

```text
TradeOps
-> broker instrument resolution
-> IBKR Paper
-> place order
-> order state
-> partial fills / executions
-> positions
-> reconciliation
```

Current IBKR documentation confirms:

- Web API provides trading, portfolio, contract and WebSocket access;
- TWS API supports C# and emits order / execution events;
- Paper Trading Accounts support simulated testing, with known simulation differences from live trading.

Before implementation, choose the API surface based on the actual client environment:

```text
Web API / Client Portal or OAuth
vs
TWS API / IB Gateway
```

Do not enable live IBKR execution in the first adapter milestone.

#### v2.8.0.0 — Portfolio Target & Rebalancing Engine

Support long-horizon stock workflows where research emits target exposures rather than immediate market-order instructions.

Concept:

```text
current portfolio
+ target weights
+ cash / risk constraints
-> delta calculation
-> deterministic rebalance plan
-> orders
```

This is a better fit for earnings/fundamental strategies than latency-sensitive news trading.

#### v2.9.0.0 — Research intake / evidence

Add a reliable API boundary and audit trail for research decisions:

- source event identity;
- idempotency / duplicate protection;
- structured metadata;
- validation status;
- portfolio decision linkage;
- execution linkage;
- evidence export.

The earnings/transcript ingestion implementation itself should remain in a separate research/DocFlow-style service and call TradeOps through this contract.

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
| OkxDemo | OKX | Demo | yes | yes | yes, REST protected by simulated-trading header; WebSocket uses demo host |
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

Private WebSocket:

```text
wss://wspap.okx.com/ws/v5/private
```

The portless endpoint is the default. Legacy `:8443` is temporarily accepted during OKX's 2026 port migration, but production `ws.okx.com` is rejected.

Private-stream flow:

- WebSocket login with API key / passphrase and HMAC-SHA256 signature over `timestamp + GET + /users/self/verify`;
- subscribe to the `orders` channel for `SWAP` and `FUTURES`;
- normalize order updates into `ExchangeOrderUpdate`;
- derive incremental executions from `tradeId`, `fillSz`, `fillPx`, `fillFee`, and `fillFeeCcy`;
- use `okx:{instId}:{tradeId}` as execution identity because OKX trade IDs are instrument-scoped;
- normalize OKX fee signs to the TradeOps accounting convention: fee paid positive, rebate negative;
- application-level `ping` heartbeat keeps idle private connections alive.

The VIP4-only dedicated `fills` channel is intentionally not required; the generally available `orders` channel carries the fill data needed by TradeOps.

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
- only Bybit/Binance/Hyperliquid/Deribit/OKX currently advertise private event streams;
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

## v2.5.0.0 OKX Demo private-event milestone

PR #44 added authenticated OKX Demo private execution events while preserving the existing Demo-only execution boundary.

Added:

```text
src/TradeOps.Infrastructure/Exchange/Okx/OkxPrivateWebSocketStream.cs
src/TradeOps.Infrastructure/Exchange/Okx/OkxWebSocketMessageParser.cs
tests/TradeOps.UnitTests/OkxWebSocketMessageParserTests.cs
```

Updated:

```text
src/TradeOps.Infrastructure/Exchange/Okx/OkxOptions.cs
src/TradeOps.Infrastructure/Exchange/ExchangeServiceRegistration.cs
src/TradeOps.Infrastructure/Exchange/ExchangeCapabilityCatalog.cs
tests/TradeOps.UnitTests/OkxAdapterTests.cs
tests/TradeOps.UnitTests/ExchangeCapabilityCatalogTests.cs
tests/TradeOps.UnitTests/ExchangeServiceRegistrationTests.cs
README.md
```

The stream:

- connects only to the official Global OKX Demo private WebSocket host;
- uses the portless 443 endpoint by default ahead of the announced 8443 retirement;
- authenticates with the documented WebSocket login signature;
- subscribes to derivative `orders` for `SWAP` and `FUTURES`;
- normalizes order lifecycle and incremental fills;
- does not depend on the VIP4-only `fills` channel;
- converts OKX fee/rebate sign convention into TradeOps accounting semantics;
- uses instrument-scoped execution identity for duplicate protection;
- sends application-level `ping` heartbeats;
- fails closed on production/private-WebSocket host configuration.

PR #44 head:

```text
74ce768ca28e05be2d7bd98748817fed2e931c50
```

Merge commit:

```text
6cb17fc75b3d84de0fe49fbf2b0779f24f3e226c
```

CI:

```text
#540 push: success
#541 pull_request: success
```

## v2.6.0.0 broker-neutral research-contract milestone

PR #46 established the boundary between external research/AI output and TradeOps execution.

Added:

```text
src/TradeOps.Domain/Enums/AssetClass.cs
src/TradeOps.Domain/Enums/ResearchDecisionAction.cs
src/TradeOps.Application/Models/InstrumentReference.cs
src/TradeOps.Application/Models/ResearchDecision.cs
src/TradeOps.Application/Models/ResearchDecisionValidationResult.cs
src/TradeOps.Application/Services/ResearchDecisionValidator.cs
src/TradeOps.Api/Controllers/ResearchDecisionsController.cs
tests/TradeOps.UnitTests/ResearchDecisionValidatorTests.cs
```

Updated:

```text
README.md
docs/HANDOFF_NextChat_TradeOps_Current.md
```

The milestone provides:

- broker-neutral asset classes;
- instrument identity with symbol, currency, exchange and optional venue-specific instrument ID;
- explicit research actions including `NoAction`, `Buy`, `Sell`, `Exit`, and `SetTargetWeight`;
- explicit `Unknown = 0` action so omitted JSON actions cannot silently become valid;
- source-event identity, confidence, reason and metadata provenance;
- deterministic validation and normalization;
- validation-only API endpoint:
  `POST /api/research-decisions/validate`;
- no automatic conversion from research output to executable orders.

For an IBKR stock, the contract can represent data such as:

```text
Symbol = AAPL
AssetClass = Stock
Currency = USD
VenueInstrumentId = 265598
Exchange = SMART
```

This is aligned with IBKR's current contract model where `conid` plus exchange is the preferred unambiguous contract identity.

PR #46 head:

```text
2915251a8b09aa3f72fb736f3520d72489001634
```

Merge commit:

```text
b1523346988c705d9ae36576473a204f059770a4
```

CI:

```text
#552 push: success
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
#44 OKX Demo Private Event Stream
```

All are merged.

## Current development state

There is no unfinished implementation branch to resume.

The crypto venue milestones above are complete. Do not restart them.

The broker-neutral research contract is now complete in v2.6.0.0. The next active direction is the paper-first Interactive Brokers adapter, while lower-priority crypto exchange parity remains backlog.

## Recommended next technical work

The immediate priority has changed from crypto exchange-event parity to broker/research preparation driven by a concrete prospective-client use case.

Recommended order:

1. Build **v2.7.0.0 Interactive Brokers Paper Adapter**.
   - default technical preference: IBKR Web API for platform-neutral HTTP/WebSocket integration;
   - reconsider TWS API / IB Gateway only when a client deployment specifically requires it;
   - paper-only first; do not enable live execution.
2. Build **v2.8.0.0 Portfolio Target & Rebalancing Engine**.
3. Build **v2.9.0.0 Research -> TradeOps intake / evidence workflow**.
4. Return to crypto streaming parity when justified by a paid-pilot requirement:
   - Bitget Demo;
   - Gate Futures TestNet;
   - Coinbase INTX Sandbox.

Do not enable live execution for:
- Interactive Brokers in the initial adapter milestone;
- MEXC;
- Kraken;
- KuCoin;
unless a safe test/paper environment and explicit rollout requirements are validated first.

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
Текущий main: b1523346988c705d9ae36576473a204f059770a4.
v2.6.0.0 Broker-Neutral Research Decision Contracts завершён.
Продолжай v2.7.0.0 Interactive Brokers Paper Adapter, затем portfolio target/rebalancing.
```

Before editing code:

1. read this handoff;
2. verify current `main`;
3. inspect recent PR / CI state;
4. do not redo merged milestones.
