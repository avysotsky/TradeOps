# VS-08 — Runnable Transcript-to-Research Integration

## State

READY

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs08-runnable-transcript-research
```

## Baseline

```text
b7a7cfb1330d4c7a122dc3b003cd1b833f40f9e9
```

Baseline CI:

```text
37612484682 — SUCCESS
```

## Purpose

Create the first runnable TradeOps composition that consumes serialized DocFlow transcript artifacts and reaches the existing deterministic research-decision boundary.

Target:

```text
prior normalized transcript JSON
+ prior StructuredExtractionResult JSON
+ prior explicit earnings context

current normalized transcript JSON
+ current StructuredExtractionResult JSON
+ current explicit earnings context

+ existing earnings research policy JSON

-> existing VS-06 normalized transcript adapter
-> existing VS-07 structured earnings-facts adapter
-> existing EarningsTranscriptEventFactory
-> prior/current EarningsEvent
-> existing DeterministicEarningsDecisionRule
-> existing ResearchDecision
-> auditable runnable JSON output
```

VS-08 is the consumer-side runnable cross-repository boundary.

It consumes files/artifacts produced by DocFlow; it does not embed, invoke, copy, or reference the DocFlow Python package.

## Architecture rules

GitHub is source of truth.

Do not create an alternative:

- normalized transcript model;
- earnings fact model;
- EarningsEvent model;
- policy model;
- decision engine;
- ResearchDecision contract.

Reuse integrated production contracts only.

The serialized boundary remains JSON.

No TradeOps project/package reference to DocFlow is allowed.

## Existing components to reuse unchanged

Use directly:

- `DocFlowEarningsResearchAdapter`
- `EarningsTranscriptResearchInput`
- `EarningsTranscriptResearchContext`
- `DocFlowStructuredEarningsFactsAdapter`
- `EarningsTranscriptFactSet`
- `EarningsTranscriptEventFactory`
- `EarningsTranscriptFactSetMapper`
- `EarningsResearchPolicyConfiguration`
- `DeterministicEarningsDecisionRule`
- `EarningsDecisionRuleSettings`
- `EarningsTargetWeightPolicy`
- `EarningsAssessmentResult`
- `ResearchDecision`
- `InstrumentReference`

Do not modify frozen shared contracts.

## Bounded application composition

Add one narrow composition service, recommended shape:

```text
TranscriptResearchDecisionDemoService
```

Recommended request semantics:

```text
prior normalized DocFlow JSON
prior structured extraction JSON
prior EarningsTranscriptResearchContext

current normalized DocFlow JSON
current structured extraction JSON
current EarningsTranscriptResearchContext

validated EarningsResearchPolicyDefinition
```

Recommended result semantics:

```text
prior EarningsTranscriptResearchInput
prior EarningsTranscriptFactSet
prior EarningsEvent

current EarningsTranscriptResearchInput
current EarningsTranscriptFactSet
current EarningsEvent

EarningsAssessmentResult
ResearchDecision
policy fingerprint / strategy id for audit
```

The exact type names may vary if there is a clearer bounded design.

Do not expose DocFlow transport DTOs publicly.

## Composition order

For both prior and current artifact bundles:

1. `DocFlowEarningsResearchAdapter.Adapt(normalizedJson, context)`
2. `DocFlowStructuredEarningsFactsAdapter.Adapt(structuredJson, input)`
3. `EarningsTranscriptEventFactory.Create(input, factSet)`

Then:

4. validate same instrument identity for prior/current;
5. require prior event to precede current event;
6. map the already validated policy with:
   - `EarningsResearchPolicyConfiguration.ToDecisionRuleSettings`
   - `EarningsResearchPolicyConfiguration.ToTargetWeightPolicy`
7. compute `EarningsAssessmentResult` using existing `DeterministicEarningsDecisionRule.Assess`;
8. compute `ResearchDecision` using existing `DeterministicEarningsDecisionRule.Evaluate`.

No alternate decision calculation.

## Deterministic GeneratedAt

Do not call the clock.

For the current event use the same observed-availability semantics already used by the existing demo pipeline:

```text
GeneratedAt = max(
  currentEvent.PublishedAt,
  currentEvent.Provenance.RetrievedAt
)
```

Normalize both to UTC before comparison.

This slice is an observed-artifact runnable demo, not a hypothetical historical-publication replay mode.

## Event ordering

Require:

```text
prior.PublishedAt < current.PublishedAt
```

If published timestamps tie, fail closed rather than invent ordering.

Prior/current instruments must match under the existing `InstrumentReference` value semantics.

Fiscal periods and event IDs must remain whatever the explicit contexts supplied.

Do not infer fiscal periods or event associations from transcript text.

## Runnable CLI

Add a small tool:

```text
tools/TradeOps.TranscriptResearchDemo
```

The CLI must be offline and file-driven.

Recommended arguments:

```text
--manifest <path>
--policy <path>
--json <output-path>
```

No network option in VS-08.

### Manifest

Use one strict versioned manifest for paths + explicit earnings context.

Recommended shape:

```json
{
  "schemaVersion": 1,
  "instrument": {
    "symbol": "SAMP",
    "assetClass": "Stock",
    "currency": "USD",
    "exchange": "XNYS"
  },
  "issuerId": "sample-issuer",
  "prior": {
    "normalizedDocument": "prior-normalized.json",
    "structuredExtraction": "prior-structured.json",
    "fiscalPeriod": "2026-Q1",
    "earningsEventAssociationId": "sample-2026-q1"
  },
  "current": {
    "normalizedDocument": "current-normalized.json",
    "structuredExtraction": "current-structured.json",
    "fiscalPeriod": "2026-Q2",
    "earningsEventAssociationId": "sample-2026-q2"
  }
}
```

Exact transport names may vary, but:

- schema version is required and currently exactly 1;
- unknown JSON members fail closed;
- paths must be non-empty;
- symbol/currency/fiscal periods/event IDs must be normalized non-empty values;
- currency must be uppercase;
- asset class must map to the existing enum;
- no arbitrary metadata bag;
- manifest paths resolve relative to the manifest directory, not process current directory;
- no path is silently substituted.

The manifest is tool transport only. It is not a new shared domain contract.

## Policy

Reuse the existing JSON policy loader.

The CLI must call:

```text
EarningsResearchPolicyConfiguration.Load
```

Do not parse policy thresholds/weights independently.

Reject invalid policy before running the composition.

Preserve the policy fingerprint in output.

## Output

Write one deterministic, auditable JSON artifact.

At minimum include:

```text
policy
  schemaVersion
  strategyId
  fingerprint

instrument

prior
  eventId
  fiscalPeriod
  publishedAt
  source provider / source URI / source timestamp / retrievedAt
  DocFlow document ID
  DocFlow fingerprint
  extraction engine

current
  same fields

assessment
researchDecision
  action
  targetWeight
  generatedAt
  strategyId
```

Evidence remains available through the validated fact sets. The output may include compact evidence-by-fact summaries if useful, but do not duplicate the whole source transcript in the result.

Do not print transcript text to console by default.

Do not include secrets.

## Synthetic runnable sample

Add a fully offline synthetic sample set sufficient to run the CLI.

Use only generic names and fake URLs such as `https://example.test/...`.

The sample must contain:

- two valid normalized transcript artifacts compatible with DF-02/VS-06;
- two valid structured extraction artifacts compatible with DF-04/VS-07;
- matching document IDs/fingerprints/segment evidence;
- explicit prior/current contexts;
- an existing-policy-format JSON file.

The structured artifact engine name should look like a genuine generic DocFlow engine identity, e.g.:

```text
schema_driven_text_v1:sample_backend
```

Do not claim that the synthetic artifact came from a real provider.

## Important cross-repository limitation

VS-08 does not execute DF-04 Python code.

It consumes the serialized artifacts DF-04 produces.

That is intentional:

```text
DocFlow process
  -> normalized JSON
  -> structured extraction JSON

JSON artifact boundary

TradeOps VS-08
  -> earnings domain
  -> ResearchDecision
```

Do not spawn Python, shell out to the DocFlow repository, clone another repository, or add a project dependency.

The live provider-backed production path is the separate DF-05 workstream.

## Validation / fail-closed behavior

At minimum fail closed for:

- missing files;
- malformed manifest;
- unknown manifest fields;
- unsupported manifest schema version;
- invalid instrument fields;
- prior/current context omissions;
- invalid policy;
- VS-06 normalized artifact validation errors;
- VS-07 structured artifact validation errors;
- mismatched document IDs/fingerprints;
- unknown evidence segment IDs;
- prior/current instrument mismatch;
- prior event not strictly earlier than current event.

Do not catch and downgrade domain-validation failures into a successful result.

CLI returns non-zero on failure.

## Tests

At minimum cover:

1. valid synthetic prior/current artifacts -> two existing `EarningsEvent` instances;
2. exact existing VS-06/VS-07 adapters are exercised;
3. exact `InstrumentReference` from each context reaches each event;
4. prior/current document identity remains preserved;
5. evidence-backed fact sets remain auditable;
6. valid policy loads through existing configuration layer;
7. policy fingerprint preserved;
8. existing `DeterministicEarningsDecisionRule.Assess` semantics are used;
9. existing `Evaluate` produces the final `ResearchDecision`;
10. deterministic GeneratedAt equals max(PublishedAt, RetrievedAt) for current event;
11. no clock access;
12. invalid normalized artifact rejects;
13. invalid structured artifact rejects;
14. identity/fingerprint mismatch rejects;
15. bad evidence rejects;
16. instrument mismatch rejects;
17. non-increasing event time rejects;
18. invalid policy rejects;
19. manifest unknown fields reject;
20. relative paths resolve against manifest directory;
21. missing file -> non-zero CLI result;
22. successful CLI writes deterministic JSON;
23. console output does not include full transcript text;
24. no network/HTTP client introduced;
25. no DocFlow project/package/process invocation;
26. no alternative ResearchDecision contract;
27. full TradeOps regression suite remains green.

## Explicitly out of scope

Do not implement:

- OpenAI/Anthropic calls;
- provider backend;
- transcript download/scraping;
- DocFlow normalization/extraction logic;
- Python process invocation;
- backtest;
- portfolio/rebalance planning;
- broker execution;
- persistence/database;
- web API;
- UI.

VS-08 stops at an auditable deterministic `ResearchDecision`.

## Privacy

Do not use real client/prospective-client names anywhere in code, docs, samples, fixtures, commits, PRs, or issues.

Scan every changed file against the orchestration privacy rule before handoff.

Do not mention prohibited names even in statements asserting absence.

## Completion protocol

Before handoff update this file with:

- State;
- Current HEAD;
- changed files;
- public/shared contracts changed or not;
- runnable command;
- sample artifacts;
- tests;
- exact CI run + conclusion;
- privacy scan;
- blockers;
- next integration action.

Stop after this bounded slice.

Do not merge independently.
