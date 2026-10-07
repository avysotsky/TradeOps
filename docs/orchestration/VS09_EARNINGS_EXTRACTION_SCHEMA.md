# VS-09 — Provider-Compatible Earnings Extraction Schema

## State

INTEGRATED

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs09-earnings-extraction-schema
```

## Baseline

```text
45ecbc841814ea861dc4c09c642c12cc660ed928
```

Baseline CI:

```text
37618585251 — SUCCESS (documentation-only orchestration launch commit)
```

## Purpose

Make the integrated VS-07 earnings extraction contract machine-readable for provider-backed DocFlow extraction without moving earnings-domain ownership into DocFlow.

Target:

```text
TradeOps-owned earnings extraction schema request
+ synthetic prior/current raw transcript inputs
-> external DocFlow DF-06 producer
-> StructuredExtractionResult JSON
-> integrated TradeOps VS-08 consumer
```

VS-09 does not call OpenAI and does not implement extraction.

It owns only the caller-side schema/configuration and synthetic provider-demo inputs.

## Ownership

TradeOps owns:

- earnings field names;
- normalized financial units;
- evidence requirements;
- guidance semantics;
- final authoritative validation in `DocFlowStructuredEarningsFactsAdapter`.

DocFlow owns:

- generic normalization;
- generic schema-driven extraction;
- provider backend;
- generic structured result.

Do not add a DocFlow project/package dependency to TradeOps.

## Machine-readable request artifact

Add one versioned caller-owned request file, recommended:

```text
schemas/research/earnings-transcript-facts-v1.schema-request.json
```

The file must be directly consumable by the integrated DF-04:

```text
SchemaDrivenTextExtractionRequest
```

Shape:

```json
{
  "schema_name": "tradeops_earnings_transcript_facts_v1",
  "schema_version": 1,
  "json_schema": {
    "type": "object"
  }
}
```

Do not add provider/model configuration to this file.

## Structured Outputs compatibility

The schema must be compatible with strict OpenAI Structured Outputs used by integrated DF-05.

Critical constraints:

- every object uses `additionalProperties: false`;
- every property declared by an object is listed in that object's `required`;
- values that are semantically optional for VS-07 are represented as nullable fields, not omitted fields;
- avoid provider-fragile JSON Schema keywords unless necessary;
- do not rely on schema features that DF-05/OpenAI strict mode does not support.

This means, for example, all supported fact slots should be present in provider output, but absent facts should be `null`.

VS-07 remains the authoritative validator after extraction.

## Earnings data shape

The JSON Schema validates the `data` object produced by DF-04/DF-05, not the outer `StructuredExtractionResult`.

Required top-level keys:

```text
schema_version
docflow_document_id
docflow_fingerprint
currency
amount_scale
margin_scale
facts
guidance
```

Constants:

```text
schema_version = 1
amount_scale = "base_units"
margin_scale = "fraction"
```

### Facts

All fact property names are required in the provider output:

```text
revenue
diluted_eps
net_income
gross_margin
operating_margin
```

Each value is either:

- null; or
- an object containing required:
  - `value` number
  - `evidence_segment_ids` array of strings.

Semantics:

```text
revenue          = currency base units
diluted_eps      = currency per share
net_income       = currency base units
gross_margin     = decimal fraction
operating_margin = decimal fraction
```

Do not ask the provider to return percentages as 42 for 42%; it must return 0.42.

### Guidance

All guidance property names are required in provider output:

```text
direction
revenue_low
revenue_high
diluted_eps_low
diluted_eps_high
```

Each may be null.

Direction, when non-null, is an evidence-bearing object whose `value` is one of:

```text
lowered
maintained
raised
```

Numeric guidance items use the same evidence-bearing numeric fact shape.

## Identity/evidence instructions in schema descriptions

Use concise schema descriptions to tell the extraction provider that:

- `docflow_document_id` must be copied exactly from the normalized document;
- `docflow_fingerprint` must be copied exactly;
- evidence IDs must be exact normalized-document segment IDs;
- IDs must not be translated or rewritten;
- unsupported facts should be null;
- numeric scaling follows the required normalized units.

Do not embed transcript-specific IDs in the reusable schema.

## What the schema must NOT attempt to own

Do not try to replace VS-07 validation with JSON Schema.

The following remain authoritative downstream checks in TradeOps and may not be fully expressible/provider-portable in strict JSON Schema:

- document ID equality with the VS-06 input;
- fingerprint equality and canonical SHA semantics;
- currency equality with `Instrument.Currency`;
- evidence membership in the exact current normalized document;
- duplicate evidence detection if not safely supported by the provider subset;
- non-empty evidence guarantees beyond what the provider subset reliably supports;
- guidance low <= high;
- minimum useful extraction across multiple nullable facts;
- event/provenance semantics.

The machine-readable schema is a provider-shaping boundary, not the final domain validator.

## Synthetic provider-demo raw inputs

Add two synthetic DF-02-compatible raw text input files, recommended:

```text
samples/research/provider-transcript-demo/prior-raw.json
samples/research/provider-transcript-demo/current-raw.json
```

Requirements:

- `document_type = "transcript"`;
- generic synthetic issuer/speakers only;
- fake source URLs under `example.test`;
- explicit timezone-aware timestamps;
- timestamps satisfy source <= published <= retrieved;
- text explicitly states that monetary figures are USD for the sample;
- facts are unambiguous and numerically simple;
- include at least revenue, diluted EPS and operating margin in both periods;
- include at least one guidance direction/value in the current period;
- use values that should lead to a deterministic nontrivial VS-08 decision under the existing sample policy;
- no actual company/client data.

Do not precompute DocFlow document IDs, segment IDs, or fingerprints in raw input. DF-02 normalization owns those.

## Demo context artifact

Add a small TradeOps-owned provider-demo context/manifest only if necessary to record:

- instrument;
- fiscal periods;
- earnings event association IDs;
- paths to the raw inputs;
- path to the schema request.

Do not duplicate the existing VS-08 manifest format unless extension is actually needed.

Prefer a separate provider-demo preparation manifest over modifying the integrated VS-08 transport.

No model/API key belongs in this artifact.

## Tests

Do not add a JSON Schema runtime dependency to TradeOps solely for VS-09.

Use existing .NET JSON APIs for structural contract tests.

At minimum verify:

1. schema-request JSON is valid JSON;
2. root request has exactly `schema_name`, `schema_version`, `json_schema`;
3. request schema version == 1;
4. json_schema root type == object;
5. every object schema recursively has `additionalProperties: false`;
6. every object schema recursively requires every declared property;
7. nullable fact/guidance slots remain explicitly present;
8. top-level earnings keys exactly match the integrated VS-07 transport names;
9. fact property names exactly match VS-07;
10. guidance property names exactly match VS-07;
11. constants align with `DocFlowStructuredEarningsFactsAdapter` supported version/scales;
12. guidance direction enum aligns with existing values;
13. no provider/model/API-key field exists in schema request;
14. prior/current raw inputs parse as JSON and use `document_type = transcript`;
15. source timestamps are explicit and correctly ordered;
16. sample text contains no real company/client identity;
17. raw inputs do not contain precomputed document/segment IDs or fingerprints;
18. no frozen shared TradeOps contract changes;
19. full TradeOps regression suite remains green.

## Explicitly out of scope

Do not:

- call OpenAI;
- add OpenAI SDK;
- add JSON Schema library;
- execute DocFlow;
- implement a normalizer;
- implement extraction;
- add a new earnings adapter;
- change VS-07 semantics;
- change VS-08 decision composition;
- add backtest/rebalance/execution;
- add real transcript data.

## Privacy

Use only generic synthetic values.

Before handoff scan all changed files against the orchestration privacy rule.

Do not mention prohibited personal names even in absence statements.

## Completion protocol

Before handoff update this file with:

- State;
- Current HEAD;
- changed files;
- schema/request path;
- provider-demo input paths;
- shared contracts changed or not;
- tests;
- exact CI;
- privacy scan;
- blockers;
- next integration action.

Stop after this bounded slice.

Do not merge independently.


## Completion handoff

### Validated implementation HEAD

```text
372c52191d4ce32d93dda902eb9ed716fb43e9ec
```

This is the exact implementation HEAD validated by GitHub Actions. The documentation-only status commit recording this handoff advances the branch after the validated implementation commit.

### Pull request

```text
PR #61 — VS-09: Provider-compatible earnings extraction schema
```

The worker has not merged the branch.

### Changed files

```text
docs/orchestration/VS09_EARNINGS_EXTRACTION_SCHEMA.md
schemas/research/earnings-transcript-facts-v1.schema-request.json
samples/research/provider-transcript-demo/prior-raw.json
samples/research/provider-transcript-demo/current-raw.json
tests/TradeOps.UnitTests/ProviderCompatibleEarningsExtractionSchemaTests.cs
```

### Schema request

```text
schemas/research/earnings-transcript-facts-v1.schema-request.json
schema_name: tradeops_earnings_transcript_facts_v1
schema_version: 1
```

The schema targets the DF-04 `SchemaDrivenTextExtractionRequest.json_schema` data payload, not the outer `StructuredExtractionResult`.

It keeps every object closed with `additionalProperties: false`, requires every declared property, represents unsupported fact/guidance slots with explicit `null`, preserves the VS-07 field names, and uses the existing adapter constants:

```text
schema_version = 1
amount_scale = base_units
margin_scale = fraction
```

No provider, model, or API-key configuration is stored in the request artifact.

### Provider-demo raw inputs

```text
samples/research/provider-transcript-demo/prior-raw.json
samples/research/provider-transcript-demo/current-raw.json
```

Both inputs are DF-02-compatible raw transcript inputs with synthetic speakers/company identity, `example.test` source URLs, explicit timezone-aware source/published/retrieved timestamps, and no precomputed DocFlow document IDs, fingerprints, segment IDs, or evidence IDs.

The prior/current financial values intentionally preserve the existing VS-08 positive-decision direction while remaining raw text:

```text
revenue: 100,000,000 USD -> 110,000,000 USD
diluted EPS: 2.00 -> 2.20 USD/share
operating margin: 20% -> 22%
current guidance: raised; revenue 115,000,000-120,000,000 USD; diluted EPS 2.25-2.35 USD/share
```

DF-02 remains responsible for generating normalized document identity, fingerprint, and segment IDs.

### Structured Outputs compatibility review

Official OpenAI Structured Outputs constraints were rechecked before implementation on 2026-10-07.

The schema uses a root object, explicit required fields, nullable unions for semantically optional values, and `additionalProperties: false` on every object. It avoids unsupported/provider-fragile composition constraints and leaves authoritative business validation in VS-07.

### Tests

New structural tests cover:

- exact schema-request root shape and schema version;
- exact VS-07 top-level/fact/guidance field names;
- recursive `additionalProperties: false`;
- recursive `required == properties`;
- nullable fact/guidance slots;
- adapter version/scale constants;
- guidance direction enum;
- absence of provider/model/API-key configuration fields in the schema request;
- DF-02-compatible raw JSON structure;
- timezone-aware timestamp ordering;
- `example.test` source URLs;
- explicit USD/revenue/EPS/operating-margin text;
- current raised guidance;
- absence of precomputed DocFlow identity/evidence fields in raw inputs.

Exact GitHub Actions result:

```text
37620533715 — SUCCESS
453 / 453 tests passed
```

Build, unit tests, API/PostgreSQL smoke, webhook/demo checks, deployment validations, and Docker image build all passed.

### Public/shared contracts

No frozen shared TradeOps contract was changed.

No new adapter, provider client, JSON Schema runtime dependency, OpenAI SDK, DocFlow project/package dependency, normalization logic, extraction execution, backtest, rebalance, or execution behavior was added.

### Privacy scan

All changed files were scanned before handoff.

Result:

```text
PASS
synthetic provider-demo identity only
sample URLs use example.test
no secrets/API keys
no prohibited personal/client identity
```

### Blockers

None for the bounded VS-09 slice.

### Integration result

VS-09 was synchronized with the then-current TradeOps `main`, reviewed, and integrated.

```text
synchronized final PR head: 78daaafcf22f59768551a99fa0f6b696582eebe9
synchronized exact-head CI: 37621698229 — SUCCESS
PR: #61
merge commit: 761e32acb90094eae3b3d029dd6b5e69edb529fd
post-merge CI: 37622030289 — SUCCESS
implementation suite before sync: 453 / 453 passed
```

Architecture/privacy/secret review passed. No frozen shared TradeOps contract changed, no provider SDK or DocFlow dependency was added, and the schema/request remains TradeOps-owned while VS-07 remains the authoritative downstream validator.

Next orchestration step: execute the first provider-backed two-repository transcript research demo using integrated DocFlow DF-06 to produce normalized/structured JSON artifacts from the VS-09 synthetic raw transcripts, then feed those artifacts into integrated TradeOps VS-08 to obtain the existing deterministic ResearchDecision.
