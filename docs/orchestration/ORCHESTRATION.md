# Development Orchestration

## Purpose

This directory is the cross-chat source of truth for parallel development.

Chat history is not the coordination mechanism. GitHub branches, commits, CI and these status files are.

## Current baseline

TradeOps repository:

```text
avysotsky/TradeOps
main: b27ebdde88c8671815f363500abaf01a4622daa1
latest production merge: VS-12 — Provider-Selectable Two-Repository Demo Harness (PR #64)
VS-12 merge commit: c0d222a5399b0a112e072d232351dccb55a548ac
VS-12 post-merge CI: 37640969754 — SUCCESS
runtime-artifact ignore fix: fc6abc8dc73af6088886cc5259f51b8663fc70c7; CI 37651592492 — SUCCESS
real Groq smoke record: b27ebdde88c8671815f363500abaf01a4622daa1; CI 37651614554 — SUCCESS
VS-03 Configurable Research Policy integrated via PR #55
VS-02 Public-Data Runnable Demo integrated via PR #56
VS-01 Research-to-Rebalance Client Demo integrated via PR #54
WS-01 manual Paper smoke harness integrated via PR #53
previous integrated workstreams: WS-04 PR #52, WS-02 PR #51, WS-01 PR #50, WS-03 PR #49
```

DocFlow repository:

```text
avysotsky/DocFlow
main: be957f139cae0eafff3cd47241a5d7dd6beca855
latest integrated slice: DF-07 — Groq Schema-Driven Text Extraction Backend (PR #7)
DF-07 merge commit: 211f890625712a161eadad09e56962ad69a759f5
post-merge CI: 37640516860 — SUCCESS
current main HEAD be957f139cae0eafff3cd47241a5d7dd6beca855 is docs-only and has no separate workflow run
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
| VS-07 | Structured Earnings Facts Boundary | TradeOps/vs07-structured-earnings-facts | INTEGRATED | PR #59; exact-head CI 37609374812 SUCCESS; post-merge CI 37609652431 SUCCESS |
| VS-08 | Runnable Transcript-to-Research Integration | TradeOps/vs08-runnable-transcript-research | INTEGRATED | PR #60; synchronized-head CI 37616104229 SUCCESS; post-merge CI 37616617370 SUCCESS; 446/446 tests |
| VS-09 | Provider-Compatible Earnings Extraction Schema | TradeOps/vs09-earnings-extraction-schema | INTEGRATED | PR #61; synchronized-head CI 37621698229 SUCCESS; post-merge CI 37622030289 SUCCESS; implementation suite 453/453 |
| VS-10 | Provider-Backed Two-Repository Transcript Research Demo | TradeOps/vs10-provider-backed-transcript-demo | INTEGRATED | PR #63; synchronized-head CI 37629377614 SUCCESS; PR-head CI 37629904230 SUCCESS; post-merge CI 37630315790 SUCCESS; real-provider smoke externally gated |
| VS-11 | Transcript Research-to-Backtest/Rebalance Composition | TradeOps/vs11-transcript-research-rebalance-demo | INTEGRATED | PR #62; exact-head CI 37627834233 SUCCESS; post-merge CI 37628959669 SUCCESS; 463/463 tests; fail-closed research equivalence gate |
| VS-12 | Provider-Selectable Two-Repository Demo Harness | TradeOps/vs12-provider-selectable-transcript-demo | INTEGRATED | PR #64; exact-head CI 37640551725 SUCCESS; 490/490 tests; post-merge CI 37640969754 SUCCESS; local real Groq run is next |
| VS-13 | Provider Transcript → Backtest/Rebalance End-to-End Consumer | TradeOps/vs13-provider-transcript-rebalance-e2e | READY | consumer-only: existing artifacts -> VS-11 -> backtest/rebalance -> auditable JSON; must not touch provider process infrastructure |
| VS-14 | .NET Child Host & Process Diagnostics Hardening | TradeOps/vs14-dotnet-child-process-hardening | READY | provider-harness infrastructure only: dotnet host resolution/global.json preflight/bounded diagnostics; must not touch VS-13 consumer files |
| DF-01 | DocFlow QBO | avysotsky/DocFlow / v1.1.1.34 | HOLD | no, unless reprioritized |
| DF-02 | DocFlow Generic Text/Transcript Normalization | DocFlow/transcript-normalization-boundary | INTEGRATED | PR #2; generic offline text/transcript normalization; no TradeOps dependency |
| DF-03 | DocFlow Generic Text Structured Extraction Boundary | DocFlow/df03-text-structured-extraction | INTEGRATED | PR #3; exact-head CI 37605915999 SUCCESS; post-merge CI 37607059797 SUCCESS |
| DF-04 | Generic Schema-Driven Text Extraction | DocFlow/df04-schema-driven-text-extraction | INTEGRATED | PR #4; exact-head CI 37611510368 SUCCESS; post-merge CI 37612364691 SUCCESS |
| DF-05 | OpenAI Schema-Driven Text Extraction Backend | DocFlow/df05-openai-schema-backend | INTEGRATED | PR #5; exact-head CI 37614234502 SUCCESS; post-merge CI 37615744590 SUCCESS; optional real-provider smoke not run |
| DF-06 | OpenAI Text Artifact CLI | DocFlow/df06-openai-text-artifact-cli | INTEGRATED | PR #6; exact final-head CI 37620734092 SUCCESS; post-merge CI 37621390275 SUCCESS; 86 tests |
| DF-07 | Groq Schema-Driven Text Extraction Backend | DocFlow/df07-groq-schema-backend | INTEGRATED | PR #7; exact-head CI 37639935357 SUCCESS; 116/116 tests; post-merge CI 37640516860 SUCCESS |

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
17. DONE — VS-07 Structured Earnings Facts Boundary integrated via PR #59; strict versioned earnings-facts validation, evidence-segment audit trail, financial unit semantics, deterministic EarningsSnapshot/EarningsEvent mapping; post-merge CI 37609652431 SUCCESS.
18. DONE — DF-04 Generic Schema-Driven Text Extraction integrated via PR #4; provider-neutral caller JSON Schema + injected backend orchestration, deterministic schema validation, existing StructuredExtractionResult/validation contracts; post-merge CI 37612364691 SUCCESS.
19. DONE — VS-08 Runnable Transcript-to-Research Integration integrated via PR #60; offline serialized DocFlow artifacts -> VS-06/VS-07 -> EarningsEvent -> existing deterministic ResearchDecision; synchronized-head CI 37616104229 SUCCESS; post-merge CI 37616617370 SUCCESS.
20. DONE — DF-05 OpenAI Schema-Driven Text Extraction Backend integrated via PR #5; exact-head CI 37614234502 SUCCESS; post-merge CI 37615744590 SUCCESS; provider-specific but domain-neutral.
21. DONE — VS-08 Runnable Transcript-to-Research Integration integrated via PR #60; 446/446 tests; post-merge CI 37616617370 SUCCESS.
22. DONE — DF-06 OpenAI Text Artifact CLI integrated via PR #6; generic raw text + caller schema + explicit model -> normalized + StructuredExtractionResult artifacts; exact final-head CI 37620734092 SUCCESS; post-merge CI 37621390275 SUCCESS.
23. DONE — VS-09 Provider-Compatible Earnings Extraction Schema integrated via PR #61; synchronized-head CI 37621698229 SUCCESS; post-merge CI 37622030289 SUCCESS; implementation suite 453/453.
24. DONE — VS-11 Transcript Research-to-Backtest/Rebalance Composition integrated via PR #62; exact-head CI 37627834233 SUCCESS; post-merge CI 37628959669 SUCCESS; 463/463 tests; transcript and existing VS-01 research semantics are fail-closed equivalent.
25. DONE — VS-10 Provider-Backed Two-Repository Transcript Research Demo integrated via PR #63 after synchronization with VS-11; synchronized-head CI 37629377614 SUCCESS; PR-head CI 37629904230 SUCCESS; post-merge CI 37630315790 SUCCESS.
26. WAITING_EXTERNAL — OpenAI real-provider smoke remains optional and blocked by separate OpenAI API billing; do not require it for progress.
27. DONE — DF-07 Groq Schema-Driven Text Extraction Backend integrated via PR #7; exact-head CI 37639935357 SUCCESS; 116/116 tests; post-merge CI 37640516860 SUCCESS.
28. DONE — VS-12 Provider-Selectable Two-Repository Demo Harness integrated via PR #64; exact-head CI 37640551725 SUCCESS; 490/490 tests; post-merge CI 37640969754 SUCCESS.
29. DONE — local real Groq smoke completed on integrated mains with environment-only GROQ_API_KEY and model openai/gpt-oss-20b; DocFlow prior/current and TradeOps VS-08 all exited 0; provider transcript research demo passed; record commit b27ebdde88c8671815f363500abaf01a4622daa1; CI 37651614554 SUCCESS.
30. PARALLEL — VS-13 Provider Transcript -> Backtest/Rebalance End-to-End Consumer: add the deterministic existing-artifacts -> VS-11 -> backtest/rebalance consumer and auditable JSON. VS-13 does not touch provider process infrastructure.
31. PARALLEL — VS-14 .NET Child Host & Process Diagnostics Hardening: harden the existing provider harness dotnet host resolution, global.json/SDK preflight and bounded safe diagnostics. VS-14 does not touch VS-13 consumer files.
32. NEXT AFTER BOTH — VS-15 small wiring slice: connect integrated provider harness to integrated VS-13 consumer mode, then run one local real Groq provider -> research -> backtest/rebalance smoke.
33. After Paper validation, consider RebalanceOrderIntent -> existing risk/order lifecycle -> IBKR Paper mutation bridge.

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
DONE VS-07 Structured Earnings Facts Boundary — PR #59; post-merge CI 37609652431 SUCCESS
DONE DF-04 Generic Schema-Driven Text Extraction — PR #4; post-merge CI 37612364691 SUCCESS
DONE VS-08 Runnable Transcript-to-Research Integration — PR #60; post-merge CI 37616617370 SUCCESS; 446/446 tests
DONE DF-05 OpenAI Schema Backend — PR #5; post-merge CI 37615744590 SUCCESS
DONE DF-06 OpenAI Text Artifact CLI — PR #6; post-merge CI 37621390275 SUCCESS
DONE VS-09 Provider-Compatible Earnings Extraction Schema — PR #61; post-merge CI 37622030289 SUCCESS
DONE VS-10 Provider-Backed Two-Repository Transcript Research Demo — PR #63; post-merge CI 37630315790 SUCCESS
DONE VS-11 Transcript Research-to-Backtest/Rebalance Composition — PR #62; post-merge CI 37628959669 SUCCESS; 463/463 tests
WAIT OpenAI real-provider smoke — separate API billing unavailable; not a blocker
DONE DF-07 Groq Schema-Driven Text Extraction Backend — PR #7; post-merge CI 37640516860 SUCCESS; 116/116 tests
DONE VS-12 Provider-Selectable Two-Repository Demo Harness — PR #64; post-merge CI 37640969754 SUCCESS; 490/490 tests
DONE REAL GROQ VALIDATION — provider groq; model openai/gpt-oss-20b; DocFlow prior/current + TradeOps VS-08 exit 0; record commit b27ebdde88c8671815f363500abaf01a4622daa1; CI 37651614554 SUCCESS
P0 PARALLEL VS-13 Provider Transcript -> Backtest/Rebalance End-to-End Consumer — consumer/audit JSON only
P0 PARALLEL VS-14 .NET Child Host & Process Diagnostics Hardening — provider process boundary only
NEXT VS-15 after both integrate — wire provider harness -> VS-13 consumer and run local real Groq end-to-end smoke
HOLD VS-05 TradeOps prototype — preserve as reference; do not merge
DONE VS-01 Research-to-Rebalance Client Demo — PR #54
WAIT WS-01 real Paper validation — harness integrated via PR #53; external Paper account unavailable
DONE WS-04 Event-driven Backtester — PR #52
DONE WS-02 Earnings Intelligence boundary — PR #51
DONE WS-03 Portfolio Target & Rebalancing Engine — PR #49
DONE WS-01 IBKR Paper read boundary — PR #50
HOLD DocFlow QBO sandbox work
```
