# Handoff — Development Orchestrator

## Role

This chat is the central development orchestrator.

It does not normally implement worker features itself.

Its responsibilities are:

- inspect both repositories;
- track worker branch HEADs;
- inspect CI;
- enforce shared contracts;
- detect file/architecture conflicts;
- decide parallel work;
- decide merge/integration order;
- maintain `docs/orchestration/ORCHESTRATION.md`;
- reprioritize based on business/client developments.

## Repositories

```text
TradeOps: avysotsky/TradeOps
DocFlow:  avysotsky/DocFlow
```

## TradeOps baseline

```text
main product integration
de3f15f0dbf5968dd1fc34f0fb4b499ba058b924
VS-03 Configurable Research Policy integrated via PR #55
VS-02 Public-Data Runnable Demo integrated via PR #56
VS-01 Research-to-Rebalance Client Demo integrated via PR #54
WS-01 manual Paper smoke harness integrated via PR #53
WS-04 Event-driven Backtester integrated via PR #52
WS-02 Earnings Intelligence boundary integrated via PR #51
WS-01 IBKR Paper read-only adapter integrated via PR #50
WS-03 Portfolio Target & Rebalancing Engine integrated via PR #49
```

Existing frozen shared boundary:

```text
InstrumentReference v1
ResearchDecision v1
ResearchDecisionAction v1
ResearchDecisionValidationResult v1
```

## Active worker branches

```text
TradeOps/ws01-ibkr-paper-adapter
TradeOps/ws02-earnings-intelligence-contract
TradeOps/ws03-signal-portfolio-engine
TradeOps/ws04-backtester-contracts
TradeOps/vs01-research-to-rebalance-demo
TradeOps/vs02-public-data-runnable-demo
TradeOps/vs03-configurable-research-policy
```

## Worker chats

Recommended chat names:

```text
TradeOps Core / Integration
WS-01 — IBKR Paper Adapter
WS-02 — Earnings Intelligence
WS-03 — Portfolio Target Engine
WS-04 — Backtester
DocFlow — Accounting/QBO
```

## Immediate scheduling

Current scheduling:

```text
WS-01 IBKR Paper Adapter — read-only boundary + manual smoke harness integrated; WAITING_FOR_REAL_PAPER_ACCOUNT
WS-02 Earnings Intelligence — integrated; event-time and SetTargetWeight boundary frozen
WS-03 Portfolio Target Engine — integrated; rebalance boundary frozen
WS-04 Backtester — integrated bounded v1; stop generic expansion
```

Integrated client-ready vertical slice:

```text
VS-01
earnings/research facts
-> explicit client rule / target-weight policy
-> ResearchDecision(SetTargetWeight)
-> WS-04 historical backtest
-> WS-03 rebalance
```

Integrated client-ready extensions:

```text
VS-02
public SEC earnings inputs
+ public daily price inputs
-> VS-01 service
-> runnable demo output

VS-03
client policy JSON
-> strict validation
-> deterministic fingerprint
-> existing earnings settings / target-weight policy
```

Next product focus:

```text
public-data demo
+ validated client policy JSON
-> one runnable client workflow
```

Initial public-data choice for the bounded slice:

```text
issuer: IBM
research source: SEC EDGAR / XBRL public APIs
daily-price demo source: Alpha Vantage TIME_SERIES_DAILY demo endpoint
```

Network acquisition must remain tooling-only and opt-in. Ordinary CI must use deterministic local fixtures/snapshots and must not depend on external services.

IBKR Paper validation remains an external dependency and is not on the critical path for the research/backtest/rebalance demo.

Do not broaden WS-04 or WS-02 generically unless the client-facing slice requires it.

DocFlow:

```text
HOLD after QBO sandbox readiness
resume only if business priority changes or a DocFlow client appears
```

## Orchestrator operating procedure

Whenever the user says `continue`, `check status`, or asks what to work on:

1. read `docs/orchestration/ORCHESTRATION.md`;
2. inspect current TradeOps and DocFlow main/active heads;
3. inspect all active WS branch heads;
4. compare each WS branch to current TradeOps main;
5. inspect CI for each non-trivial HEAD;
6. read the WS status files;
7. identify blockers/dependency changes;
8. update priorities;
9. integrate only branches whose contracts and CI are compatible;
10. update orchestration docs after integration.

Do not assume another chat's progress from memory when GitHub can verify it.

## First worker instructions

### WS-01

Read:

```text
docs/orchestration/WS01_IBKR.md
docs/HANDOFF_NextChat_TradeOps_Current.md
```

Then work only on `TradeOps/ws01-ibkr-paper-adapter`.

### WS-02

Read:

```text
docs/orchestration/WS02_EARNINGS.md
src/TradeOps.Application/Models/ResearchDecision.cs
src/TradeOps.Application/Models/InstrumentReference.cs
```

Keep earnings ingestion as a separate bounded context. This TradeOps branch is the integration-contract anchor until a dedicated repository is assigned.

### WS-03

Integrated via PR #49.

```text
worker HEAD: 3e5796bf815f64c9d9bc6adbb4391239fd3ca157
merge commit: 3f7aa84565fbd77f202821f72c36160c001774f3
CI: 37510799848 success
```

Treat RebalancePlan / RebalanceOrderIntent v1 semantics as frozen for downstream WS-04 and future execution-bridge work.

### WS-04

Integrated via PR #52.

```text
worker HEAD: d7c1f172643691024cf25001afdcf52b6624f647
merge commit: eb6cdf9c3e8b111a85e0d44d170718527eaf9d40
CI: 37519740705 success
```

Treat bounded v1 daily single-instrument backtest semantics as integrated. Further work should be driven by the client-ready end-to-end slice, not generic backtester expansion.

## User-facing workflow

The user should mainly interact with this chat for planning:

```text
continue
check status
what should run in parallel
integrate ready branches
reprioritize after client feedback
```

Worker chats receive execution commands:

```text
continue
```

and must finish each bounded slice by updating their WS status file with exact HEAD and CI.


## VS-01

Integrated via PR #54.

```text
synchronized worker HEAD: 3569ffa281bd978bc82874e1ab1370fab09a50e8
merge commit: ef1690203929a5c0c4adafd3fd0f33d55b088170
exact-head CI: 37524381672 success
```

Reuse ResearchToRebalanceDemoService for subsequent client-facing slices. Do not create parallel research, portfolio or backtest contracts.


## VS-02

Integrated via PR #56.

```text
worker HEAD: 278fe0540846d8a474debaf5ae1c11b4f193268a
merge commit: c52746ee451f222e0f4440c5a8e451dbd66a9c28
exact-head CI: 37594048247 success
```

Historical SEC availability/provenance correction is integrated. External public-data smoke remains manual/opt-in and was not simulated.

## VS-03

Integrated via PR #55.

```text
synchronized worker HEAD: fe9ae05fa257ca683fab1ed08555f71e288a7264
merge commit: de3f15f0dbf5968dd1fc34f0fb4b499ba058b924
exact-head CI: 37596078515 success
```

Reuse EarningsResearchPolicyConfiguration for subsequent client-facing policy input. Do not introduce a parallel strategy configuration contract.
