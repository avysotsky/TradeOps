# VS-14 — .NET Child Host & Process Diagnostics Hardening

## State

READY_FOR_INTEGRATION

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs14-dotnet-child-process-hardening
```

## Baseline

```text
ddaf8558f908dd68ff39aeb5eb71ff5cb5bf878b
```

## Purpose

Harden the existing provider transcript harness process boundary without changing research, backtest, rebalance, provider, or broker semantics.

Observed issue:

```text
ProviderTranscriptResearchDemo invokes the TradeOps child using bare "dotnet".
The selected host therefore depends on PATH.
A normal Visual Studio launch previously selected C:\Program Files\dotnet and failed with exit code 0x80008091.
When Visual Studio inherited D:\DotNet first in PATH, dotnet 8.0.421 satisfied TradeOps global.json and the real Groq smoke succeeded.
Child stdout/stderr is currently redirected and discarded, making host failures opaque.
```

VS-14 owns only host resolution, SDK preflight, and bounded process diagnostics.

## Mandatory live verification

Before implementation fetch live GitHub state and verify:

- current TradeOps main;
- current DocFlow main;
- VS-14 branch HEAD;
- compare VS-14 vs current TradeOps main;
- exact CI for current TradeOps main and VS-14 branch HEAD;
- current VS-13 branch/CI and changed-file scope;
- open PR/workstreams;
- privacy/secret boundary.

GitHub is source of truth.

## Parallel ownership

VS-14 exclusively owns:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
docs/orchestration/VS14_DOTNET_CHILD_PROCESS_HARDENING.md
```

Modify `Program.cs` only if needed for argument plumbing.

VS-14 MUST NOT modify:

```text
tools/TradeOps.TranscriptResearchDemo/TranscriptResearchDemo.cs
tests/TradeOps.UnitTests/TranscriptResearchDemoTests.cs
samples/research/provider-transcript-demo/rebalance-input.json
```

Those are VS-13-owned.

Do not modify VS-11 application services, backtester, rebalance planner, frozen shared models, DocFlow, or broker code.

## Required existing behavior to preserve

Preserve VS-12 semantics:

- provider selection remains `openai|groq`;
- default provider remains `openai`;
- explicit model remains mandatory;
- selected credential only:
  - openai -> `OPENAI_API_KEY`
  - groq -> `GROQ_API_KEY`
- no provider fallback;
- no API key in CLI arguments;
- child environment inheritance remains normal;
- `UseShellExecute = false`;
- `ArgumentList`;
- no shell command concatenation;
- current provider/DocFlow stage ordering unchanged;
- existing research-only consumer invocation semantics unchanged in this slice.

VS-14 does not wire the new VS-13 consumer mode. That small wiring is deferred to VS-15 after both slices integrate.

## Dotnet host resolution

The TradeOps child must no longer depend only on bare PATH resolution.

Support explicit operator override:

```text
--dotnet <path-to-dotnet-host>
```

Resolution should be deterministic and cross-platform.

Recommended precedence:

1. explicit `--dotnet`;
2. `DOTNET_HOST_PATH` when valid;
3. `DOTNET_ROOT` + platform dotnet executable;
4. `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` + platform dotnet executable when valid;
5. PATH fallback only with explicit preflight.

A smaller equivalent precedence is acceptable if well justified and tested.

Never hard-code:

```text
D:\DotNet
C:\Program Files\dotnet
```

into repository behavior.

## SDK/global.json preflight

TradeOps root contains:

```json
{
  "sdk": {
    "version": "8.0.400",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Before invoking the TradeOps consumer, validate the selected host sufficiently to prevent the previously observed wrong-SDK ambiguity.

At minimum obtain bounded version information from the selected host and reject clearly incompatible or unusable selection before the consumer stage.

Do not build a general-purpose .NET SDK resolver.

The implementation should be narrowly sufficient for the repository contract.

Expected locally valid example:

```text
8.0.421
```

Do not hard-code 8.0.421 as the only accepted patch; obey the repository `latestPatch` intent.

## Process result and diagnostics

The current `SystemChildProcessRunner` discards redirected stdout/stderr.

Replace or extend the tool-local contract so failure diagnostics can carry bounded output, for example a narrow result:

```text
exit code
bounded stdout
bounded stderr
```

Exact type/name is implementation-owned.

Requirements:

- bounded memory/output size;
- deterministic truncation;
- preserve process exit code;
- avoid deadlock when stdout and stderr both produce data;
- success console remains concise;
- on failure surface enough safe diagnostic text to identify host/runtime/tool errors.

Do not turn this into a shared/general process framework.

## Secret and payload safety

Never output or persist:

- `OPENAI_API_KEY` value;
- `GROQ_API_KEY` value;
- environment dump;
- raw provider request/response;
- raw transcript body;
- authorization headers;
- bearer tokens.

Provider name, model name, selected dotnet executable path, resolved dotnet version, stage name, and exit code are safe bounded diagnostics.

Because child stderr could theoretically contain sensitive/provider payloads, do not blindly echo arbitrary child output.

Use a narrow safety strategy. Examples include:

- expose child output only for the TradeOps/dotnet host preflight and TradeOps consumer stage;
- keep DocFlow/provider child payload output suppressed;
- sanitize known selected secret values before displaying bounded text;
- or another equally strict implementation.

Tests must prove credential values are not surfaced.

## CLI compatibility

Existing invocations without `--dotnet` remain accepted.

The new option is additive.

Unknown arguments still fail closed.

Explicit invalid `--dotnet` must fail before provider/DocFlow child work begins where practical.

Do not add API-key CLI options.

## Tests

CI is network-free and secret-free.

At minimum cover:

1. existing argument parsing remains backward-compatible;
2. `--dotnet <path>` is parsed and honored;
3. missing `--dotnet` value fails;
4. explicit host wins over environment hints;
5. deterministic environment-hint precedence;
6. invalid explicit host fails closed;
7. valid host path is used for TradeOps child invocation;
8. bare `dotnet` is not the sole unvalidated production path;
9. repository global.json is considered;
10. compatible latestPatch .NET 8 SDK is accepted;
11. incompatible major/minor SDK is rejected;
12. host/version preflight failure occurs before TradeOps consumer invocation;
13. no hard-coded local machine path;
14. `UseShellExecute = false`;
15. `ArgumentList` retained;
16. no shell command construction;
17. stdout/stderr capture is bounded;
18. truncation is deterministic;
19. failure diagnostics contain stage/exit/host information;
20. selected credential value is absent from diagnostics;
21. provider child payload is not blindly forwarded;
22. raw transcript content is not printed;
23. existing OpenAI credential selection tests remain green;
24. existing Groq credential selection tests remain green;
25. existing provider prior/current invocation tests remain green;
26. existing runtime artifact naming remains unchanged;
27. full TradeOps suite remains green.

## Privacy

Use synthetic test values only.

No real transcripts, accounts, broker data, client data, personal data, or real credentials.

Before handoff scan all changed files for secrets/privacy leakage.

## Explicitly out of scope

Do not add:

- VS-13 research-rebalance mode;
- provider-to-VS-13 wiring;
- new ResearchDecision;
- earnings rules;
- backtester changes;
- rebalance planner changes;
- provider SDK/HTTP integration;
- provider retry/fallback;
- DocFlow changes;
- broker execution;
- IBKR mutation;
- generic process library.

## Completion protocol

Before handoff update this file with:

- State;
- baseline/current HEAD;
- compare vs current main;
- changed files;
- host-resolution precedence;
- global.json/SDK validation behavior;
- process-output safety behavior;
- public/shared contracts changed or not;
- targeted/full tests;
- exact CI run/conclusion;
- privacy/secret scan;
- blockers;
- next integration action.

Stop after this bounded slice.

Do not merge independently.

## Completion handoff

### Validated implementation head

```text
5cac6ab024c24e3bd517ba10fe80e6ebfccc6e65
```

This is the exact code-and-test HEAD validated by GitHub Actions before this status-file bookkeeping update.

At validation time, current TradeOps `main` was:

```text
99013d7f5f7551f3abf1d342c46d3e48dbbfb68f
```

The implementation branch was intentionally not broadly merged or rebased because its one-commit main divergence was orchestration/docs-only and there was no implementation overlap.

### Changed files

The bounded VS-14 slice changes only:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
docs/orchestration/VS14_DOTNET_CHILD_PROCESS_HARDENING.md
```

`Program.cs`, VS-13-owned files, shared/domain contracts, backtester/rebalance code, DocFlow and broker/IBKR code are unchanged.

### Host resolution precedence

The TradeOps consumer host is selected deterministically:

1. explicit `--dotnet <path>`; invalid explicit paths fail closed;
2. valid `DOTNET_HOST_PATH`;
3. valid `DOTNET_ROOT` plus the platform `dotnet` executable;
4. valid `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` plus the platform `dotnet` executable;
5. platform `dotnet` executable through PATH, but only after the same explicit preflight.

No machine-local dotnet installation path is repository behavior.

### global.json / SDK validation

Before any provider/DocFlow child stage, the selected host runs `dotnet --version` from the TradeOps repository root.

The narrow preflight reads repository `global.json` and enforces `sdk.version = 8.0.400`, `sdk.rollForward = latestPatch`, and `sdk.allowPrerelease = false`.

Compatible 8.0.4xx SDK patches at or above 8.0.400 are accepted, including 8.0.421. Different major/minor versions, older requested feature-band versions, a later 8.0.5xx feature band, prerelease SDKs, malformed output, missing hosts and non-zero preflight exits are rejected before provider work.

This is intentionally not a generic .NET SDK resolver.

### Process-output safety

`IChildProcessRunner` now returns a tool-local bounded result containing exit code, bounded stdout and bounded stderr.

`SystemChildProcessRunner` keeps `UseShellExecute = false`, `ArgumentList`, redirected stdout/stderr and no shell concatenation. Both streams are drained concurrently. Capture is capped deterministically at 4096 characters per stream with a fixed truncation marker.

Provider/DocFlow child output is never forwarded to the console. Provider failures expose only stage and exit status.

Dotnet preflight / TradeOps consumer failures never quote child stdout/stderr text. The captured output is inspected only to emit fixed runtime/SDK classification labels (for example `.NET SDK`, `global.json`, `hostfxr`); arbitrary child payload text cannot cross the console boundary. Known selected credential values, API-key assignment forms, Authorization Bearer data and Bearer token forms are also redacted from exception/error text before display.

Success output remains concise and may show provider, model, selected dotnet host, resolved SDK version, stage and exit status.

### Tests

Provider harness test coverage at validated implementation HEAD:

```text
existing VS-12 provider-harness regression cases: 27
new VS-14 host/diagnostics cases: 16
provider-harness cases total: 43
```

The existing 27 cases and new 16 cases were all executed by the full branch-head test suite.

Full TradeOps GitHub Actions unit suite:

```text
506 / 506 tests passed
```

Exact validated implementation CI:

```text
37659035569 — SUCCESS
head: 5cac6ab024c24e3bd517ba10fe80e6ebfccc6e65
```

The workflow also passed build, API/PostgreSQL smoke, signed-webhook demo, customer TradingView demo, deployment validation and Docker image build. A final hardening review additionally replaced line-level child-output filtering with fixed diagnostic labels so transcript/provider text cannot be echoed merely because it contains an SDK/runtime keyword.

A separate local filtered test invocation was not available in this worker environment; the exact-head GitHub Actions full suite is the execution authority and includes both the VS-14 targeted cases and the retained VS-12 regression cases.

### Public/shared contracts

No frozen shared/domain contract changed.

The additions are limited to the provider-demo tool-local CLI/process boundary.

### Privacy / secret scan

Changed implementation/test/status files were scanned for credential-like material.

Result: no real API keys, private keys, JWTs, client/account/broker data, real transcripts, environment dump, or persisted provider request/response.

Machine-local path examples occur only in the workstream specification/prohibition text; production behavior contains no hard-coded local installation path. Authorization/Bearer strings in source/tests are redaction logic and synthetic safety fixtures only.

### Blockers

None in VS-14 scope.

Parallel VS-13 remains isolated under `VS13_SCOPE_OVERRIDE.md`; VS-14 does not wire the VS-13 rebalance consumer.

### Next integration action

Development Orchestrator should review the exact branch diff and CI, then integrate VS-14 without broadening scope. Do not merge this worker branch independently.

After both VS-13 and VS-14 are integrated, use the separately planned VS-15 slice for provider-harness -> VS-13 consumer wiring.
