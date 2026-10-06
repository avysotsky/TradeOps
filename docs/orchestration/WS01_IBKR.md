# WS-01 — Interactive Brokers Paper Adapter

State: READY

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
main @ cd62df96fdec27e047aac63818e49aa46ba724f2
```

## Goal

Add a paper-first Interactive Brokers integration behind TradeOps broker/execution boundaries.

## First implementation slice

Do not start with every IBKR feature.

Deliver:

1. select/document API surface for the first milestone;
2. paper-only configuration and production/live safety guard;
3. stock instrument resolution using `InstrumentReference`;
4. account summary;
5. positions;
6. open orders / order lookup;
7. one minimal market/limit order path only if safe paper lifecycle is testable;
8. deterministic client identity / retry semantics;
9. tests and capability metadata.

## Constraints

- Paper only.
- No live account order placement.
- Do not implement strategy logic.
- Do not change ResearchDecision v1.
- Preserve existing exchange adapters.
- Explicitly document TWS/IB Gateway or Web API session constraints.

## Integration contract

Input identity:

```text
InstrumentReference
Symbol
AssetClass=Stock
Currency
VenueInstrumentId/conid when known
Exchange/SMART when applicable
```

Output must map into existing TradeOps order/position lifecycle instead of creating an IBKR-only parallel domain model.

## Status update template

```text
State:
Current HEAD:
Completed:
Shared contracts changed:
Tests:
CI:
Blockers:
Next integration action:
```
