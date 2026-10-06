# WS-04 — Event-Driven Backtester

State: BLOCKED_IMPLEMENTATION

Repository:

```text
avysotsky/TradeOps
```

Branch:

```text
TradeOps/ws04-backtester-contracts
```

Base:

```text
main @ cd62df96fdec27e047aac63818e49aa46ba724f2
```

## Goal

Evaluate long-horizon earnings/research decisions with event-time correctness and the same portfolio decision semantics used by production TradeOps.

## Allowed work now

Contract/design only:

- event clock;
- market data bar abstraction;
- execution-price policy;
- portfolio snapshot interface;
- performance metrics contract;
- explicit look-ahead-bias rules;
- fixtures.

Do not build the full simulator yet.

## Implementation blockers

Need stable outputs from:

```text
WS-02 EarningsSnapshot / event-time contract
WS-03 RebalancePlan / order-intent contract
```

## Required correctness properties

- earnings information cannot be used before its actual publication timestamp;
- after-hours releases cannot fill at an earlier regular-session close;
- delisted/survivorship handling must be explicit for broad-universe tests;
- commissions/slippage are configurable;
- metrics separate signal quality from portfolio/execution effects;
- backtest output never implies guaranteed future profitability.

## Initial metrics

```text
event count
hit rate by horizon
return 1/5/20/60/250 trading days
portfolio CAGR
max drawdown
Sharpe
Sortino
turnover
exposure
```

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
