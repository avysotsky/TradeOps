# WS-03 — Signal / Portfolio Target Engine

State: INTEGRATED

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

Integrated:

```text
PR #49
worker HEAD: 3e5796bf815f64c9d9bc6adbb4391239fd3ca157
merge commit: 3f7aa84565fbd77f202821f72c36160c001774f3
worker CI: 37510799848 — success
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

The integrated WS-03 contract is pure application-layer calculation:

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

## Architecture boundary

Existing TradeOps execution remains:

```text
TradingSignal
-> RiskEngine
-> OrderManager
-> PlaceOrderRequest
-> IExchangeClient
-> OrderStateMachine
```

`RebalanceOrderIntent` is not an executable order. It explicitly remains behind the downstream TradeOps risk/emergency-stop boundary.

WS-03 does not modify `RiskEngine`, `OrderManager`, or any broker adapter.

## Integrated contract

Added:

```text
PortfolioSnapshot
PortfolioPosition
TargetPosition
RebalanceConstraints
RebalanceConstraintViolation
RebalancePlan
RebalancePlanStatus
RebalanceOrderIntent
PortfolioRebalancePlanner
```

Implemented:

- target/current/delta quantity and notional calculation;
- long-only validation;
- max-target-weight validation;
- available-cash and minimum-cash-reserve validation;
- minimum-trade-notional suppression;
- quantity-tolerance suppression;
- decision expiry / future-availability / staleness validation;
- missing/invalid reference-price blocking;
- currency-mismatch blocking because v1 does not perform FX conversion;
- deterministic blocked plans for invalid research decisions;
- explicit 1.8% -> 4.0% / NAV $100,000 test case.

## Shared contracts changed

Frozen shared contracts were not changed:

```text
InstrumentReference v1
ResearchDecision v1
ResearchDecisionAction v1
ResearchDecisionValidationResult v1
```

The WS-03 rebalance contracts above are now the integrated v1 portfolio-planning boundary and should not be changed by another worker without orchestrator approval.

## Tests / CI

Worker HEAD:

```text
3e5796bf815f64c9d9bc6adbb4391239fd3ca157
```

GitHub Actions:

```text
run id: 37510799848
conclusion: success
```

Coverage includes target/delta calculation, new buy, sell-down, target zero, no-op, tolerance/minimum-trade suppression, cash constraints, max weight, expiry/staleness, price validation, invalid decision, unsupported asset class and unsupported action.

## Blockers

None for RebalancePlan v1 semantics.

The production execution bridge from `RebalanceOrderIntent` into existing TradeOps risk/order lifecycle is not implemented yet. That integration depends on the orchestrator-approved bridge design and WS-01 broker capabilities.

## Next integration action

1. Treat `RebalancePlan` / `RebalanceOrderIntent` v1 semantics as frozen for downstream work.
2. WS-04 may reuse this exact planner boundary for historical replay.
3. Do not add a second backtest-only portfolio-decision model.
4. Build the execution/risk bridge only after WS-01's broker boundary is integrated and reviewed.
