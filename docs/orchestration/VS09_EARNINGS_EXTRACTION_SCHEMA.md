# VS-09 — Provider-Compatible Earnings Extraction Schema

## State

READY

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs09-earnings-extraction-schema
```

## Baseline

```text
cdb5d66b42e42fd216617e39f22ad2eb459ffe47
```

Baseline CI:

```text
37616983355 — SUCCESS
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
