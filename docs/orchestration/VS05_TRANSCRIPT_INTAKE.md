# VS-05 — Earnings Transcript Intake Boundary

State: READY_FOR_INTEGRATION

Repository / branch:

```text
avysotsky/TradeOps
TradeOps/vs05-earnings-transcript-intake
```

## Baseline

```text
2d1aed28a778dafba4729187c6ac3a87996d6b7a
```

## Exact CI-validated implementation HEAD

```text
e71ac9209463087f6175dd40c16be9e6dd1dcc8a
```

This status file is a documentation-only handoff written after the successful full CI run above. The implementation HEAD is the exact commit whose code, tests and sample fixture were validated.

## Goal

Create a bounded deterministic consumer-side intake boundary for already acquired earnings-call transcripts:

```text
raw transcript input
-> deterministic validation
-> normalized EarningsTranscriptDocument
-> normalized participants / segments
-> deterministic document and segment IDs
-> ResearchSourceProvenance
-> deterministic SHA-256 fingerprint
-> machine-readable JSON
```

The slice ends at the normalized transcript document. It does not perform investment analysis, fact extraction, sentiment, summarization, ResearchDecision generation, portfolio mapping, backtesting, broker integration, scraping, provider acquisition, audio transcription or LLM work.

## Changed files

Implementation HEAD:

```text
src/TradeOps.Application/Models/EarningsTranscriptDocument.cs
src/TradeOps.Application/Services/EarningsTranscriptNormalizer.cs
tests/TradeOps.UnitTests/EarningsTranscriptNormalizerTests.cs
samples/research/earnings-transcript.sample.json
```

Handoff documentation:

```text
docs/orchestration/VS05_TRANSCRIPT_INTAKE.md
```

No VS-04-owned file was modified.

## Public/shared contracts changed

No.

The following frozen/shared contracts were reused without modification:

```text
InstrumentReference
ResearchSourceProvenance
EarningsEvent
ResearchDecision
ResearchDecisionAction
ResearchDecisionValidationResult
MarketDataBar
PortfolioSnapshot
RebalancePlan
RebalanceOrderIntent
BacktestPerformanceMetrics
```

No change was made to:

```text
DeterministicEarningsDecisionRule
EarningsResearchPolicyConfiguration
ResearchToRebalanceDemoService
PortfolioRebalancePlanner
EventDrivenBacktester
SecEarningsEventFactory
```

## Models added

```text
TranscriptParticipantRole
TranscriptParticipant
TranscriptSegment
EarningsTranscriptDocument
RawTranscriptParticipant
RawTranscriptSegment
RawEarningsTranscriptInput
```

Participant roles are intentionally bounded to:

```text
Unknown
Executive
Analyst
Operator
```

Explicit participant IDs are authoritative. Duplicate display names are allowed and do not merge distinct participants.

## Normalization semantics

The normalizer:

- validates required provider, source document, instrument, fiscal period, title, extraction method, participants and segments;
- reuses the existing `InstrumentReference`;
- normalizes symbol and currency to uppercase;
- trims scalar metadata and participant display names;
- normalizes CRLF / CR line endings to LF;
- trims surrounding per-line transcript whitespace while preserving meaningful text and line structure;
- orders participants deterministically by explicit participant ID;
- orders segments deterministically by explicit positive sequence;
- rejects duplicate or non-positive segment sequences;
- rejects unknown participant references;
- rejects empty transcripts and segments without meaningful text;
- does not infer identities or merge participants by display name;
- uses no random GUIDs, clock reads, network, NLP or LLM.

## Time / provenance semantics

The raw boundary requires explicit:

```text
SourceTimestamp
PublishedAt
RetrievedAt
```

The invariant is enforced fail closed:

```text
SourceTimestamp <= PublishedAt <= RetrievedAt
```

All accepted timestamps are normalized to UTC.

`PublishedAt` must be supplied explicitly by the caller from evidence-backed availability. The normalizer does not derive publication availability from an earnings event time and does not call `DateTime.UtcNow`.

The normalized document reuses `ResearchSourceProvenance` with transcript semantics:

```text
Provider          = transcript source/provider
SourceUri         = source transcript document URI
SourceTimestamp   = provider/source timestamp
RetrievedAt       = explicit acquisition/retrieval timestamp
ExtractionMethod  = explicit intake/extraction method identifier
SourceDocumentId  = provider/source transcript document identifier
IssuerId          = optional provider issuer identifier
```

## Deterministic identity semantics

`DocumentId` is deterministic and is derived from canonical:

```text
provider
source document id
issuer id
instrument identity fields
fiscal period
```

It uses the prefix `transcript:` followed by a lowercase SHA-256 digest.

Each `SegmentId` is deterministic and is derived from:

```text
DocumentId
sequence
explicit participant id
SHA-256(normalized segment text)
```

It uses the prefix `segment:` followed by a lowercase SHA-256 digest.

The same logical input therefore produces the same document and segment IDs. A meaningful segment text change changes that segment identity.

## Fingerprint semantics

The document fingerprint is a 64-character lowercase SHA-256 hex value over a length-prefixed canonical representation that includes:

```text
document identity
all InstrumentReference identity fields
fiscal period
title
PublishedAt
provider/source URI/source timestamps/retrieval timestamp
extraction method/source document id/issuer id
canonical ordered participant metadata
canonical ordered segments, segment IDs and normalized text
```

The fingerprint does not depend on JSON property order. CRLF vs LF and semantically equivalent surrounding/trailing transcript whitespace normalize to the same fingerprint.

Meaningful transcript text changes or changes in the sequence/content association change the fingerprint.

## JSON serialization

`EarningsTranscriptJson` uses `System.Text.Json` only.

It provides:

```text
DeserializeRaw
SerializeNormalized
DeserializeNormalized
```

Normalized JSON deserialization validates the complete normalized document fail closed, including deterministic DocumentId, SegmentId values, canonical ordering and the document fingerprint. Unknown JSON members are rejected.

Normalized document -> JSON -> normalized document preserves meaningful fields and fingerprint semantics.

## Sample fixture

```text
samples/research/earnings-transcript.sample.json
```

The fixture is fully synthetic:

```text
symbol: SAMP
participants: Operator / CEO / CFO / Analyst
segments: 5 short synthetic segments
network dependency: none
copyrighted transcript text: none
```

It is a format/normalization example only and is not a financial recommendation.

## Tests

VS-05 adds 17 deterministic tests covering:

- valid raw transcript -> normalized document;
- reuse of existing `InstrumentReference`;
- reuse of existing `ResearchSourceProvenance`;
- valid time ordering;
- `PublishedAt < SourceTimestamp` rejection;
- `RetrievedAt < PublishedAt` rejection;
- empty transcript rejection;
- empty meaningful segment rejection;
- duplicate sequence rejection;
- deterministic segment ordering;
- identical logical input -> identical DocumentId;
- identical logical input -> identical SegmentIds;
- identical logical input -> identical fingerprint;
- CRLF vs LF fingerprint equivalence;
- surrounding/trailing whitespace fingerprint equivalence;
- meaningful text change -> different segment identity/fingerprint;
- segment sequence/content-order change -> different fingerprint;
- normalized JSON round trip;
- tampered normalized fingerprint -> fail closed;
- duplicate display names with different explicit participant IDs remain distinct;
- synthetic sample fixture normalizes successfully.

Full solution CI result:

```text
Total tests: 385
Passed: 385
Failed: 0
```

## Full CI run

```text
workflow: build
run number: 678
run id: 37599164325
commit: e71ac9209463087f6175dd40c16be9e6dd1dcc8a
conclusion: SUCCESS
```

Validated stages:

```text
restore
build
385/385 unit tests
API + PostgreSQL smoke test
signed webhook end-to-end demo
customer TradingView demo
client pilot starter-kit validation
Docker Compose validation
TradingView gateway deployment validation
Docker image build
```

## Blockers

None for integration review of this bounded slice.

No provider acquisition, transcript extraction/classification or LLM dependency is required for this slice.

## Next integration action

Development Orchestrator should:

1. compare VS-05 against current `main`;
2. verify the diff remains limited to the transcript intake model/service/tests/sample plus this status file;
3. confirm no frozen/shared contract or VS-04-owned file changed;
4. review the deterministic identity, availability/provenance and fingerprint semantics;
5. integrate VS-05 if review remains green;
6. only after a separate orchestration decision start any transcript extraction/classification/LLM slice.

Do not extend VS-05 into ResearchDecision, policy mapping, portfolio/rebalance, backtest, IBKR, scraping, transcript-provider API, audio transcription or real-time transcript streaming.
