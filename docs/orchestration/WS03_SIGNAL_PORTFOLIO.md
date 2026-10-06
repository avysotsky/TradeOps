# WS-03 — Signal / Portfolio Target Engine

State: READY_WITH_CONTRACT

Repository:

```text
avysotsky/TradeOps
```

Branch:

```text
TradeOps/ws03-signal-portfolio-engine
```

Base:

```text
main @ cd62df96fdec27e047aac63818e49aa46ba724f2
```

## Goal

Translate validated research intent into deterministic portfolio actions without allowing research/AI output to bypass TradeOps risk controls.

## Inputs

Frozen:

```text
ResearchDecision v1
InstrumentReference v1
```

Primary first action:

```text
SetTargetWeight
```

## First implementation slice

Define and test:

```text
PortfolioSnapshot
TargetPosition
RebalanceConstraint(s)
RebalancePlan
RebalanceOrderIntent
```

Then implement:

```text
current position + NAV/cash + target weight
-> target notional/quantity
-> delta
-> minimum trade / cash / max weight checks
-> deterministic rebalance plan
```

Start with long-only stocks unless the orchestrator explicitly expands scope.

## Constraints

- No LLM.
- No SEC/transcript fetching.
- No direct IBKR SDK dependency.
- No live order placement.
- Do not mutate ResearchDecision v1.
- Preserve risk/emergency-stop boundary.

## Dependency

May proceed now against ResearchDecision v1.

Final execution integration depends on WS-01's broker adapter/output semantics.

Backtester WS-04 depends on this workstream freezing the rebalance-plan semantics.

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
