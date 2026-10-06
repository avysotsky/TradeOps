# WS-03 — Signal / Portfolio Target Engine

State: READY_FOR_INTEGRATION

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
main @ e4466fecab999d83b7b9f9b5353514125b793251
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

## Current status

```text
State: READY_FOR_INTEGRATION
Current implementation HEAD: 8005d944ac092ec1c4f96602451dc2575687d8d3
Completed:
- Added PortfolioSnapshot / PortfolioPosition.
- Added TargetPosition.
- Added RebalanceConstraints and structured RebalanceConstraintViolation.
- Added RebalancePlan / RebalancePlanStatus.
- Added broker-neutral RebalanceOrderIntent with explicit downstream risk-approval boundary.
- Added deterministic PortfolioRebalancePlanner for SetTargetWeight.
- Implemented long-only stock checks, target notional/quantity, delta, minimum-trade, cash-reserve, max-target-weight, expiry, and single-currency checks.
- Added unit coverage for buy, sell, exit-to-zero, no-op, minimum-trade suppression, insufficient cash, max weight, negative target, expiry, and non-stock rejection.
Shared contracts changed:
- Frozen ResearchDecision v1: unchanged.
- Frozen InstrumentReference v1: unchanged.
- New WS-03 public contracts added: PortfolioSnapshot, PortfolioPosition, TargetPosition, RebalanceConstraints, RebalanceConstraintViolation, RebalancePlan, RebalancePlanStatus, RebalanceOrderIntent.
Tests:
- GitHub Actions build succeeded.
- Unit tests succeeded, including PortfolioRebalancePlannerTests.
- Existing API/PostgreSQL smoke and repository validation steps also succeeded.
CI:
- build run #578
- run id: 37509185570
- conclusion: success
- validated implementation SHA: 8005d944ac092ec1c4f96602451dc2575687d8d3
Blockers:
- None for the first WS-03 slice.
- Final broker execution mapping remains dependent on WS-01 broker adapter/output semantics.
Next integration action:
- Orchestrator reviews the new rebalance-plan contract and changed-file overlap.
- Merge when orchestration order permits.
- After contract freeze, WS-04 may consume RebalancePlan / RebalanceOrderIntent semantics.
```
