# VS-07 — Structured Earnings Facts Boundary

## State

READY_FOR_INTEGRATION

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs07-structured-earnings-facts
```

## Baseline

```text
c6e50fe3285346cbe1318e3d26270fc00a3b5883
```

Baseline CI:

```text
37607668804 — IN_PROGRESS (documentation-only orchestration launch commit)
```

## Purpose

Define the strict consumer-side contract between a generic DocFlow structured text extraction result and the existing TradeOps earnings domain.

Target flow:

```text
EarningsTranscriptResearchInput
+ serialized DocFlow StructuredExtractionResult
-> strict versioned earnings-facts validation
-> auditable EarningsTranscriptFactSet
-> existing EarningsSnapshot
-> existing EarningsEvent
```

This slice does **not** call an LLM and does **not** implement a DocFlow extraction engine.

Its purpose is to freeze what a future generic DocFlow schema-driven extractor must produce before the result can enter TradeOps research logic.

## Architecture decision

Ownership is split deliberately:

```text
DocFlow
  owns generic normalized text and generic structured extraction machinery

TradeOps
  owns the earnings-facts schema, evidence requirements,
  financial units/normalization semantics, validation,
  EarningsSnapshot mapping and EarningsEvent creation
```

Do not add earnings-specific models, prompts, enums, or schemas to DocFlow.

Do not add a DocFlow package/project dependency to TradeOps.

The cross-repository boundary remains serialized JSON.

## Existing contracts to preserve

Do not modify:

- `InstrumentReference`
- `ResearchDecision`
- `ResearchDecisionAction`
- `ResearchDecisionValidationResult`
- `EarningsEvent`
- `EarningsSnapshot`
- `EarningsGuidanceSnapshot`
- `EarningsGuidanceDirection`
- `ResearchSourceProvenance`
- `EarningsTranscriptResearchInput`
- `EarningsTranscriptResearchContext`
- `EarningsTranscriptParticipant`
- `EarningsTranscriptSegment`

Do not alter existing SEC/XBRL semantics.

## Input 1 — existing transcript research input

Use the integrated VS-06 contract:

```text
EarningsTranscriptResearchInput
```

It is the authoritative source for:

- `InstrumentReference`;
- fiscal period;
- earnings-event association ID;
- `PublishedAt`;
- source provenance;
- DocFlow document ID;
- DocFlow fingerprint;
- normalized participants;
- normalized transcript segments.

Do not infer or overwrite these values from semantic extraction output.

## Input 2 — serialized DocFlow StructuredExtractionResult

Consume the existing generic DF-03 result shape over the JSON boundary:

```json
{
  "engine": "provider-neutral-engine-name",
  "document_type": "transcript",
  "data": {},
  "confidence": 0.0,
  "validation_status": null,
  "validation": null
}
```

The transport DTO exists only at the integration boundary. Do not reproduce DocFlow as a second domain model in TradeOps.

Unknown members must fail closed where practical.

If `validation_status` is explicitly `invalid` or `incomplete`, reject the artifact.

A missing validation status is allowed because TradeOps performs the authoritative earnings-specific validation in this slice.

Top-level extraction confidence may be preserved for audit but must not directly influence `ResearchDecision`, target weights, or execution.

## Earnings facts schema v1

The `data` payload must be strict and versioned.

Required identity/normalization fields:

```json
{
  "schema_version": 1,
  "docflow_document_id": "textdoc:...",
  "docflow_fingerprint": "<64 lowercase hex>",
  "currency": "USD",
  "amount_scale": "base_units",
  "margin_scale": "fraction",
  "facts": {},
  "guidance": {}
}
```

### Identity rules

Require:

- `schema_version == 1`;
- `docflow_document_id` exactly equals `EarningsTranscriptResearchInput.DocFlowDocumentId`;
- `docflow_fingerprint` exactly equals `EarningsTranscriptResearchInput.DocFlowFingerprint`;
- fingerprint remains 64 lowercase hexadecimal characters;
- `currency` is normalized uppercase and exactly equals `Instrument.Currency`;
- `amount_scale == "base_units"`;
- `margin_scale == "fraction"`.

The structured extraction result must never be accepted for a different transcript merely because the earnings symbol/fiscal period happens to match.

### Numeric fact shape

Each supported numeric fact, when present, must use:

```json
{
  "value": 123.45,
  "evidence_segment_ids": [
    "textseg:..."
  ]
}
```

Supported `facts` keys:

- `revenue`
- `diluted_eps`
- `net_income`
- `gross_margin`
- `operating_margin`

Semantics:

```text
revenue          = instrument currency, base units
diluted_eps      = instrument currency per share
net_income       = instrument currency, base units
gross_margin     = decimal fraction, e.g. 0.42 means 42%
operating_margin = decimal fraction, e.g. 0.18 means 18%
```

Do not use hidden million/billion scaling.

For example:

```text
"$14.3 billion revenue" -> 14300000000
"42% gross margin"      -> 0.42
```

Values are allowed to be negative where economically possible. Do not invent arbitrary domain ranges merely to reject unusual but valid financial results.

### Evidence rules

Every present extracted fact must have at least one `evidence_segment_id`.

Every evidence segment ID must:

- be non-empty;
- be unique within that fact;
- exist in `EarningsTranscriptResearchInput.Segments[*].DocFlowSegmentId`.

Do not accept free-form quotes as evidence identity.

Do not accept segment indexes in place of DocFlow segment IDs.

Evidence is audit metadata and must remain available on the validated fact set even though the existing `EarningsSnapshot` contract does not carry it.

## Guidance schema

Optional `guidance` keys:

- `direction`
- `revenue_low`
- `revenue_high`
- `diluted_eps_low`
- `diluted_eps_high`

Numeric guidance items use the same:

```json
{
  "value": 123.45,
  "evidence_segment_ids": ["textseg:..."]
}
```

Direction uses:

```json
{
  "value": "raised",
  "evidence_segment_ids": ["textseg:..."]
}
```

Allowed direction values:

```text
lowered
maintained
raised
```

Do not use `unknown` as an extracted positive fact. If direction is not supported by evidence, omit it.

If both range endpoints are present:

```text
revenue_low <= revenue_high
diluted_eps_low <= diluted_eps_high
```

Revenue guidance uses currency base units.

EPS guidance uses currency per share.

Every present guidance item requires evidence.

## Minimum useful extraction

Reject a payload that contains no supported current facts and no supported guidance facts.

Do not create an empty `EarningsSnapshot` from an extraction artifact that provided no supported evidence-backed earnings information.

## New narrow TradeOps contract

Create the smallest auditable domain artifact needed between transport validation and existing `EarningsSnapshot`.

Recommended semantics:

```text
EarningsTranscriptFactSet
  SchemaVersion
  ExtractionEngine
  DocFlowDocumentId
  DocFlowFingerprint
  Currency
  ExtractionConfidence?   // audit only
  facts + evidence
  guidance + evidence
```

Naming may vary if a clearer name is justified.

Do not add generic metadata bags.

Do not expose the raw transport DTO as the public earnings contract.

## Mapping to existing EarningsSnapshot

Provide deterministic mapping from the validated fact set to the existing:

```text
EarningsSnapshot
EarningsGuidanceSnapshot
EarningsGuidanceDirection
```

Mapping is direct:

```text
revenue          -> EarningsSnapshot.Revenue
diluted_eps      -> EarningsSnapshot.DilutedEps
net_income       -> EarningsSnapshot.NetIncome
gross_margin     -> EarningsSnapshot.GrossMargin
operating_margin -> EarningsSnapshot.OperatingMargin

guidance.direction      -> EarningsGuidanceSnapshot.Direction
guidance.revenue_low    -> RevenueLow
guidance.revenue_high   -> RevenueHigh
guidance.diluted_eps_low  -> DilutedEpsLow
guidance.diluted_eps_high -> DilutedEpsHigh
```

If no guidance field is present, `EarningsSnapshot.Guidance` should remain null.

Do not derive margins from unrelated values in this transcript slice. The structured extraction must provide explicit evidence-backed normalized margin values.

## Mapping to existing EarningsEvent

Add a small deterministic event factory/mapper using:

```text
EventId      <- EarningsTranscriptResearchInput.EarningsEventAssociationId
Instrument   <- exact existing input InstrumentReference
PublishedAt  <- input.PublishedAt
FiscalPeriod <- input.FiscalPeriod
Snapshot     <- validated fact-set mapping
```

Do not invent event timestamps.

Do not call the clock.

### Provenance for the final EarningsEvent

Preserve from VS-06 provenance:

- Provider
- SourceUri
- SourceTimestamp
- RetrievedAt
- SourceDocumentId
- IssuerId

For the final semantic earnings event, set:

```text
ExtractionMethod = "docflow-structured-earnings-v1"
```

This indicates that the earnings event was produced from a DocFlow structured earnings-fact artifact rather than merely normalized text.

The validated fact set separately retains the concrete DocFlow extraction engine name for audit.

Required time invariant remains:

```text
SourceTimestamp <= PublishedAt <= RetrievedAt
```

## Determinism

For identical:

- `EarningsTranscriptResearchInput`;
- serialized structured extraction artifact;

the validated fact set, snapshot and event must be deterministic.

No network.

No clock.

No random IDs.

## Explicitly out of scope

Do not implement in VS-07:

- LLM calls;
- OpenAI/Anthropic/local-model clients;
- prompt templates;
- DocFlow changes;
- a generic schema engine;
- transcript acquisition;
- HTTP scraping;
- audio/speech-to-text;
- sentiment;
- summarization;
- trading recommendation generation;
- `ResearchDecision` generation;
- target-weight selection;
- backtest changes;
- portfolio changes;
- execution changes;
- persistence.

VS-07 ends at a validated `EarningsEvent` plus its auditable fact set.

## Required tests

At minimum cover:

1. valid structured transcript facts -> validated fact set;
2. validated fact set -> existing `EarningsSnapshot`;
3. validated fact set + VS-06 input -> existing `EarningsEvent`;
4. exact existing `InstrumentReference` instance is reused;
5. EventId/fiscal period/PublishedAt come only from VS-06 input;
6. source provenance fields are preserved;
7. event `ExtractionMethod == "docflow-structured-earnings-v1"`;
8. extraction engine name is retained only on the auditable fact set;
9. document ID mismatch -> reject;
10. fingerprint mismatch -> reject;
11. invalid fingerprint -> reject;
12. currency mismatch -> reject;
13. unsupported amount/margin scale -> reject;
14. unknown JSON members -> reject where practical;
15. explicit `invalid` or `incomplete` generic validation status -> reject;
16. empty supported fact payload -> reject;
17. present fact without evidence -> reject;
18. unknown evidence segment -> reject;
19. duplicate evidence segment within one fact -> reject;
20. evidence for all supported numeric fields is preserved;
21. `$14.3 billion`-equivalent normalized value is represented as `14300000000` in the synthetic fixture;
22. `42%`-equivalent normalized margin is represented as `0.42`;
23. valid guidance direction maps to the existing enum;
24. unsupported guidance direction -> reject;
25. invalid guidance low/high ordering -> reject;
26. guidance evidence is preserved;
27. no guidance fields -> `EarningsSnapshot.Guidance == null`;
28. no clock/network access;
29. no `ResearchDecision` creation;
30. existing SEC/XBRL tests remain green;
31. full TradeOps regression suite remains green.

Use only synthetic fixtures.

## Privacy

Do not use personal client or prospective-client names anywhere in:

- code;
- docs;
- tests;
- fixtures;
- commits;
- PRs;
- issues.

Use only generic synthetic names such as:

- sample
- Sample Company
- Speaker A
- Analyst

Before handoff, search all changed files for prohibited personal names defined by the orchestration privacy rule.

## Completion protocol

Before handoff update this file with:

- State
- Current HEAD
- changed files
- public/shared contracts changed or not
- tests
- exact CI run + conclusion
- privacy scan
- blockers
- next integration action

Stop after this bounded slice.

Do not merge independently.

## Planned next slice

After VS-07 is integrated, define DF-04 in DocFlow:

```text
generic schema-driven text extraction request
-> concrete provider-neutral extraction engine implementation
-> StructuredExtractionResult
```

DF-04 must remain domain-neutral. It may consume a caller-supplied schema/configuration, but must not contain earnings-specific models or trading logic.


## Handoff status

### Current HEAD

CI-validated implementation HEAD:

`4e302432fc1f90e6afbaab589b42f8a1e2e85d34`

This handoff documentation update is docs-only and follows the validated implementation commit.

### Changed files

- `src/TradeOps.Application/Models/EarningsTranscriptFactSet.cs`
- `src/TradeOps.Application/Services/DocFlowStructuredEarningsFactsAdapter.cs`
- `src/TradeOps.Application/Services/EarningsTranscriptEventFactory.cs`
- `tests/TradeOps.UnitTests/DocFlowStructuredEarningsFactsAdapterTests.cs`
- `docs/orchestration/VS07_STRUCTURED_EARNINGS_FACTS.md`

### Completed

Implemented one bounded consumer-side slice:

```text
EarningsTranscriptResearchInput
+ serialized DocFlow StructuredExtractionResult
-> strict earnings schema v1 validation
-> auditable EarningsTranscriptFactSet
-> existing EarningsSnapshot
-> existing EarningsEvent
```

Validation is fail-closed for schema version, transcript/document identity, lowercase SHA-256 fingerprint, exact instrument currency, amount/margin scales, generic invalid/incomplete validation status, unsupported JSON members in the earnings transport DTOs, evidence presence/uniqueness/membership, guidance direction and guidance range ordering.

The adapter performs no million/billion or percent conversion. Synthetic fixtures prove that already-normalized values such as `14300000000` revenue and `0.42` gross margin are preserved directly.

The event factory reuses the exact input `InstrumentReference`, takes EventId/FiscalPeriod/PublishedAt only from the VS-06 input, preserves source provenance fields, enforces `SourceTimestamp <= PublishedAt <= RetrievedAt`, and sets semantic event extraction method to `docflow-structured-earnings-v1`.

No network, clock, random IDs, LLM/provider clients, DocFlow dependency, ResearchDecision generation, portfolio/backtest/execution or persistence changes were added.

### Public/shared contracts

Frozen existing contracts were not modified.

One narrow additive TradeOps earnings audit contract was added:

- `EarningsTranscriptFactSet`
- `EarningsTranscriptNumericFact`
- `EarningsTranscriptGuidanceFactSet`
- `EarningsTranscriptGuidanceDirectionFact`

No DocFlow project/package dependency was added and no SEC/XBRL contract or semantics were changed.

### Tests

GitHub Actions full TradeOps suite:

- Test Run Successful
- Total tests: `431`
- Passed: `431`
- Failed: `0`

The suite includes VS-07 positive/negative structured-fact tests plus existing SEC/XBRL and full repository regressions.

### CI

Exact CI-validated implementation HEAD:

`4e302432fc1f90e6afbaab589b42f8a1e2e85d34`

GitHub Actions:

`build` run `37608501151` (run number `710`) — `success`.

Draft integration PR: #59.

### Privacy scan

Changed files were reviewed for personal/prospective-client names. Project-prohibited personal name `Filip` is absent.

Synthetic code/tests use only generic values such as `sample`, `Sample Company`, `Speaker A`, and `Analyst`.

Production code contains no `HttpClient`, clock access (`Now`/`UtcNow`), random/GUID generation, OpenAI/Anthropic clients, or ResearchDecision generation. OpenAI/Anthropic and ResearchDecision mentions remain only in the orchestration out-of-scope text / negative boundary assertions.

### Blockers

None.

### Next integration action

Development Orchestrator should review draft PR #59, confirm changed-file scope and CI, and merge VS-07 into `main` if accepted.

Do not start DF-04 from this worker. DF-04 should be defined only after VS-07 integration and must remain generic/domain-neutral.
