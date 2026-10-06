# WS-01 — Interactive Brokers Paper Adapter

State: WAITING_FOR_REAL_PAPER_ACCOUNT

Repository:

```text
avysotsky/TradeOps
```

Branch:

```text
TradeOps/ws01-ibkr-paper-adapter
```

Original base:

```text
main @ e4466fecab999d83b7b9f9b5353514125b793251
```

Integrated:

```text
PR #50
worker HEAD: 78345b1d219ac0924886f61f17cc8b6243682f8d
merge commit: 2c9acfa9a19b1ebe82add786c66656a4458e885d
worker CI: 37510272883 — success
```

## Integrated scope

- IBKR Web API via local Client Portal Gateway.
- Base endpoint restricted to `https://localhost:5000/v1/api`.
- Runtime Paper-session enforcement through `isPaper=true`.
- Optional explicit Paper account selection.
- Stock/conid instrument resolution using frozen `InstrumentReference v1`.
- Account summary.
- Positions.
- Open orders and order lookup.
- Existing `IExchangeClient` / connection boundaries preserved.
- Dedicated local self-signed certificate handling only for the restricted localhost CPGW client.

## Safety boundary

Order mutation is intentionally disabled:

```text
PlaceOrderAsync -> NotSupportedException
CancelOrderAsync -> NotSupportedException
```

Capability metadata advertises:

```text
account read: yes
positions read: yes
open orders read: yes
order lookup: yes
order placement: no
order cancellation: no
private event stream: no
```

No live IBKR execution is enabled.

## Shared contracts changed

None.

Frozen contracts remain unchanged:

```text
InstrumentReference v1
ResearchDecision v1
ResearchDecisionAction v1
ResearchDecisionValidationResult v1
```

## Validation

Worker CI:

```text
run id: 37510272883
conclusion: success
```

Validation level:

```text
implemented: yes
CI tested: yes
real authenticated IBKR Paper/CPGW smoke: not yet
live tested: no
```

## Next slice / gate

Before enabling mutations:

1. run real authenticated CPGW Paper smoke for session/account/positions/orders;
2. define deterministic TradeOps client-order identity -> IBKR cOID;
3. implement MARKET/LIMIT Paper submission including full reply-confirmation chain;
4. implement cancel and ambiguous-placement recovery;
5. validate fills/reconciliation against real Paper;
6. only then advertise mutation capability.

Real Paper validation is a hard gate before order placement/cancellation can be enabled.


## External validation dependency

A real authenticated IBKR Paper read-path smoke cannot currently be executed because no IBKR client/Paper account is available to the repository owner.

This is an external validation dependency, not a code blocker.

Until an IBKR Paper account is available, WS-01 may only:

- keep the integrated read-only adapter green against current main;
- provide an opt-in manual CPGW smoke harness and exact local run instructions;
- preserve the Paper-only runtime guard;
- preserve mutation capabilities as disabled.

WS-01 must not:

- simulate or claim a successful real-account validation;
- enable PlaceOrder or CancelOrder capability metadata;
- implement or advertise live execution.

The client-facing research/backtest/rebalance demo may proceed independently of this external broker-account dependency.
