# Development Orchestration

## Purpose

This directory is the cross-chat source of truth for parallel development.

Chat history is not the coordination mechanism. GitHub branches, commits, CI and these status files are.

## Current baseline

TradeOps repository:

```text
avysotsky/TradeOps
main: 9006e06bc7265c2e9048fcf9b32daa532498650d
latest production merge: VS-16 — Rebalance Order Intent → Existing RiskEngine Preview (PR #70)
VS-16 post-merge CI: 37760934674 — SUCCESS
VS-16 CodeQL: 37760933995 — SUCCESS
full suite: 537 / 537 tests passed
post-SEC-001 build: 37767816902 — SUCCESS
post-SEC-001 CodeQL: 37767816589 — SUCCESS
SEC-01/SEC-02: SEC-001 + SEC-002 RESOLVED; current live refs SAFE TO KEEP PUBLIC with cache/fork limitations
prior real Groq research-only validation record: bfef73cd98e7db72eb6b13dcdab3bdccd679ab1f; CI 37651614554 — SUCCESS
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

## Approved architecture direction

Canonical architecture roadmap:

```text
docs/orchestration/DUAL_TOPOLOGY_ARCHITECTURE_PLAN.md
```

Approved target: DocFlow and TradeOps must each be independently deployable HTTP services while their application code remains reusable in one in-process modular-monolith composition. HTTP is a host/adapter concern, not the business-logic boundary. TradeOps will consume DocFlow through a TradeOps-owned port with both HTTP and in-process adapters. The existing CLI/child-process harness remains until topology-equivalent replacements are validated.

Planned sequence after VS-17:

```text
ARCH-01 DocFlow application boundary + HTTP host
ARCH-02 TradeOps research/rebalance/risk/dry-run HTTP host
ARCH-03 TradeOps -> DocFlow HTTP adapter
ARCH-04 TradeOps -> DocFlow in-process adapter
ARCH-05 HTTP vs in-process topology equivalence harness
```

OP-03 remains independently WAITING_EXTERNAL and does not block this architecture work.

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
| VS-13 | Provider Transcript → Backtest/Rebalance End-to-End Consumer | TradeOps/vs13-provider-transcript-rebalance-e2e | INTEGRATED | PR #66; exact-head CI 37658577157 SUCCESS; merge f8c56d22c40e800421e3402d1a061257c0ee3171; combined post-merge CI 37660656844 SUCCESS |
| VS-14 | .NET Child Host & Process Diagnostics Hardening | TradeOps/vs14-dotnet-child-process-hardening | INTEGRATED | PR #65; exact-head CI 37659413168 SUCCESS; merge ec2865382bd65bd15ea591a4851d2987645ace0c; combined post-merge CI 37660656844 SUCCESS |
| VS-15 | Provider Transcript → Research → Backtest/Rebalance Wiring | TradeOps/vs15-provider-transcript-rebalance-wiring | INTEGRATED | PR #67; exact-head CI 37663139885 SUCCESS; merge a62e603ec7d590f0f692db6f1f437f454584d0e9; post-merge CI 37669861416 SUCCESS; 519/519 tests |
| SEC-01 | Public Repository Security & Privacy Audit | TradeOps/sec01-public-repo-security-audit | INTEGRATED | 0 BLOCKER; SEC-002 RESOLVED; SEC-001 RESOLVED by controlled metadata rewrite; live refs revalidated; public-safe verdict restored with cache/fork limitations |
| OP-01 | Real Groq Provider → Research → Backtest/Rebalance Validation | operator/local | DONE | PASS on TradeOps main eaa2420c68f4dc554838c6803e3d034f99c2d793; Groq/DocFlow prior+current/TradeOps VS-13 all exit 0; SAMP targetWeight 0.40; rebalance Ready; runtime JSON 7838 bytes |
| SEC-02 | Commit Metadata Remediation Plan | TradeOps/sec02-commit-metadata-remediation-plan | EXECUTED | Option B executed 2026-10-08; two branch-only refs cleaned; 7 retained refs rewritten with force-with-lease; backup + commit-map retained in Actions run 37767052158 |
| VS-16 | Rebalance Order Intent → Existing RiskEngine Preview | TradeOps/vs16-rebalance-risk-preview | INTEGRATED | PR #70; exact-head CI 37760138944 SUCCESS; merge c86ed98032c551d59fcc807cacc220539c6446bc; post-merge CI 37760934674 SUCCESS; CodeQL 37760933995 SUCCESS; 537/537 tests; 0 warnings |
| OP-02 | Real Groq → Rebalance → Risk Preview Validation | operator/local | DONE | PASS on main 084624291ca3a0554bcd6a31e8541fc187490e6f; Groq/DocFlow prior+current/TradeOps VS-16 exit 0; SAMP Buy 30; requiresRiskApproval=true; existing RiskEngine allowed=true; no order mutation |
| OP-03 | Real IBKR Paper Read-Path Validation | operator/local | WAITING_EXTERNAL | no available real IBKR Paper account/session; Java/CPGW alone is insufficient; read-only harness remains ready |
| VS-17 | Risk-Approved Intent → Execution Dry-Run/Audit Boundary | TradeOps/vs17-risk-approved-execution-dry-run | READY | non-mutating: existing risk-approved signal → production client-order identity → auditable dry-run envelope; no persistence, OrderManager, SignalExecutionService or exchange call |
| ARCH-01 | DocFlow Application Boundary → HTTP Host | TBD | PLANNED | next after VS-17; reusable application path exposed through HTTP without TradeOps dependency |
| ARCH-02 | TradeOps Research/Risk Pipeline → HTTP Host | TBD | PLANNED | reuse existing application services; no broker mutation endpoint |
| ARCH-03 | TradeOps → DocFlow HTTP Adapter | TBD | PLANNED | TradeOps-owned port implemented over versioned DocFlow HTTP contract |
| ARCH-04 | TradeOps → DocFlow In-Process Adapter | TBD | PLANNED | same TradeOps-owned port implemented directly against DocFlow.Application |
| ARCH-05 | HTTP ↔ Monolith Topology Equivalence Harness | TBD | PLANNED | prove semantically equivalent results for same input across both deployment topologies |
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
29. DONE — local real Groq smoke completed on integrated mains with environment-only GROQ_API_KEY and model openai/gpt-oss-20b; DocFlow prior/current and TradeOps VS-08 all exited 0; provider transcript research demo passed; record commit bfef73cd98e7db72eb6b13dcdab3bdccd679ab1f; CI 37651614554 SUCCESS.
30. DONE — VS-13 Provider Transcript -> Backtest/Rebalance End-to-End Consumer integrated via PR #66; exact-head CI 37658577157 SUCCESS; merge f8c56d22c40e800421e3402d1a061257c0ee3171.
31. DONE — VS-14 .NET Child Host & Process Diagnostics Hardening integrated via PR #65; exact-head CI 37659413168 SUCCESS; merge ec2865382bd65bd15ea591a4851d2987645ace0c; combined post-merge CI 37660656844 SUCCESS.
32. DONE — VS-15 Provider Transcript -> Research -> Backtest/Rebalance Wiring integrated via PR #67; exact-head CI 37663139885 SUCCESS; merge a62e603ec7d590f0f692db6f1f437f454584d0e9; post-merge CI 37669861416 SUCCESS; 519/519 tests. Real Groq rebalance smoke remains an explicit local/operator validation.
33. DONE — SEC-01 Public Repository Security & Privacy Audit integrated via PR #68. Audit covered 101/101 public branch tips; 0 BLOCKER findings. SEC-002 public PR-body certificate identity was sanitized and verified. SEC-001 personal/custom email metadata remains the sole unresolved MUST FIX finding and requires coordinated history/ref remediation.
34. After Paper validation, consider RebalanceOrderIntent -> existing risk/order lifecycle -> IBKR Paper mutation bridge.

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
DONE REAL GROQ VALIDATION — provider groq; model openai/gpt-oss-20b; DocFlow prior/current + TradeOps VS-08 exit 0; record commit bfef73cd98e7db72eb6b13dcdab3bdccd679ab1f; CI 37651614554 SUCCESS
DONE VS-13 Provider Transcript -> Backtest/Rebalance End-to-End Consumer — PR #66; merge f8c56d22c40e800421e3402d1a061257c0ee3171
DONE VS-14 .NET Child Host & Process Diagnostics Hardening — PR #65; merge ec2865382bd65bd15ea591a4851d2987645ace0c; combined post-merge CI 37660656844 SUCCESS
DONE VS-15 Provider Transcript -> Research -> Backtest/Rebalance Wiring — PR #67; post-merge CI 37669861416 SUCCESS; 519/519 tests
DONE SEC-01 Public Repository Security & Privacy Audit — PR #68; 0 BLOCKER; SEC-002 resolved; SEC-001 remains MUST FIX
DONE OP-01 — real Groq provider -> DocFlow -> research -> backtest/rebalance validated; SAMP targetWeight 0.40; rebalance Ready; runtime artifact remains local
DONE VS-16 — integrated via PR #70; exact-head CI 37760138944 SUCCESS; post-merge CI 37760934674 SUCCESS; CodeQL 37760933995 SUCCESS; 537/537 tests; 0 warnings
DONE OP-02 — real Groq -> DocFlow -> research -> backtest/rebalance -> existing RiskEngine validated; SAMP Buy 30; allowed=true; no broker mutation
NEXT SECURITY GATE — SEC-001 execution may begin only after explicit owner approval of the destructive history/ref rewrite plan
WAIT OP-03 — real IBKR Paper account/session unavailable; no need to install Java/CPGW until this external dependency exists
P0 READY VS-17 — risk-approved intent → deterministic execution dry-run/audit envelope; no broker call, persistence or order mutation
NEXT ARCHITECTURE PHASE — after VS-17 execute ARCH-01 → ARCH-05 from DUAL_TOPOLOGY_ARCHITECTURE_PLAN.md
WAIT MUTATION SLICE — only after OP-03 PASS
DONE SEC-02 — remediation plan integrated via PR #69; Option B selected as recommended plan; NO HISTORY REWRITE EXECUTED
DONE SEC-001 REMEDIATION — Option B executed under explicit owner approval; old affected root removed from live rewritten refs; production/source/test trees unchanged
WAIT WS-01 real IBKR Paper validation — mutation remains blocked
HOLD VS-05 TradeOps prototype — preserve as reference; do not merge
DONE VS-01 Research-to-Rebalance Client Demo — PR #54
READY OP-03 real Paper validation — harness integrated via PR #53; requires local CPGW + manual Paper login
DONE WS-04 Event-driven Backtester — PR #52
DONE WS-02 Earnings Intelligence boundary — PR #51
DONE WS-03 Portfolio Target & Rebalancing Engine — PR #49
DONE WS-01 IBKR Paper read boundary — PR #50
HOLD DocFlow QBO sandbox work
```


SEC-001 POST-REWRITE — Actions run 37767052158 SUCCESS; pre-rewrite main tree = rewritten main tree; sanitized root = 22a28883e117534ac61575db59fb3d8ed99d64d8; backup and verified candidate artifacts retained.
