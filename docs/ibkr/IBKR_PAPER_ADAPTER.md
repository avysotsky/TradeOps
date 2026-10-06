# Interactive Brokers Paper Adapter — WS-01

## API surface decision

The first IBKR milestone uses the Interactive Brokers Web API through Client Portal Gateway (CPGW).

Why this surface is used first:

- TradeOps exchange adapters already use HTTP-oriented boundaries and `IExchangeClient`.
- Web API exposes contract discovery, account data, positions, order monitoring and trading.
- IBKR Paper accounts are supported by the Web API.
- CPGW is suitable for an individual-client Paper MVP without introducing the TWS Java/C++-style callback SDK into TradeOps.

TWS API / IB Gateway remains an alternative for a later deployment-specific milestone if a client requires its native event model or operational characteristics.

## CPGW constraints

The adapter assumes:

- base URL `https://localhost:5000/v1/api`;
- browser authentication is completed on the same machine as CPGW;
- API calls are made from the same machine;
- one IB username can have only one active brokerage session at a time;
- the session is kept alive externally (IBKR recommends regular `/tickle`);
- CPGW's default self-signed localhost certificate may be accepted only by the dedicated IBKR HttpClient.

## Paper-only safety boundary

The adapter fails closed in two layers:

1. configuration accepts only the local CPGW endpoint;
2. every account/read lifecycle begins with `GET /iserver/accounts` and requires `isPaper=true`.

If the authenticated brokerage session is live, the adapter refuses it.

The first slice deliberately advertises no order-placement or cancellation capability. Mutations throw locally until reply-confirmation handling, retry/idempotency semantics and ambiguous-placement recovery are implemented and validated against an actual IBKR Paper account.

## Instrument resolution

The existing frozen `InstrumentReference v1` is reused unchanged.

First milestone supports:

```text
AssetClass = Stock
Symbol
Currency
VenueInstrumentId = IBKR conid (optional)
Exchange = SMART or listing/routing exchange (optional)
```

When `VenueInstrumentId` is present, the conid is validated against IBKR contract metadata.

When it is absent, the adapter searches the IBKR security-definition API and resolves a unique stock contract using symbol plus optional currency/exchange. Ambiguous results require the caller to provide a conid.

## Read-path mapping

IBKR data is normalized into the existing TradeOps lifecycle:

- account summary -> `AccountInfo`;
- stock positions -> `Position`;
- live/recent orders -> `Order`;
- specific order lookup -> existing `Order` lifecycle state.

No IBKR-specific parallel trading domain is introduced.

## First-slice endpoints

```text
GET /iserver/accounts
GET /iserver/secdef/search
GET /iserver/contract/{conid}/info
GET /iserver/account/{accountId}/summary
GET /portfolio/accounts
GET /portfolio2/{accountId}/positions
GET /iserver/account/orders
GET /iserver/account/order/status/{orderId}
POST /iserver/account   (only for selecting among multiple Paper accounts)
```

## Deferred mutation slice

Before enabling MARKET/LIMIT in capability metadata, implement and paper-test:

- deterministic TradeOps client ID -> IBKR `cOID` semantics;
- `POST /iserver/account/{accountId}/orders`;
- all required `/iserver/reply/{replyId}` confirmation chains;
- cancellation;
- ambiguous placement recovery;
- reconciliation after reconnect/session reset;
- execution/fill ingestion strategy;
- pacing-aware retry behavior.

Live execution remains out of scope.
