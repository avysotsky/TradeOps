# WS-02 — Earnings Intelligence / Research Boundary

State: READY

Coordination branch:

```text
TradeOps/ws02-earnings-intelligence-contract
```

TradeOps base:

```text
main @ cd62df96fdec27e047aac63818e49aa46ba724f2
```

## Goal

Build the research-side MVP needed for the prospective long-horizon stock workflow without embedding document/LLM ingestion inside TradeOps.

Target flow:

```text
company/watchlist
-> earnings/filing event
-> structured facts
-> deterministic research rule/score
-> ResearchDecision v1
-> TradeOps
```

## Bounded-context rule

The production earnings ingestion service should be a separate service/repository when implementation begins.

This TradeOps branch may define/adapt the integration contract and fixtures needed to consume the service, but it must not grow into a transcript crawler/LLM application.

The orchestrator should create/assign the separate research repository when repository tooling/ownership is available.

## First implementation slice

1. define `EarningsEvent` / `EarningsSnapshot` external contract;
2. define publication timestamp semantics;
3. define provenance/source identity;
4. implement one deterministic source path, preferably SEC filings/data for US issuers;
5. normalize a small KPI set;
6. translate a deterministic rule result into existing `ResearchDecision v1`;
7. provide fixtures for AAPL/MSFT/NVDA-style examples;
8. no automated trading from raw LLM text.

## Required initial facts

Prefer a small stable schema:

```text
symbol
eventId
publishedAt
fiscalPeriod
revenue
eps
netIncome
selected margins
guidance direction/fields when available
source URI/provider
source timestamp
extraction provenance
```

Transcript/audio ingestion is a later provider. Do not make it a prerequisite for the first MVP.

## Critical correctness rule

Historical evaluation must use the actual information-availability timestamp.

No future filing values, revised data or post-event prices may leak into a decision generated at `publishedAt`.

## Output boundary

Research produces structured `ResearchDecision`.

It does not place orders directly.

## Status update template

```text
State:
Current HEAD / repository:
Completed:
Shared contracts changed:
Tests:
CI:
Blockers:
Next integration action:
```
