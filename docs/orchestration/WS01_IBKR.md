# WS-01 — Interactive Brokers Paper Adapter

State: VALIDATION

Repository:

```text
avysotsky/TradeOps
```

Branch:

```text
TradeOps/ws01-ibkr-paper-adapter
```

Base:

```text
main @ e4466fecab999d83b7b9f9b5353514125b793251
```

Current implementation HEAD:

```text
c43f97bf9e6e26f76aa77add008b02f911a763d3
```

## Goal

Add a paper-first Interactive Brokers integration behind TradeOps broker/execution boundaries.

## API surface decision

Selected for the first milestone:

```text
IBKR Web API via Client Portal Gateway (CPGW)
```

Rationale:

- aligns with the existing HTTP-oriented TradeOps exchange adapter architecture;
- supports IBKR Paper accounts;
- exposes contract discovery, accounts, positions and order monitoring;
- avoids introducing a second callback-oriented SDK architecture before a client deployment requires TWS API / IB Gateway.

Operational constraints are documented in:

```text
docs/ibkr/IBKR_PAPER_ADAPTER.md
```

## Completed slice

Implemented:

1. `IbkrPaper` exchange provider and capability metadata.
2. Local CPGW-only configuration guard:
   `https://localhost:5000/v1/api`.
3. Runtime Paper-session guard using `GET /iserver/accounts` and `isPaper=true`.
4. Optional explicit Paper account selection through `Exchange:Ibkr:AccountId`.
5. Stock instrument resolution from frozen `InstrumentReference v1`:
   - known `VenueInstrumentId/conid` validation;
   - symbol search;
   - currency/exchange filtering;
   - ambiguity rejection.
6. Account summary mapped into existing `AccountInfo`.
7. stock positions mapped into existing `Position`.
8. live/open order snapshot mapped into existing `Order`.
9. order lookup by IBKR order ID and existing client reference.
10. existing `IExchangeClient` / `IExchangeConnectionManager` boundaries preserved.
11. mutation methods fail closed locally.
12. CPGW self-signed certificate acceptance is restricted to the dedicated IBKR HttpClient whose base URL is itself restricted to localhost.

## Shared contracts changed

None.

Frozen contracts remain unchanged:

```text
InstrumentReference v1
ResearchDecision v1
ResearchDecisionAction v1
ResearchDecisionValidationResult v1
```

No second IBKR trading domain was introduced.

## Tests

Added:

```text
tests/TradeOps.UnitTests/IbkrAdapterTests.cs
```

Coverage includes:

- production/non-local Web API host rejection;
- live brokerage-session rejection;
- known conid validation;
- symbol -> unique stock conid resolution;
- account / position / open-order normalization;
- local rejection of order placement/cancellation.

Existing cross-provider capability and DI contract tests also include `IbkrPaper` automatically.

## CI

Implementation HEAD:

```text
c43f97bf9e6e26f76aa77add008b02f911a763d3
```

GitHub Actions:

```text
build #581
run id: 37509874185
conclusion: success
```

Successful stages include build, unit tests, API/PostgreSQL smoke, both existing E2E demos, deployment validation and Docker image build.

Validation level:

```text
implemented: yes
CI tested: yes
IBKR Paper/CPGW tested against a real authenticated account: no
live tested: no
```

## Blockers / safety boundary

No blocker for code-level review of the read-path slice.

The mutation slice is intentionally not enabled yet because IBKR order submission can return one or more mandatory `/iserver/reply/{replyId}` warning/confirmation messages that must be handled immediately and deterministically. Reliable validation also requires an authenticated real IBKR Paper CPGW session, which CI does not provide.

Current capability metadata therefore advertises:

```text
account read: yes
positions read: yes
open orders read: yes
order lookup: yes
order placement: no
order cancellation: no
private event stream: no
```

Live execution remains prohibited.

## Next integration action

Development Orchestrator should review this read-path slice for integration readiness.

Recommended next WS-01 work after that decision:

1. run a real CPGW Paper smoke test for session/account/positions/orders;
2. define deterministic TradeOps client-order identity -> IBKR `cOID`;
3. implement MARKET/LIMIT Paper submission with complete reply-confirmation chain handling;
4. add cancel + ambiguous-placement recovery;
5. validate reconciliation and execution/fill ingestion;
6. enable mutation capability metadata only after Paper validation succeeds.
