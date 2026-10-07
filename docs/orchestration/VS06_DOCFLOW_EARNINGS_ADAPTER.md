# VS-06 — DocFlow Earnings Research Adapter

## State

READY_FOR_INTEGRATION

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


## Completion handoff

### Exact CI-validated implementation HEAD

```text
0db2067334825da5309dbbaa86152d43a947fd53
```

This is the exact code/test HEAD validated by the full CI run below. The status-file commit that records this handoff follows that implementation HEAD and is documentation-only, matching the repository's existing worker handoff convention.

### Completed

Implemented one bounded consumer-side adapter slice:

```text
DocFlow DF-02 NormalizedTextDocument JSON
+ explicit TradeOps earnings context
-> strict fail-closed transport validation
-> existing ResearchSourceProvenance
-> EarningsTranscriptResearchInput
```

The adapter:

- consumes serialized DF-02 JSON only;
- has no DocFlow Python/package/project dependency;
- requires exact `document_type = transcript`;
- requires absolute `source_uri`, provider, source/published/retrieved timestamps, fiscal period, earnings-event association and at least one segment;
- requires explicit timezone evidence and normalized UTC timestamps;
- enforces `source_timestamp <= published_at <= retrieved_at`;
- rejects unknown JSON members through strict `System.Text.Json` options;
- preserves DocFlow document ID, fingerprint, segment order, segment IDs and participant/segment associations;
- reuses the existing `InstrumentReference` instance supplied by the caller;
- maps provenance into the existing `ResearchSourceProvenance` contract with extraction method `docflow-normalized-text-v1`;
- performs no clock or network access;
- does not recompute DocFlow canonical hashes;
- does not create `ResearchDecision`, `EarningsEvent` or `EarningsSnapshot`;
- performs no fact extraction, LLM/NLP/sentiment, acquisition, scraping, persistence, backtest or portfolio/rebalance work.

### Changed files

Implementation/test HEAD:

```text
src/TradeOps.Application/Models/EarningsTranscriptResearchInput.cs
src/TradeOps.Application/Services/DocFlowEarningsResearchAdapter.cs
tests/TradeOps.UnitTests/DocFlowEarningsResearchAdapterTests.cs
```

Completion documentation:

```text
docs/orchestration/VS06_DOCFLOW_EARNINGS_ADAPTER.md
```

### Public/shared contracts changed

No frozen or existing production contract was changed.

Unchanged:

```text
InstrumentReference
ResearchDecision
ResearchDecisionAction
ResearchDecisionValidationResult
EarningsEvent
EarningsSnapshot
ResearchSourceProvenance
MarketDataBar
PortfolioSnapshot
RebalancePlan
RebalanceOrderIntent
BacktestPerformanceMetrics
```

VS-06 adds a new narrow earnings-specific input boundary only:

```text
EarningsTranscriptResearchContext
EarningsTranscriptResearchInput
EarningsTranscriptParticipant
EarningsTranscriptSegment
DocFlowEarningsResearchAdapter
```

No generic `NormalizedTextDocument` domain model was added to TradeOps.

### Tests

Added 23 deterministic synthetic test cases covering:

- valid DF-02 normalized transcript + explicit earnings context;
- deterministic mapping;
- unchanged `InstrumentReference` reuse;
- existing `ResearchSourceProvenance` type and required provenance mapping;
- DocFlow document ID/fingerprint preservation;
- segment ordering and participant association;
- missing/relative source URI rejection;
- missing source/published/retrieved timestamp rejection;
- timezone-less timestamp rejection;
- non-UTC normalized timestamp rejection;
- invalid timestamp ordering rejection;
- non-transcript rejection;
- missing fiscal period rejection;
- missing earnings-event association rejection;
- unknown root/nested JSON member rejection;
- invalid fingerprint rejection;
- empty segment rejection;
- duplicate/unordered sequence rejection;
- unresolved participant rejection;
- duplicate participant ID rejection;
- absence of forbidden downstream contracts from the adapter output boundary.

Full TradeOps CI regression:

```text
Total tests: 399
Passed: 399
Failed: 0
```

### Exact CI run

```text
workflow: build
run number: 692
run id: 37604699448
event: push
validated HEAD: 0db2067334825da5309dbbaa86152d43a947fd53
conclusion: SUCCESS
```

Validated stages include restore, build, full unit tests, API + PostgreSQL smoke, signed webhook demo, TradingView demo, client pilot starter-kit validation, Docker Compose validation, deployment validation and Docker image build.

### Privacy search

Changed implementation/test files were checked before handoff for prohibited client/prospective-client personal names. No matches were found. Synthetic values use only generic `sample`, `Sample Company` and equivalent non-personal fixtures.

### Blockers

None.

### Next integration action

Development Orchestrator should:

1. compare `TradeOps/vs06-docflow-earnings-adapter` with current `main`;
2. confirm the diff is limited to the narrow earnings adapter, its tests and this handoff document;
3. verify no frozen/shared contract or existing earnings provenance semantic changed;
4. verify CI run `37604699448` remains the exact implementation validation gate, or rerun exact-head CI if synchronization changes code;
5. integrate VS-06 if review remains green;
6. only after integration start the separate structured earnings fact-extraction slice.

Do not merge VS-06 into `main` from this worker.
