# Development Orchestration

## Purpose

This directory is the cross-chat source of truth for parallel development.

Chat history is not the coordination mechanism. GitHub branches, commits, CI and these status files are.

## Current baseline

TradeOps repository:

```text
avysotsky/TradeOps
main: 0027041b037b71af5b0b45534c450e49d61a4007
main CI: 37606275357 — SUCCESS
latest integrated slice: VS-06 — DocFlow Earnings Research Adapter (PR #58)
VS-03 Configurable Research Policy integrated via PR #55
VS-02 Public-Data Runnable Demo integrated via PR #56
VS-01 Research-to-Rebalance Client Demo integrated via PR #54
WS-01 manual Paper smoke harness integrated via PR #53
previous integrated workstreams: WS-04 PR #52, WS-02 PR #51, WS-01 PR #50, WS-03 PR #49
```

DocFlow repository:

```text
avysotsky/DocFlow
main: 2bbb18d2d056e87f0616569e46b011bb0e31668d
latest integrated slice: DF-03 — Generic Text Structured Extraction Boundary (PR #3)
DF-03 merge commit: a2a947767801420f2900790b81501104dda0be5c
post-merge CI: 37607059797 — SUCCESS
DF-02 Generic Text/Transcript Normalization integrated via PR #2
legacy QBO work: HOLD / maintenance unless business priority changes
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
| WS-01 | IBKR Paper Adapter | TradeOps/ws01-ibkr-paper-adapter | WAITING_EXTERNAL | harness integrated; real Paper account required for authenticated validation |
| WS-02 | Earnings Intelligence / research integration | TradeOps/ws02-earnings-intelligence-contract | INTEGRATED | no further contract changes without orchestration |
| WS-03 | Signal / Portfolio Target Engine | TradeOps/ws03-signal-portfolio-engine | INTEGRATED | no further contract changes without orchestration |
| WS-04 | Event-driven Backtester | TradeOps/ws04-backtester-contracts | INTEGRATED | bounded v1 complete; no generic expansion without orchestration |
| VS-01 | Research-to-Rebalance Client Demo | TradeOps/vs01-research-to-rebalance-demo | INTEGRATED | extend only through orchestrated client-facing slices |
| VS-02 | Public-Data Runnable Demo | TradeOps/vs02-public-data-runnable-demo | INTEGRATED | public IBM SEC/market-data runnable demo integrated via PR #56 |
| VS-03 | Configurable Research Policy | TradeOps/vs03-configurable-research-policy | INTEGRATED | strict versioned JSON policy layer integrated via PR #55 |
| VS-04 | Configurable Public-Data Client Demo | TradeOps/vs04-configurable-public-demo | INTEGRATED | policy-driven public-data client demo integrated via PR #57 |
| VS-05 | Earnings Transcript Intake Boundary | TradeOps/vs05-earnings-transcript-intake | HOLD_ARCHITECTURE | hold completed correctly; DO_NOT_MERGE; preserved as reference prototype for DocFlow boundary |
| VS-06 | DocFlow Earnings Research Adapter | TradeOps/vs06-docflow-earnings-adapter | INTEGRATED | PR #58; synchronized-head CI 37605602130 SUCCESS; post-merge CI 37605867347 SUCCESS |
| DF-01 | DocFlow QBO | avysotsky/DocFlow / v1.1.1.34 | HOLD | no, unless reprioritized |
| DF-02 | DocFlow Generic Text/Transcript Normalization | DocFlow/transcript-normalization-boundary | INTEGRATED | PR #2; generic offline text/transcript normalization; no TradeOps dependency |
| DF-03 | DocFlow Generic Text Structured Extraction Boundary | DocFlow/df03-text-structured-extraction | INTEGRATED | PR #3; exact-head CI 37605915999 SUCCESS; post-merge CI 37607059797 SUCCESS |

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

WS-01 read-only boundary is integrated by PR #50. Real authenticated Paper validation remains a hard gate before enabling order mutations.

WS-02 semantic alignment is resolved and integrated by PR #51. Earnings assessment now maps through explicit EarningsTargetWeightPolicy into ResearchDecision(Action = SetTargetWeight), compatible with integrated WS-03.

WS-03 RebalancePlan / RebalanceOrderIntent v1 semantics are integrated by PR #49 and are now the downstream portfolio-planning boundary.

WS-04 bounded v1 is integrated by PR #52 and reuses the integrated WS-02 event-time semantics and WS-03 planner semantics. Do not expand it into a generic research project unless required by the client-facing vertical slice.

## Integration order

1. DONE — merge orchestration layer.
2. DONE — WS-03 target-weight/rebalance planning contract (PR #49).
3. DONE — WS-01 paper-only IBKR read boundary (PR #50); real Paper smoke remains a gate before mutation capability.
4. DONE — WS-02 semantic alignment and earnings/event-time contract integrated (PR #51).
5. DONE — historical event/provenance and SetTargetWeight boundaries frozen for downstream replay.
6. DONE — WS-04 bounded event-driven backtester integrated (PR #52).
7. DONE — VS-01 client-ready research -> backtest -> rebalance vertical slice integrated (PR #54).
8. DONE — WS-01 opt-in manual IBKR Paper read-path harness integrated (PR #53).
9. WAIT — execute real authenticated IBKR Paper smoke only when a real Paper account becomes available.
10. DONE — VS-02 Public-Data Runnable Demo integrated (PR #56).
11. DONE — VS-03 Configurable Research Policy integrated (PR #55).
12. DONE — VS-04 Configurable Public-Data Client Demo integrated via PR #57.
13. HOLD — VS-05 TradeOps transcript intake prototype: preserve branch, do not merge; reassess boundary against DocFlow document-processing engine.
14. DONE — DF-02 DocFlow Generic Text/Transcript Normalization integrated via PR #2; generic normalized text document + segments/participants + deterministic fingerprint, no TradeOps dependency.
15. DONE — VS-06 DocFlow Earnings Research Adapter integrated via PR #58; bounded serialized DocFlow transcript -> earnings research input adapter, with no generic normalization or fact extraction.
16. DONE — DF-03 DocFlow Generic Text Structured Extraction Boundary integrated via PR #3; `NormalizedTextDocument -> TextStructuredExtractionEngine -> StructuredExtractionResult`, post-merge CI 37607059797 SUCCESS.
17. NEXT — define the separate structured earnings-fact extraction slice and its exact cross-repository boundary. Decide ownership/API before launching a worker; do not add earnings-specific logic to the generic DF-03 base abstraction.
18. After Paper validation, consider RebalanceOrderIntent -> existing risk/order lifecycle -> IBKR Paper mutation bridge.

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
DONE VS-04 Configurable Public-Data Client Demo — PR #57
DONE DF-02 DocFlow Generic Text/Transcript Normalization — PR #2; post-merge CI 37603127313 SUCCESS
DONE VS-06 DocFlow Earnings Research Adapter — PR #58; post-merge CI 37605867347 SUCCESS
DONE DF-03 Generic Text Structured Extraction — PR #3; post-merge CI 37607059797 SUCCESS
P0  Structured earnings-fact extraction boundary design — decide DocFlow generic extraction configuration vs TradeOps domain mapping before worker launch
HOLD VS-05 TradeOps prototype — preserve as reference; do not merge
DONE VS-01 Research-to-Rebalance Client Demo — PR #54
WAIT WS-01 real Paper validation — harness integrated via PR #53; external Paper account unavailable
DONE WS-04 Event-driven Backtester — PR #52
DONE WS-02 Earnings Intelligence boundary — PR #51
DONE WS-03 Portfolio Target & Rebalancing Engine — PR #49
DONE WS-01 IBKR Paper read boundary — PR #50
HOLD DocFlow QBO sandbox work
```
