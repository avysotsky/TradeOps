# VS-14 — .NET Child Host & Process Diagnostics Hardening

## State

READY

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
