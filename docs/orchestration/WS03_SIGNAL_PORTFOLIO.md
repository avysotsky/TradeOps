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

## RebalancePlan v1 scope

The first WS-03 contract is pure application-layer calculation:

```text
validated ResearchDecision
+ PortfolioSnapshot
+ reference price
+ RebalanceConstraints
-> deterministic target/current/delta calculation
-> constraint evaluation
-> RebalancePlan
-> broker-neutral pre-risk RebalanceOrderIntent
```

Initial scope:

- stocks only;
- long-only;
- target weights;
- no leverage;
- no FX conversion;
- no live execution;
- no broker SDK dependency.

## Architecture audit

Existing TradeOps execution is currently centered on:

```text
TradingSignal
-> RiskEngine
-> OrderManager
-> PlaceOrderRequest
-> IExchangeClient
-> OrderStateMachine
```

The existing RiskEngine is quantity/symbol based and exchange-position aware.
The existing OrderManager accepts TradingSignal and creates executable PlaceOrderRequest only after risk approval.
The existing PositionService is currently symbol based.

Therefore WS-03 does not bypass or directly reuse those execution models as its calculation core.
RebalanceOrderIntent remains a broker-neutral pre-risk intent and must later be adapted through the TradeOps risk/execution boundary.
Final adapter integration remains dependent on WS-01.

## Current status

```text
State: READY_FOR_INTEGRATION
Current implementation HEAD: f26207627f484bbab2900952e8afcde30fb2575e

Completed:
- Added PortfolioSnapshot / PortfolioPosition.
- Added TargetPosition.
- Added RebalanceConstraints.
- Added RebalanceConstraintViolation.
- Added RebalancePlan / RebalancePlanStatus.
- Added broker-neutral RebalanceOrderIntent with explicit downstream risk-approval boundary.
- Added pure deterministic PortfolioRebalancePlanner for SetTargetWeight.
- Added explicit CurrentNotional and DeltaNotional to RebalancePlan.
- Implemented long-only and max-target-weight validation.
- Implemented available-cash and optional minimum-cash-reserve validation.
- Implemented minimum-trade-notional suppression.
- Implemented independent near-zero quantity suppression through QuantityTolerance.
- Implemented expired-decision validation.
- Implemented DecisionNotYetAvailable validation when GeneratedAt is after PortfolioSnapshot.AsOf.
- Implemented optional MaximumDecisionAge / DecisionStale validation.
- Implemented unsupported action and unsupported asset-class blocking.
- Implemented missing/invalid reference-price blocking.
- Implemented currency-mismatch blocking because v1 does not perform FX conversion.
- Invalid ResearchDecision now produces a deterministic Blocked plan instead of broker/execution activity.
- Added explicit 1.8% -> 4.0% / NAV $100,000 calculation coverage:
  current notional $1,800 -> target $4,000 -> delta +$2,200.

Shared contracts changed:
- Frozen ResearchDecision v1: unchanged.
- Frozen InstrumentReference v1: unchanged.
- Frozen ResearchDecisionValidationResult v1: unchanged.
- Frozen ResearchDecisionAction v1: unchanged.
- WS-03 proposed public contract evolved before integration:
  PortfolioSnapshot,
  PortfolioPosition,
  TargetPosition,
  RebalanceConstraints,
  RebalanceConstraintViolation,
  RebalancePlan,
  RebalancePlanStatus,
  RebalanceOrderIntent.

Tests:
- PortfolioRebalancePlannerTests cover:
  exact 1.8% -> 4.0% example,
  new buy,
  sell-down,
  target zero / exit,
  exact no-op,
  QuantityTolerance suppression,
  minimum-trade suppression,
  insufficient cash,
  cash reserve,
  max weight,
  negative target,
  expired decision,
  future-generated decision,
  stale decision,
  missing price,
  invalid ResearchDecision,
  unsupported asset class,
  unsupported action.
- Existing repository test suite remains green.

CI:
- build run #583
- run id: 37510423419
- validated implementation SHA: f26207627f484bbab2900952e8afcde30fb2575e
- conclusion: success
- Restore: success
- Build: success
- Unit tests: success
- API + PostgreSQL smoke: success
- Signed webhook demo: success
- Customer TradingView demo: success
- deployment/config validation: success
- Docker image build: success

Blockers:
- None for RebalancePlan v1 calculation semantics.
- Final execution/risk bridge depends on WS-01 broker adapter/output semantics and an orchestrator-approved integration design.
- Existing RiskEngine cannot yet consume RebalanceOrderIntent directly without a dedicated bridge; WS-03 intentionally does not modify that shared execution path in this milestone.

Next integration action:
- Development Orchestrator reviews RebalancePlan v1 / RebalanceOrderIntent for contract freeze.
- Orchestrator checks overlap against WS-01 and current main.
- Merge only through orchestrated integration.
- Once RebalancePlan v1 is frozen, WS-04 may reuse the same planner semantics for historical replay.
```
