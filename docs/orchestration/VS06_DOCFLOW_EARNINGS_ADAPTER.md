# VS-06 — DocFlow Earnings Research Adapter

## State

READY

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs06-docflow-earnings-adapter
```

## Baseline

```text
5e3e49b4d2c3dcb0e506d91295f16fa275f23458
```

## Purpose

Build the minimal consumer-side boundary between the integrated DocFlow DF-02 normalized text artifact and TradeOps earnings research.

Target flow:

```text
DocFlow NormalizedTextDocument JSON
+ explicit earnings context
-> strict transport validation
-> deterministic earnings-context mapping
-> ResearchSourceProvenance
-> earnings-specific transcript research input
```

This slice stops before semantic fact extraction.

## Architecture decision

The adapter belongs in TradeOps because it consumes TradeOps contracts.

DocFlow remains independent and must not depend on TradeOps.

Do not add a project/package reference from TradeOps to the DocFlow Python worker. The integration boundary is serialized JSON compatible with the DF-02 `NormalizedTextDocument` schema.

Do not copy the held VS-05 implementation. VS-05 remains reference-only on `TradeOps/vs05-earnings-transcript-intake`.

## Frozen TradeOps contracts

Do not modify:

- `InstrumentReference`
- `ResearchDecision`
- `ResearchDecisionAction`
- `ResearchDecisionValidationResult`

Also do not change existing production semantics of:

- `EarningsEvent`
- `EarningsSnapshot`
- `ResearchSourceProvenance`
- `MarketDataBar`
- `PortfolioSnapshot`
- `RebalancePlan`
- `RebalanceOrderIntent`
- `BacktestPerformanceMetrics`

## Input boundary

Consume the serialized DF-02 normalized artifact, whose relevant JSON shape is:

```text
document_id
title
document_type
source:
  provider
  source_uri
  source_document_id
  source_timestamp
  published_at
  retrieved_at
participants[]
segments[]
fingerprint
```

Use strict `System.Text.Json` transport DTOs or equivalent bounded adapter DTOs.

Transport DTOs are integration-only. They must not become a second generic document domain model.

Do not reimplement DocFlow normalization, canonical SHA-256 generation, participant normalization, or segment normalization.

The adapter may validate structural invariants needed to consume the artifact safely, but must not duplicate the entire DF-02 canonical hashing engine.

## Earnings context

The caller must explicitly supply the TradeOps-specific context that cannot be inferred from generic DocFlow output:

- `InstrumentReference`
- fiscal period
- earnings-event association identifier
- optional issuer identifier

Do not infer symbol, currency, fiscal period, earnings event, or issuer from transcript text.

## Stronger earnings-source requirements

DF-02 allows some source fields to be absent. The TradeOps adapter must fail closed when data required by TradeOps provenance is unavailable.

For this first slice require:

- `document_type` represents a transcript;
- absolute `source_uri`;
- non-empty provider;
- explicit `source_timestamp`;
- explicit `published_at`;
- explicit `retrieved_at`;
- `source_timestamp <= published_at <= retrieved_at`;
- non-empty document ID and 64-char lowercase SHA-256 fingerprint;
- at least one segment;
- positive unique ordered segment sequences;
- participant references resolve when present.

Do not manufacture missing timestamps or source URIs.

Do not use `DateTimeOffset.UtcNow`.

## Provenance mapping

Map to the existing `ResearchSourceProvenance` without changing that contract.

Expected semantics:

```text
Provider          <- DocFlow source.provider
SourceUri         <- DocFlow source.source_uri
SourceTimestamp   <- DocFlow source.source_timestamp
RetrievedAt       <- DocFlow source.retrieved_at
ExtractionMethod  <- stable adapter identifier, e.g. docflow-normalized-text-v1
SourceDocumentId  <- DocFlow source.source_document_id when present
IssuerId          <- explicit earnings context, when supplied
```

Preserve DocFlow `document_id` and `fingerprint` separately on the earnings-specific input for auditability.

`published_at` remains explicit source availability evidence and must be preserved on the earnings-specific input. It must not be replaced with source timestamp or retrieval time.

## Output boundary

Create one minimal earnings-specific input contract suitable for a later fact-extraction slice.

Recommended semantics:

- existing `InstrumentReference`;
- fiscal period;
- earnings-event association identifier;
- title;
- published-at;
- existing `ResearchSourceProvenance`;
- DocFlow document ID;
- DocFlow fingerprint;
- ordered transcript segments needed by downstream earnings extraction;
- participant metadata only to the extent required to preserve speaker/segment association.

Keep the contract narrow. Do not add generic metadata bags.

Naming is implementation-owned, but the contract must clearly be an earnings transcript research input, not a generic document model.

## Explicitly out of scope

Do not implement in VS-06:

- LLM calls;
- NLP or sentiment;
- revenue/EPS/guidance extraction;
- `EarningsSnapshot`;
- `EarningsEvent` creation;
- `ResearchDecision`;
- backtest integration;
- portfolio/rebalance integration;
- provider API acquisition;
- HTTP scraping;
- audio/speech-to-text;
- persistence;
- DocFlow code changes;
- generic transcript normalization.

The next slice after VS-06 may perform structured earnings-fact extraction from the accepted earnings transcript research input.

## Tests

At minimum cover:

1. valid normalized DocFlow transcript + explicit earnings context -> deterministic earnings research input;
2. existing `InstrumentReference` is reused unchanged;
3. existing `ResearchSourceProvenance` is used unchanged;
4. DocFlow document ID and fingerprint are preserved;
5. segment order and participant association are preserved;
6. missing/invalid source URI fails closed;
7. missing source/published/retrieved timestamps fail closed;
8. invalid timestamp ordering fails closed;
9. non-transcript document type fails closed;
10. missing fiscal period or earnings-event association fails closed;
11. unknown JSON members fail closed where practical;
12. no clock/network access;
13. no `ResearchDecision`, `EarningsEvent`, or `EarningsSnapshot` creation;
14. full existing TradeOps regression suite remains green.

Use only synthetic fixtures.

## Privacy

Do not use personal names of clients or prospective clients anywhere in code, docs, tests, fixtures, commits, PRs, or issues.

Use generic values such as:

- client
- prospective client
- sample
- Sample Company

Before handoff, search all changed files for prohibited personal names defined by the orchestration privacy rule.

## Completion protocol

Before handoff:

1. update this file with exact HEAD;
2. list changed files;
3. state whether any public/shared contract changed;
4. record test count/results;
5. record exact CI run and conclusion;
6. list blockers;
7. stop after one bounded implementation slice.

Do not merge independently.
