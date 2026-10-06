# WS-04 — Event-Driven Backtester

State: READY

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

## Implementation status

Full bounded implementation may now begin.

Integrated dependencies:

```text
WS-02 via PR #51:
EarningsEvent / EarningsSnapshot
PublishedAt + provenance / anti-look-ahead semantics
ResearchDecision(Action = SetTargetWeight)

WS-03 via PR #49:
PortfolioSnapshot
RebalanceConstraints
PortfolioRebalancePlanner
RebalancePlan
RebalanceOrderIntent
```

WS-04 must reuse these production contracts. Do not create backtest-only substitutes for ResearchDecision, EarningsEvent, PortfolioSnapshot or RebalancePlan.

First bounded implementation should cover:

- deterministic event clock;
- market-data bar abstraction;
- execution-price policy;
- historical portfolio snapshot construction;
- replay through the actual PortfolioRebalancePlanner;
- commissions/slippage configuration;
- event-time/look-ahead enforcement;
- core performance metrics;
- deterministic fixtures/tests.

Do not integrate broker-specific IBKR APIs into the backtester.

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

## Dependency decision

The previous implementation blocker is resolved as of:

```text
WS-03 PR #49 merged
WS-02 PR #51 merged
```

The worker should synchronize this branch to current `main` before implementation.

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
