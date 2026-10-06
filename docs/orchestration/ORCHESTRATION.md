# Development Orchestration

## Purpose

This directory is the cross-chat source of truth for parallel development.

Chat history is not the coordination mechanism. GitHub branches, commits, CI and these status files are.

## Current baseline

TradeOps repository:

```text
avysotsky/TradeOps
main: cd62df96fdec27e047aac63818e49aa46ba724f2
latest completed milestone: v2.6.0.0 — Broker-Neutral Research Decision Contracts
```

DocFlow repository:

```text
avysotsky/DocFlow
branch: DocFlow/v_1.1.1.34_AccountingPosting
validated sandbox-readiness head: c03e71f0a2c1c4ae414c083fc1f53a6351c3b4bf
status: HOLD / maintenance unless business priority changes
```

## Current product objective

Prepare a demonstrable long-horizon stock research-to-execution workflow for the prospective Upwork use case:

```text
company universe
-> earnings / filings / transcripts
-> structured research facts
-> ResearchDecision
-> deterministic portfolio/risk translation
-> TradeOps
-> Interactive Brokers Paper
```

Do not position this as an alpha-generation or guaranteed-profit system.

## Shared contract boundary

TradeOps v2.6.0.0 freezes the first shared contract:

```text
InstrumentReference
ResearchDecision
ResearchDecisionAction
ResearchDecisionValidationResult
```

Canonical code lives in:

```text
src/TradeOps.Application/Models/InstrumentReference.cs
src/TradeOps.Application/Models/ResearchDecision.cs
src/TradeOps.Application/Models/ResearchDecisionValidationResult.cs
src/TradeOps.Application/Services/ResearchDecisionValidator.cs
```

Worker branches must not change this v1 contract without an explicit orchestrator decision.

## Workstreams

| ID | Workstream | Branch | State | Can run now |
|---|---|---|---|---|
| WS-01 | IBKR Paper Adapter | TradeOps/ws01-ibkr-paper-adapter | READY | yes |
| WS-02 | Earnings Intelligence / research integration | TradeOps/ws02-earnings-intelligence-contract | READY | yes |
| WS-03 | Signal / Portfolio Target Engine | TradeOps/ws03-signal-portfolio-engine | READY_WITH_CONTRACT | yes, bounded |
| WS-04 | Event-driven Backtester | TradeOps/ws04-backtester-contracts | BLOCKED_IMPLEMENTATION | contract/design only |
| DF-01 | DocFlow QBO | avysotsky/DocFlow / v1.1.1.34 | HOLD | no, unless reprioritized |

## Dependency graph

```text
                 +------------------+
                 | WS-01 IBKR Paper |
                 +---------+--------+
                           |
                           v
                    execution boundary
                           |
+-------------------+      |      +--------------------------+
| WS-02 Earnings    |----->+----->| WS-03 Portfolio Engine   |
| facts / decisions |             | decision -> target/delta |
+---------+---------+             +------------+-------------+
          |                                      |
          +------------------+-------------------+
                             v
                    +--------+---------+
                    | WS-04 Backtester |
                    +------------------+
```

WS-01 and WS-02 are independent and should proceed in parallel.

WS-03 may implement deterministic translation against the frozen ResearchDecision v1 contract, but must avoid assumptions about a specific earnings provider.

WS-04 implementation waits until the event/research snapshot contract from WS-02 and portfolio-decision output contract from WS-03 are stable.

## Integration order

1. Merge orchestration layer.
2. WS-01: paper-only IBKR connectivity/instrument/account/read path.
3. WS-02: earnings/research event contract + one deterministic source path.
4. WS-03: target-weight/rebalance planning contract.
5. Integrate WS-01 execution with WS-03 rebalance output.
6. Freeze historical event contract.
7. WS-04: event-driven backtester.
8. End-to-end demo: earnings event -> ResearchDecision -> rebalance -> IBKR Paper.

## Ownership / merge-conflict rules

### Orchestrator owns

```text
docs/orchestration/**
cross-workstream contract decisions
integration order
merge readiness
priority changes
```

### WS-01 owns

IBKR-specific adapter, provider configuration, instrument resolution, paper-account reads/order lifecycle and IBKR tests.

It must not implement earnings parsing or portfolio strategy logic.

### WS-02 owns

Research/earnings source ingestion contract and transformation into the frozen TradeOps ResearchDecision boundary.

The actual transcript/filing ingestion service should remain a separate bounded context. Do not turn TradeOps into an LLM/document-ingestion application.

### WS-03 owns

Deterministic translation from validated ResearchDecision / target weights into portfolio/rebalance plans and risk-aware execution intents.

It must not call an LLM or fetch earnings data.

### WS-04 owns

Historical event replay, timestamp correctness, look-ahead-bias prevention, portfolio simulation and evaluation metrics.

It must not define a second production decision contract.

## State machine

```text
PLANNED
-> READY
-> IN_PROGRESS
-> VALIDATION
-> READY_FOR_INTEGRATION
-> INTEGRATED
-> DONE
```

`BLOCKED` can be entered from any pre-integration state with a named dependency.

## Worker completion protocol

Every worker must update its own status file before asking for integration.

Required fields:

```text
State
Branch
Current HEAD
Completed
Public/shared contracts changed
Tests
CI run + conclusion
Blockers
Next integration action
```

A worker may not claim DONE solely because local tests pass. CI or an explicit orchestrator waiver is required.

## Orchestrator review protocol

On each "continue" or "check status":

1. fetch current main heads for TradeOps and DocFlow;
2. fetch each active worker branch HEAD;
3. compare each worker branch with its base/main;
4. inspect current CI;
5. read changed workstream status files;
6. identify dependency changes or overlapping files;
7. choose which workstreams continue, block, or integrate;
8. update this file only when the plan/status materially changes.

## Current priority

```text
P0  WS-01 IBKR Paper Adapter
P0  WS-02 Earnings Intelligence boundary / MVP
P1  WS-03 Portfolio Target & Rebalancing Engine
P2  WS-04 Backtester
HOLD DocFlow QBO sandbox work
```
