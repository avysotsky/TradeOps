# VS-10 — Provider-Backed Two-Repository Transcript Research Demo

## State

READY

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs10-provider-backed-transcript-demo
```

## Baseline

```text
02db6f9c9dd86ef36aa4c0baadeb9f186cb4c534
```

Baseline CI:

```text
37622453715 — SUCCESS
```

DocFlow integration baseline used by this slice:

```text
avysotsky/DocFlow
main: 88d0bf7d0ff20208c2992fa43bac1f32414e0b8d
DF-06 post-merge CI: 37621390275 — SUCCESS
```

## Purpose

Provide the first one-command operator harness that composes the already integrated provider-backed DocFlow producer and TradeOps transcript-research consumer across the existing serialized JSON boundary.

Target runtime:

```text
TradeOps VS-09 prior/current raw transcript JSON
+ TradeOps VS-09 earnings schema request
+ explicit OpenAI model
+ OPENAI_API_KEY from environment

-> external DocFlow checkout
-> DF-06 text_artifact_main.py (prior)
-> prior-normalized.json
-> prior-structured.json

-> DF-06 text_artifact_main.py (current)
-> current-normalized.json
-> current-structured.json

-> existing TradeOps VS-08 TranscriptResearchDemo
-> transcript-research-result.json
-> existing deterministic ResearchDecision
```

VS-10 is an operator/demo orchestration surface only.

It must not create a library dependency between TradeOps and DocFlow.

## Architecture decision

Add a new standalone tool:

```text
tools/TradeOps.ProviderTranscriptResearchDemo
```

The tool may invoke external processes because this slice is explicitly the operator-level integration boundary.

It must not add:

- a ProjectReference/PackageReference to DocFlow;
- copied DocFlow source;
- Python embedding;
- HTTP/OpenAI SDK code in TradeOps;
- a second transcript parser;
- a second earnings adapter;
- a second research-decision implementation.

The runtime dependency is only:

```text
filesystem JSON artifacts + child-process CLI contracts
```

## Required existing inputs

Reuse the integrated TradeOps-owned files:

```text
schemas/research/earnings-transcript-facts-v1.schema-request.json
samples/research/provider-transcript-demo/prior-raw.json
samples/research/provider-transcript-demo/current-raw.json
samples/research/transcript-research/policy.json
```

Do not duplicate the earnings schema or policy.

Add only one small VS-08-compatible provider-demo consumer manifest if needed, recommended:

```text
samples/research/provider-transcript-demo/consumer-manifest.json
```

It should reference these generated runtime artifact names relative to the copied runtime manifest:

```text
prior-normalized.json
prior-structured.json
current-normalized.json
current-structured.json
```

Use the existing VS-09 synthetic instrument/context:

```text
symbol: SAMP
assetClass: Stock
currency: USD
exchange: XNYS
issuerId: sample-issuer
prior fiscal period: FY2026-Q1
prior event id: sample-earnings-2026-q1
current fiscal period: FY2026-Q2
current event id: sample-earnings-2026-q2
```

No model or API key belongs in the manifest.

## CLI

Recommended operator command:

```text
dotnet run --project tools/TradeOps.ProviderTranscriptResearchDemo -- \
  --docflow-root <path-to-DocFlow-checkout> \
  --model <explicit-openai-model>
```

Optional:

```text
--python <python-executable>
--work-dir <path>
```

Defaults:

```text
--python python
--work-dir <TradeOps-root>/.tradeops/provider-transcript-demo
```

No default model.

Do not accept an API key argument.

The tool should fail clearly if `OPENAI_API_KEY` is missing/blank, without printing its value.

## Repository root behavior

Resolve the TradeOps repository root deterministically from the executable location or an equivalent bounded mechanism.

Validate the supplied DocFlow root before provider execution.

Required DocFlow entry point:

```text
<docflow-root>/src/DocFlow.Extraction.Worker/text_artifact_main.py
```

The DocFlow child process working directory should be:

```text
<docflow-root>/src/DocFlow.Extraction.Worker
```

so existing Python package imports work without copying code.

Do not clone/pull/fetch either repository.

The operator is responsible for supplying a checkout of the integrated DocFlow revision.

## Runtime artifact directory

The default directory:

```text
.tradeops/provider-transcript-demo
```

is already covered by the existing repository ignore rule.

Before a run, create the work directory if needed.

Write only runtime/generated files there:

```text
prior-normalized.json
prior-structured.json
current-normalized.json
current-structured.json
manifest.json
transcript-research-result.json
```

Do not write provider output into committed sample/schema directories.

Do not automatically delete artifacts after a successful run; they are useful for audit.

Do not use a timestamp/random subdirectory in this slice.

Repeated runs with the same explicit work directory overwrite the same six runtime outputs in a controlled way.

## Process execution safety

Create a small injected process-runner abstraction for testability.

Production implementation should use:

```text
ProcessStartInfo
UseShellExecute = false
ArgumentList
```

Do not build one shell command string.

Do not invoke `cmd.exe`, PowerShell, bash, or another shell.

Do not use shell quoting/escaping logic.

Child processes inherit the current environment; do not copy or print secret values.

Do not add the API key to `ArgumentList`.

Capture/forward only bounded child output needed for operator diagnostics.

## DocFlow prior/current calls

For prior:

```text
<python>
  text_artifact_main.py
  --input-raw-json <TradeOps prior-raw.json>
  --schema-request <TradeOps earnings schema request>
  --model <explicit model>
  --output-normalized-json <work-dir>/prior-normalized.json
  --output-structured-json <work-dir>/prior-structured.json
  --document-name <synthetic prior name>
```

For current, same shape with current paths.

Use the exact integrated DF-06 CLI argument names.

Stop immediately if prior fails.

Stop immediately if current fails.

Do not invoke TradeOps VS-08 if either DocFlow producer call returns non-zero.

Treat DF-06 exit code 2 (schema-invalid structured result) as failure for the end-to-end demo, while preserving its written artifact.

## VS-08 consumer call

Copy the committed provider-demo consumer manifest into:

```text
<work-dir>/manifest.json
```

without rewriting its domain content.

Then invoke the existing CLI:

```text
dotnet run --project tools/TradeOps.TranscriptResearchDemo -- \
  --manifest <work-dir>/manifest.json \
  --policy <TradeOps-root>/samples/research/transcript-research/policy.json \
  --json <work-dir>/transcript-research-result.json
```

Use the same process runner.

Do not call or duplicate `TranscriptResearchDecisionDemoService` directly from the new tool.

VS-08 remains the consumer owner.

Stop/fail if VS-08 returns non-zero.

## Success criteria

The harness reports PASS only if:

1. prior DF-06 call returns 0;
2. current DF-06 call returns 0;
3. all four DocFlow artifacts exist;
4. copied manifest exists;
5. VS-08 returns 0;
6. `transcript-research-result.json` exists and is non-empty.

Do not independently recalculate the research decision.

The existing VS-08 JSON output is the authoritative demo result.

## Console output

Success output may include:

- stage names;
- chosen model name;
- DocFlow root;
- work directory;
- child exit statuses;
- final result artifact path.

Do not print:

- API key;
- transcript contents;
- full normalized JSON;
- full structured provider data;
- raw OpenAI response.

Do not echo environment-variable values.

The model name is not secret and may be printed.

## Failure behavior

Return non-zero for:

- missing/invalid DocFlow root;
- missing integrated TradeOps sample/schema/policy/manifest inputs;
- missing `OPENAI_API_KEY`;
- prior DF-06 non-zero;
- current DF-06 non-zero;
- missing expected generated artifact;
- VS-08 non-zero;
- missing/empty final result.

Preserve any artifacts already written before failure.

Do not silently fall back to the old offline VS-08 synthetic structured artifacts.

## Testability

CI must remain network-free and must not require `OPENAI_API_KEY`.

Inject:

- process runner;
- environment reader if useful;
- filesystem/path boundary only if useful.

Tests must use temporary directories and fake process results.

Do not spawn real Python/OpenAI/dotnet child processes in unit tests.

## Required tests

At minimum cover:

1. model is required and has no default;
2. default python executable is `python`;
3. default work directory is under `.tradeops/provider-transcript-demo`;
4. explicit work directory is honored;
5. missing DocFlow root rejects before process execution;
6. missing DF-06 entry point rejects;
7. missing API-key environment variable rejects without exposing a value;
8. API key is never present in child argument lists;
9. exact prior DF-06 CLI argument names/paths are used;
10. exact current DF-06 CLI argument names/paths are used;
11. explicit model is forwarded unchanged to both DF-06 calls;
12. DocFlow working directory is exactly `src/DocFlow.Extraction.Worker`;
13. process runner uses argument-list semantics rather than a shell command contract;
14. prior failure stops current/VS-08 execution;
15. current failure stops VS-08 execution;
16. DF-06 exit code 2 is treated as failure while existing artifact is preserved;
17. provider-demo consumer manifest is copied into work directory unchanged;
18. VS-08 CLI is invoked with the generated manifest and existing policy;
19. result JSON target is inside work directory;
20. VS-08 failure propagates non-zero;
21. success requires all four DocFlow artifacts and final result file;
22. success returns zero;
23. no provider output is written to committed sample/schema directories;
24. no HTTP/OpenAI SDK/DocFlow ProjectReference is introduced in the new tool;
25. no alternate earnings or ResearchDecision calculation is introduced;
26. console/error paths do not echo transcript text/API key;
27. existing VS-08 tests remain green;
28. existing VS-09 tests remain green;
29. full TradeOps regression suite remains green.

## Optional real-provider smoke

A real smoke is desirable but not a CI gate.

After implementation is CI-green, the worker may run one real provider demo only if the execution environment already has:

```text
OPENAI_API_KEY
```

and an explicit model is provided by orchestration/user context.

Do not invent a model choice for the user.

If no usable API key/model is available in the worker environment, record:

```text
real-provider smoke: NOT RUN — external credential/model required
```

Do not ask for or commit the key.

## Explicitly out of scope

Do not add:

- transcript acquisition/scraping;
- real company transcript data;
- new provider SDK;
- provider fallback;
- model registry;
- retries;
- prompt changes;
- schema changes;
- VS-07 semantic changes;
- VS-08 semantic changes;
- backtest/rebalance/execution;
- database/persistence;
- web API;
- service-to-service RPC;
- Docker composition between repositories.

VS-10 stops at the existing `ResearchDecision` JSON artifact.

## Privacy

Use only the integrated synthetic sample data.

Before handoff scan every changed file against the orchestration privacy rule.

Do not mention prohibited personal/client names even in absence statements.

## Completion protocol

Before handoff update this file with:

- State;
- Current HEAD;
- changed files;
- tool/run command;
- process-boundary details;
- tests;
- exact CI run + conclusion;
- privacy/secret scan;
- real-provider smoke status separately;
- blockers;
- next integration action.

Stop after this bounded slice.

Do not merge independently.
