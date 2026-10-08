# Development Orchestration

## Purpose

This directory is the cross-chat source of truth for parallel development.

Chat history is not the coordination mechanism. GitHub branches, commits, CI and these status files are.

## Current baseline

TradeOps repository:

```text
avysotsky/TradeOps
main: 54be1190764703b8dd76a47356daecd9bfbe8d49 (ARCH-03 PR #73 merged and integration recorded)
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
main: 602346f90aed9ca9f940ac38ad1847249e3c81df (DocFlow .NET normalization PR #9 merged)
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
| VS-17 | Risk-Approved Intent → Execution Dry-Run/Audit Boundary | TradeOps/vs17-risk-approved-execution-dry-run | INTEGRATED (post-merge CI unverified) | PR #71 merged as f78aed7d641eccd0ab9d50edfdb8cd591a8333d0; exact-head build 37772150069 SUCCESS; worker reports 543/543 tests and 0 warnings; no broker mutation or persistence |
| ARCH-01 | DocFlow Application Boundary → HTTP Host | DocFlow/arch01-application-http-host | INTEGRATED (post-merge CI unverified) | PR #8 merged d6de1b5168c156b107cb3c3d71ef29983401ad40; exact-head Python Worker CI 37786661428 SUCCESS; in-memory extraction + HTTP host parity |
| ARCH-02 | TradeOps Research/Risk Pipeline → HTTP Host | TradeOps/arch02-research-risk-http-host | INTEGRATED (post-merge CI unverified) | PR #72 squash-merged c0d589c91d49d4cffda1deb99dc9ba0ea4e4ca3d; exact-head CI 37798316067 SUCCESS; query-only HTTP, hosted tests, shared deterministic identity |
| ARCH-03 | TradeOps → DocFlow HTTP Adapter | TradeOps/arch03-docflow-http-adapter | INTEGRATED (post-merge CI unverified) | PR #73 squash merged 4ead2320733fea459b51fce64de17a07bf6f4246; exact-head build 37806608859 SUCCESS; port, bounded HTTP adapter and offline tests |
| ARCH-04 | TradeOps → DocFlow In-Process Adapter | TradeOps/arch04-docflow-inprocess-adapter | FEASIBILITY_PROVEN / ADAPTER_NOT_IMPLEMENTED | PR #74 merged 52dbfe375f63857e733d5c6617f987960f4338a3; exact-head probe 37807983142 SUCCESS, full build 37807982919 SUCCESS; actual DocFlow extraction binding pending |
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
DONE MERGE VS-17 — PR #71 merged as f78aed7d641eccd0ab9d50edfdb8cd591a8333d0; post-merge CI not yet verifiable through available workflow lookup; no broker call, persistence or order mutation
DONE MERGE ARCH-01 — DocFlow PR #8 merged d6de1b5168c156b107cb3c3d71ef29983401ad40; exact-head CI 37786661428 SUCCESS; post-merge push CI unverified
DONE MERGE ARCH-02 — PR #72 squash merge c0d589c91d49d4cffda1deb99dc9ba0ea4e4ca3d; exact-head build 37798316067 SUCCESS; post-merge CI unverified
READY ARCH-03 — TradeOps/arch03-docflow-http-adapter, spec commit 53418956daa9394950cab9990dac2cfdf96950d3; ARCH-04/05 downstream
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

## VS-17 integration / ARCH-01 initiation — 2026-10-08

VS-17 PR #71 merged to TradeOps main as `f78aed7d641eccd0ab9d50edfdb8cd591a8333d0` after successful final PR-head build `37772150069`. Worker reported 543/543 tests, 0 warnings and successful CodeQL. Post-merge push-triggered CI for this SHA has **not** been independently verified; the available commit workflow lookup only surfaces PR-triggered runs. Do not report post-merge CI success without exact run evidence.

ARCH-01 branch `DocFlow/arch01-application-http-host` started from DocFlow main `be957f139cae0eafff3cd47241a5d7dd6beca855`, with worker assignment/spec commit `c2d4b67dec33c7e781efec55e46f4b60c8fcaa80` at `docs/ARCH01_APPLICATION_HTTP_HOST.md`. This is a specification-only branch at initiation. HTTP host implementation has not begun. ARCH-02 remains on hold until ARCH-01 establishes a reusable application boundary. OP-03 remains WAITING_EXTERNAL; broker mutation remains gated.

## ARCH-01 integration / ARCH-02 initiation — 2026-10-08

DocFlow ARCH-01 merged through PR #8 as `d6de1b5168c156b107cb3c3d71ef29983401ad40` after exact-HEAD Python Worker CI run `37786661428` SUCCESS (install, compile, pytest). No post-merge push workflow was surfaced by the available connector; do not claim such a result without evidence. ARCH-01 implements an in-memory generic extraction entrypoint and thin versioned FastAPI HTTP host while retaining CLI and existing providers. TradeOps contracts are unchanged.

ARCH-02 worker branch `TradeOps/arch02-research-risk-http-host` was created from main `c13e229835bd17e49430afe7d773c2e618e6e5d4` and registered via specification commit `0caf62322c0a7c83ac94750fdc15a90f444c16a6` at `docs/orchestration/ARCH02_TRADEOPS_HTTP_HOST.md`. No ARCH-02 production files have been changed. ARCH-03 remains downstream of ARCH-02 integration. OP-03 remains WAITING_EXTERNAL and broker mutation blocked.

## ARCH-02 merge / ARCH-03 kickoff — 2026-10-08

TradeOps PR #72 integrated by squash merge `c0d589c91d49d4cffda1deb99dc9ba0ea4e4ca3d` after final branch-head `c1dd06bb4aa21912db126681a7d421d97a76daaf` passed exact-head build `37798316067` SUCCESS (unit tests, API/Postgres smoke, Docker/demo checks); same-head push build `37798306782` and dynamic PR check `37798308432` also SUCCESS. Corrected actual HTTP JSON-binding defect with transport-only snapshot DTO and corrected dictionary-value parity assertion. PR #72 was reviewed for non-mutating semantics, auth gate, no broker/persistence. Post-merge CI is not independently verified. Detail: `ARCH02_TRADEOPS_HTTP_HOST.md`.

ARCH-03 now assigned as specification-only branch `TradeOps/arch03-docflow-http-adapter`, commit `53418956daa9394950cab9990dac2cfdf96950d3` at `docs/orchestration/ARCH03_DOCFLOW_HTTP_ADAPTER.md`. Application port and HTTP adapter should be implemented next. Note ARCH-01 DocFlow HTTP host lacks native service-to-service authentication; unauthenticated public deployment is prohibited. ARCH-04 and ARCH-05 remain downstream. OP-03 WAITING_EXTERNAL.

## ARCH-03 integration / ARCH-04 registration — 2026-10-08

TradeOps ARCH-03 PR #73 squash merged as `4ead2320733fea459b51fce64de17a07bf6f4246`; exact reviewed head `3f81d61b9e956ee8fb735476e767e719465c6abf`, GitHub Actions `37806608859` SUCCESS; dynamic check `37806604366` SUCCESS. TradeOps owns `IDocFlowExtractionPort`; Infrastructure provides a versioned, bounded HttpClient adapter and opt-in DI, offline security tests. No new broker mutation, data storage or executable signal boundary. Post-merge CI is unverified. DocFlow HTTP deployment remains private-network/loopback-only until service authentication is added.

ARCH-04 branch `TradeOps/arch04-docflow-inprocess-adapter` registered from current main `54be1190764703b8dd76a47356daecd9bfbe8d49`; spec `docs/orchestration/ARCH04_DOCFLOW_INPROCESS_ADAPTER.md` commit `7eb42d1fb697f48a13d9b2adc15747838db76148`. Critical runtime issue: TradeOps is .NET 8 and DocFlow application is Python 3.11. A true same-process topology requires an explicit embedded Python runtime proof (no subprocess or localhost HTTP) or a documented ADR deferral; cannot mark ARCH-04 implemented by wrapping the HTTP client. ARCH-05 must follow feasibility evidence rather than assert unsupported parity.

## ARCH-04 feasibility outcome — 2026-10-08

PR #74 was squash-merged as `52dbfe375f63857e733d5c6617f987960f4338a3`. Isolated .NET 8/Python 3.11 Python.NET same-PID synchronous and coroutine proof PASS: workflow `37807983142` SUCCESS. TradeOps full build `37807982919` SUCCESS. ADR and reproducible isolated probe committed, no production DI or broker changes. **Status is only feasibility-proven, not ARCH-04 complete:** still need real DocFlow Python application in-process invocation, trusted artifact distribution and equivalence tests. Default remains ARCH-03 HTTP topology. ARCH-05 not started.

## C#-first DocFlow milestone — 2026-10-08

Prefer C#/.NET where equivalent in quality; retain Python where objectively justified. DocFlow PR #9 squash merged `602346f90aed9ca9f940ac38ad1847249e3c81df` from head `70396711aa3f6095e3e40ecaa5f6fda270955509`. Native .NET 8 normalization library and CLI added. Differential 13-case Python-reference parity CI `37809980079` SUCCESS; Python Worker CI `37809980155` SUCCESS; .NET CI `37809980154` SUCCESS. Original Python HTTP service/LLM pipeline remains unchanged, with no .NET production cutover. Plan next .NET extraction application and API, preserve cross-topology contract and existing TradeOps-owned `IDocFlowExtractionPort`; keep Python.NET embedded runtime as fallback feasibility, not architectural default. ARCH-05 pending; broker mutation blocked.
