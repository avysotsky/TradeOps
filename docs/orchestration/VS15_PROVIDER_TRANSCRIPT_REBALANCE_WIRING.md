# VS-15 — Provider Transcript → Research → Backtest/Rebalance Wiring

## State

READY_FOR_INTEGRATION

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs15-provider-transcript-rebalance-wiring
```

## Baseline

TradeOps orchestration baseline:

```text
015fce4aa8f0cebaf3cccda2c6f74dd64523fa05
```

Latest code-bearing integrated TradeOps baseline:

```text
1444a6ccb023d2545c0b1bf8ba6d52b71e6b6f48
```

Combined VS-13 + VS-14 post-merge CI:

```text
37660656844 — SUCCESS
```

Current DocFlow integration baseline at slice creation:

```text
be957f139cae0eafff3cd47241a5d7dd6beca855
DF-07 post-merge Python Worker CI: 37640516860 — SUCCESS
```

## Purpose

Connect the already integrated provider-backed transcript harness to the already integrated VS-13 research/backtest/rebalance consumer mode.

Target:

```text
raw prior/current transcript
-> DocFlow provider-backed normalization + schema extraction
-> provider-compatible normalized/structured artifacts
-> TradeOps.TranscriptResearchDemo
   --rebalance-input <deterministic input>
-> existing VS-11 TranscriptResearchToRebalanceDemoService
-> existing backtest + rebalance composition
-> auditable transcript-research-rebalance-result.json
```

This is a wiring slice only.

Do not introduce a new research contract, provider backend, backtester, rebalance planner, process framework, or DocFlow change.

## Existing integrated contracts to reuse unchanged

Provider harness:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
```

Integrated provider harness already provides:

- provider selection `openai|groq`;
- explicit model;
- environment-only credential selection;
- DocFlow prior/current artifact generation;
- explicit/deterministic dotnet host resolution;
- repository `global.json` SDK preflight;
- bounded child stdout/stderr capture;
- safe diagnostics with provider payload suppression.

Integrated VS-13 consumer:

```text
tools/TradeOps.TranscriptResearchDemo/TranscriptResearchDemo.cs
tools/TradeOps.TranscriptResearchDemo/TranscriptResearchRebalanceDemo.cs
```

VS-13 already accepts:

```text
--rebalance-input <path>
```

and reuses:

```text
TranscriptResearchToRebalanceDemoService.Run(...)
```

It must remain the only composition path. VS-15 must not call `EventDrivenBacktester` or `PortfolioRebalancePlanner` directly.

Existing deterministic input:

```text
samples/research/provider-transcript-demo/rebalance-input.json
```

Existing instrument:

```text
SAMP / Stock / USD / XNYS
```

Existing positive research policy target:

```text
targetWeight = 0.40
```

## Required wiring behavior

Add a backward-compatible provider-harness path that invokes the integrated VS-13 consumer mode.

Recommended additive CLI surface:

```text
--rebalance-input <path>
```

Behavior:

1. when `--rebalance-input` is absent, preserve the existing research-only VS-12/VS-14 path unchanged;
2. when `--rebalance-input` is present:
   - resolve/validate the file;
   - keep the existing provider prior/current DocFlow stages unchanged;
   - keep the generated runtime `manifest.json`;
   - invoke `tools/TradeOps.TranscriptResearchDemo` with:
     - `--manifest <runtime-manifest>`
     - `--policy <existing policy>`
     - `--rebalance-input <resolved input>`
     - `--json <workdir>/transcript-research-rebalance-result.json`;
   - require the rebalance result artifact to exist and be non-empty;
   - report a concise deterministic PASS line and result path.

Do not duplicate VS-13 parsing/composition logic inside the provider harness.

## Compatibility

The existing provider research-only command must remain valid.

Without `--rebalance-input`:

```text
result artifact = transcript-research-result.json
consumer semantics = existing research-only mode
```

With `--rebalance-input`:

```text
result artifact = transcript-research-rebalance-result.json
consumer semantics = integrated VS-13 mode
```

Do not rename/remove existing provider/model/docflow/python/work-dir/dotnet arguments.

No `--api-key`.

No provider fallback.

## Ownership

VS-15 may modify only the smallest wiring/test surface:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
docs/orchestration/VS15_PROVIDER_TRANSCRIPT_REBALANCE_WIRING.md
```

Modify `Program.cs` only if strictly required for CLI plumbing; current CLI delegates to `ProviderTranscriptResearchDemoCli.Run`, so this should normally be unnecessary.

Reuse without modifying unless a proven blocker exists:

```text
samples/research/provider-transcript-demo/rebalance-input.json
tools/TradeOps.TranscriptResearchDemo/**
src/TradeOps.Application/Services/TranscriptResearchToRebalanceDemoService.cs
```

VS-15 MUST NOT modify:

- DocFlow;
- VS-13 consumer implementation;
- backtester;
- rebalance planner;
- frozen shared models/contracts;
- earnings decision semantics;
- broker/IBKR code;
- provider SDK/HTTP implementation;
- credential names or storage behavior.

## Process / privacy safety

Preserve all VS-14 guarantees.

Never output/persist:

- `OPENAI_API_KEY` value;
- `GROQ_API_KEY` value;
- authorization/bearer data;
- raw provider request/response;
- raw transcript body;
- environment dump.

Provider/DocFlow child output stays suppressed.

TradeOps child failure diagnostics stay bounded and must not blindly echo arbitrary child output.

The new rebalance input is synthetic deterministic portfolio/market data and may be referenced by path; do not print raw transcript/provider payloads.

## Tests

CI must remain network-free and secret-free.

At minimum add/adjust tests for:

1. existing research-only provider harness behavior remains unchanged when no rebalance input is supplied;
2. `--rebalance-input <path>` parses successfully;
3. missing value for `--rebalance-input` fails closed;
4. nonexistent rebalance input fails before provider/DocFlow work where practical;
5. rebalance mode invokes the same DocFlow prior/current stages as before;
6. rebalance mode invokes `TradeOps.TranscriptResearchDemo` exactly once;
7. TradeOps child args contain the existing manifest/policy plus exact resolved `--rebalance-input`;
8. rebalance mode writes/validates `transcript-research-rebalance-result.json`;
9. research-only mode still writes/validates `transcript-research-result.json`;
10. provider/model credential selection remains unchanged;
11. dotnet host resolution/global.json preflight remains unchanged;
12. provider child stdout/stderr remains suppressed;
13. credential values and raw transcript text are absent from diagnostics;
14. no direct `EventDrivenBacktester` call in VS-15 production wiring;
15. no direct `PortfolioRebalancePlanner` call in VS-15 production wiring;
16. no modifications to VS-13 consumer contracts;
17. full TradeOps suite passes.

## Real Groq end-to-end acceptance

After CI-validating the wiring, run one explicit local/operator smoke only if the environment already has the required credential.

Provider/model:

```text
provider = groq
model = openai/gpt-oss-20b
credential = GROQ_API_KEY from environment only
```

Representative command shape:

```text
dotnet run --project tools/TradeOps.ProviderTranscriptResearchDemo -- \
  --provider groq \
  --model openai/gpt-oss-20b \
  --docflow-root <local-DocFlow-root> \
  --rebalance-input samples/research/provider-transcript-demo/rebalance-input.json
```

Use `--dotnet <path>` only when needed; VS-14 host resolution remains authoritative.

Acceptance:

- dotnet preflight succeeds;
- DocFlow prior exit 0;
- DocFlow current exit 0;
- TradeOps rebalance consumer exit 0;
- final provider harness PASS;
- `transcript-research-rebalance-result.json` exists and is non-empty;
- artifact identifies `SAMP`;
- research decision remains compatible with the established positive policy path (target weight 0.40 for the existing validated provider fixture);
- artifact contains backtest metrics/final portfolio/current rebalance plan;
- no credential/provider payload/raw transcript leakage.

Do not commit runtime output.

If no local credential is available, record:

```text
real Groq rebalance smoke: NOT RUN — local operator credential required
```

This is not a CI blocker; it remains the post-integration operator validation step.

## Completion protocol

Before handoff:

1. fetch live TradeOps `main`;
2. fetch live DocFlow `main`;
3. verify VS-15 branch HEAD has not moved unexpectedly;
4. compare branch vs current TradeOps `main`;
5. verify exact branch-head/PR-head CI;
6. confirm changed files stay within VS-15 ownership;
7. confirm no overlap/regression in VS-13/VS-14 contracts;
8. run privacy/secret scan;
9. update this status file with:
   - State;
   - current HEAD;
   - compare;
   - changed files;
   - CLI behavior;
   - tests;
   - exact CI;
   - privacy/secret scan;
   - real Groq smoke status;
   - blockers;
   - next integration action.

Do not merge independently.

Stop after this bounded slice.


## Completion handoff

### Validated implementation head

The exact code-and-test HEAD validated by GitHub Actions is:

```text
dfcb4c599301dcd7022aa476dcf5acfe7ee94f11
```

At implementation validation time:

```text
TradeOps main: 7e91388ec81d4b9ae2c27f7cc2d321d56f867088
DocFlow main: be957f139cae0eafff3cd47241a5d7dd6beca855
```

TradeOps main advanced by one orchestration-only SEC-01 registration commit after the VS-15 branch was created. There is no VS-15 production/test ownership overlap with that main change, so the worker branch was not broadened by merging unrelated orchestration state.

Compare versus current TradeOps main at validation:

```text
status: diverged
ahead: 3
behind: 1
merge base: 015fce4aa8f0cebaf3cccda2c6f74dd64523fa05
```

### Changed files

VS-15 changes remain inside the assigned slice:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
docs/orchestration/VS15_PROVIDER_TRANSCRIPT_REBALANCE_WIRING.md
```

The following VS-13/application/sample files were verified blob-identical to current main and were not modified:

```text
tools/TradeOps.TranscriptResearchDemo/TranscriptResearchDemo.cs
tools/TradeOps.TranscriptResearchDemo/TranscriptResearchRebalanceDemo.cs
src/TradeOps.Application/Services/TranscriptResearchToRebalanceDemoService.cs
samples/research/provider-transcript-demo/rebalance-input.json
samples/research/provider-transcript-demo/consumer-manifest.json
samples/research/transcript-research/policy.json
```

No frozen shared/domain contract, DocFlow code, provider SDK/HTTP code, credential mapping, fallback behavior, broker code, or IBKR code changed.

### CLI behavior

The provider harness now accepts the additive option:

```text
--rebalance-input <path>
```

Without the option, the existing research-only path is unchanged and continues to invoke the integrated consumer with:

```text
--manifest <runtime-manifest>
--policy <policy>
--json <workdir>/transcript-research-result.json
```

With the option, the input path is resolved relative to the TradeOps repository root when relative, must exist before dotnet preflight/provider child work, and the same prior/current DocFlow stages plus unchanged runtime manifest are reused. The TradeOps child receives:

```text
--manifest <runtime-manifest>
--policy <policy>
--rebalance-input <resolved-path>
--json <workdir>/transcript-research-rebalance-result.json
```

The final result must exist and be non-empty. The provider harness continues to emit only bounded safe stage/status information plus the concise PASS/result path; provider child stdout/stderr remains suppressed.

The production wiring contains no direct `EventDrivenBacktester`, `PortfolioRebalancePlanner`, `TranscriptResearchToRebalanceDemoService`, or duplicate VS-13 composition call. VS-13 remains the sole consumer/composition boundary.

### VS-14 guarantees

VS-14 process hardening is preserved unchanged:

- explicit `--dotnet`;
- `DOTNET_HOST_PATH`;
- `DOTNET_ROOT`;
- `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR`;
- PATH fallback only behind the existing preflight;
- repository `global.json` SDK validation;
- bounded concurrent stdout/stderr drain;
- `UseShellExecute = false`;
- `ArgumentList`;
- no shell command concatenation;
- provider child output suppression;
- bounded safe diagnostics and credential redaction.

### Tests / CI

Existing provider-harness regression tests plus VS-15 coverage verify:

- research-only compatibility and legacy result filename;
- `--rebalance-input` parsing and missing-value failure;
- missing rebalance file fails before dotnet preflight/provider child execution;
- prior/current DocFlow stages remain on the existing path;
- exactly one TradeOps consumer invocation in rebalance mode;
- exact resolved `--rebalance-input` child argument;
- rebalance result filename and non-empty artifact requirement;
- unchanged provider credential selection;
- unchanged dotnet host/preflight semantics;
- provider output suppression and diagnostic redaction;
- no direct backtester/rebalance planner calls in production wiring;
- unchanged VS-13 consumer/service/sample contracts.

Exact validated implementation CI:

```text
37662668640 — SUCCESS
head: dfcb4c599301dcd7022aa476dcf5acfe7ee94f11
519 / 519 tests passed
```

The same workflow also passed build, API/PostgreSQL smoke, signed-webhook demo, customer TradingView demo, client starter-kit validation, Docker Compose validation, gateway deployment validation, and Docker image build.

No separate network/provider test is part of CI; CI remains secret-free and provider-network-free for this slice.

### Privacy / secret scan

Changed files were scanned for private-key/JWT/live OpenAI-key/live Groq-key patterns and credential-like bearer literals.

Result:

```text
PASS — no real credentials, provider payloads, raw transcript bodies, environment dumps, account/broker data, or private client data introduced.
```

Occurrences of credential variable names and Authorization/Bearer terminology are limited to existing redaction/safety logic and synthetic tests/specification text.

### Real Groq rebalance smoke

```text
real Groq rebalance smoke: NOT RUN — local operator credential required
```

No real provider credential is available to this worker execution environment. This is not a CI blocker.

### Blockers

None in the bounded VS-15 implementation/CI scope.

### Next integration action

Create a draft PR only as the integration/CI surface. The Development Orchestrator should review changed-file ownership, current-main divergence, exact PR-head CI and this handoff, then decide integration. Do not merge from the worker chat.
