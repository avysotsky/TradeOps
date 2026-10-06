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
3f7aa84565fbd77f202821f72c36160c001774f3
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
WS-01 IBKR Paper Adapter — validation/read-boundary integration candidate
WS-02 Earnings Intelligence — next integration candidate after current-head CI
WS-03 Portfolio Target Engine — integrated; stop independent changes
```

WS-04:

```text
design/contracts only until WS-02 integration
after WS-02 is frozen, full implementation may begin using integrated WS-03 semantics
```

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

Read:

```text
docs/orchestration/WS04_BACKTESTER.md
```

Do not start full simulator implementation until WS-02 and WS-03 are unblocked/frozen.

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
