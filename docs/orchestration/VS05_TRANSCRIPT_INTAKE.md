# VS-05 — Earnings Transcript Intake Boundary

State: HOLD_ARCHITECTURE

Merge status: DO_NOT_MERGE

Architecture reason:
generic transcript/document normalization overlaps with the existing DocFlow document-intelligence responsibility.

Preserve current implementation as reference prototype pending repository-boundary decision.

Repository / branch:

```text
avysotsky/TradeOps
TradeOps/vs05-earnings-transcript-intake
```

## Baseline

```text
2d1aed28a778dafba4729187c6ac3a87996d6b7a
```

## Implementation HEAD

```text
e71ac9209463087f6175dd40c16be9e6dd1dcc8a
```

## Validated CI

```text
workflow: build
run id: 37599164325
conclusion: SUCCESS
tests: 385/385 passed
```

The implementation HEAD above is the exact commit whose code, tests and sample fixture were validated. The current branch is intentionally held as an architecture prototype/reference implementation and must not be merged until Development Orchestrator resolves the repository boundary.

## Goal

The original bounded slice implemented a deterministic consumer-side intake boundary for already acquired earnings-call transcripts:

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

The implementation remains preserved for architectural review. No further VS-05 feature development is authorized while this hold is active.

## Architecture hold

The current prototype spans two conceptual responsibility categories.

### GENERIC DOCUMENT / TRANSCRIPT PROCESSING — candidate for DocFlow

```text
RawEarningsTranscriptInput-like raw document intake
text normalization
line-ending / whitespace normalization
participant/speaker normalization
segment normalization
segment ordering
generic JSON serialization/deserialization
deterministic document fingerprinting
deterministic segment fingerprinting/identity
generic transcript validation
generic normalized transcript representation
```

These concerns overlap with the existing DocFlow document-intelligence responsibility and should not automatically become a second generic document-processing engine inside TradeOps.

### TRADEOPS / EARNINGS-RESEARCH CONSUMER CONCERNS

```text
InstrumentReference mapping
fiscal-period association
earnings-event association
ResearchSourceProvenance mapping at the TradeOps boundary
future mapping from extracted earnings research facts into the existing TradeOps research pipeline
```

`ResearchDecision`, portfolio, backtest and rebalance remain downstream concerns and are not part of transcript normalization.

### Intended target architecture

Preferred direction:

```text
DocFlow
-> generic document/transcript normalization
-> structured extraction result

-> separate earnings-research adapter / boundary
-> earnings-specific structured facts
-> existing TradeOps research contracts
-> ResearchDecision
-> portfolio / backtest / rebalance
```

TradeOps should not independently become a generic document-processing platform.

DocFlow must not depend on TradeOps contracts. In particular, do not build:

```text
DocFlow
-> dependency on TradeOps.Application
```

Preferred dependency direction:

```text
DocFlow generic output
-> adapter / earnings-research boundary
-> TradeOps consumer contracts
```

No repository split, code move, shared package extraction or DocFlow branch is part of this VS-05 hold action. Those changes require a separate orchestrated slice after DocFlow review.

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

## Models added in the prototype

```text
TranscriptParticipantRole
TranscriptParticipant
TranscriptSegment
EarningsTranscriptDocument
RawTranscriptParticipant
RawTranscriptSegment
RawEarningsTranscriptInput
```

These models are retained as prototype/reference material pending the architecture decision. Their current location in TradeOps is not an approval of final repository ownership.

Participant roles are intentionally bounded to:

```text
Unknown
Executive
Analyst
Operator
```

Explicit participant IDs are authoritative. Duplicate display names are allowed and do not merge distinct participants.

## Normalization semantics in the prototype

The current normalizer:

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

These semantics are preserved for review only. Do not extend `EarningsTranscriptNormalizer` while VS-05 is on architecture hold.

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

The prototype maps transcript source metadata into the existing `ResearchSourceProvenance` at the TradeOps consumer boundary:

```text
Provider          = transcript source/provider
SourceUri         = source transcript document URI
SourceTimestamp   = provider/source timestamp
RetrievedAt       = explicit acquisition/retrieval timestamp
ExtractionMethod  = explicit intake/extraction method identifier
SourceDocumentId  = provider/source transcript document identifier
IssuerId          = optional provider issuer identifier
```

Whether generic provenance normalization belongs upstream in DocFlow versus the earnings-research adapter remains part of the repository-boundary decision. The TradeOps-specific mapping into `ResearchSourceProvenance` remains a TradeOps consumer concern.

## Deterministic identity semantics in the prototype

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

These mechanisms are candidates for generic DocFlow responsibility rather than final TradeOps ownership.

## Fingerprint semantics in the prototype

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

Generic document/segment fingerprinting is explicitly part of the DocFlow-candidate responsibility under architecture review.

## JSON serialization in the prototype

`EarningsTranscriptJson` uses `System.Text.Json` only.

It provides:

```text
DeserializeRaw
SerializeNormalized
DeserializeNormalized
```

Normalized JSON deserialization validates the complete normalized document fail closed, including deterministic DocumentId, SegmentId values, canonical ordering and the document fingerprint. Unknown JSON members are rejected.

Generic JSON serialization/deserialization of normalized transcript documents is a DocFlow-candidate responsibility under architecture review.

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

It remains preserved as prototype/reference material only.

## Tests

The preserved VS-05 prototype tests cover:

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

Validated full solution result:

```text
Total tests: 385
Passed: 385
Failed: 0
```

## Full CI run

```text
workflow: build
run id: 37599164325
commit: e71ac9209463087f6175dd40c16be9e6dd1dcc8a
conclusion: SUCCESS
```

Validated stages included restore/build, 385/385 unit tests, API + PostgreSQL smoke, signed webhook E2E, customer TradingView demo, client starter-kit validation, Docker Compose/gateway validation and Docker image build.

## Hold constraints

While State is `HOLD_ARCHITECTURE`:

```text
DO NOT MERGE this branch into main.
DO NOT synchronize/rebase/merge new main into this branch without an explicit Orchestrator command.
DO NOT revert or delete the current prototype.
DO NOT cherry-pick the prototype into another branch.
DO NOT create a DocFlow branch from VS-05.
DO NOT copy this code into DocFlow.
DO NOT create a new shared package.
DO NOT extend EarningsTranscriptNormalizer.
DO NOT start a new transcript feature slice.
```

Explicitly out of scope while held:

```text
LLM
NLP
sentiment
summary
guidance extraction
fact extraction
provider API
HTTP acquisition
scraping
audio
speech-to-text
CLI
database storage
ResearchDecision integration
backtest integration
portfolio integration
```

## Blockers

Architecture ownership is intentionally unresolved.

The blocker is repository-boundary review between the existing DocFlow document-intelligence responsibility and the TradeOps earnings-research consumer boundary.

The current code is therefore a reference prototype, not an approved production boundary for TradeOps.

## Next action

Development Orchestrator should review DocFlow and decide the repository boundary for generic transcript/document processing.

No merge or refactor action is authorized from VS-05 until that decision is made.

After the architecture decision, a separate orchestrated slice may define the generic DocFlow output and the earnings-research adapter into TradeOps consumer contracts.
