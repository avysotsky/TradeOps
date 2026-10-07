# VS-13 — Provider Transcript -> Backtest/Rebalance End-to-End Harness

## State

READY

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs13-provider-transcript-rebalance-e2e
```

## Baseline

```text
ddaf8558f908dd68ff39aeb5eb71ff5cb5bf878b
```

This branch starts from the orchestrator registration commit for VS-13.

Before implementation, live-verify:

- current TradeOps `main`;
- current DocFlow `main`;
- this branch HEAD;
- compare this branch against current TradeOps `main`;
- exact GitHub Actions state for the branch HEAD and current relevant main HEADs;
- no unexpected new PR/workstream overlap;
- the privacy/secret boundary below.

GitHub is the source of truth.

## Verified dependency baseline at launch

TradeOps before the registration commit:

```text
main: b27ebdde88c8671815f363500abaf01a4622daa1
build CI: 37651614554 — SUCCESS
```

The immediately preceding runtime-artifact ignore fix is:

```text
fc6abc8dc73af6088886cc5259f51b8663fc70c7
build CI: 37651592492 — SUCCESS
```

DocFlow:

```text
main: be957f139cae0eafff3cd47241a5d7dd6beca855
latest code-bearing main: 211f890625712a161eadad09e56962ad69a759f5
Python Worker CI: 37640516860 — SUCCESS
```

The DocFlow current HEAD is a documentation-only integration record and has no separate workflow run.

Integrated dependencies:

- VS-11 — Transcript Research-to-Backtest/Rebalance Composition;
- VS-12 — Provider-Selectable Two-Repository Demo Harness;
- DF-07 — Groq Schema-Driven Text Extraction Backend.

## Purpose

Create one bounded operator harness proving the existing provider-backed transcript pipeline can continue through the already integrated VS-11 composition to existing backtest metrics and current rebalance planning.

Target chain:

```text
provider (Groq or OpenAI)
-> DocFlow raw transcript normalization
-> DocFlow schema-driven structured earnings extraction
-> TradeOps transcript adapter / structured earnings facts
-> existing ResearchDecision
-> existing TranscriptResearchToRebalanceDemoService (VS-11)
-> existing ResearchToRebalanceDemoService
-> existing EventDrivenBacktester
-> existing BacktestPerformanceMetrics
-> existing PortfolioRebalancePlanner
-> existing RebalancePlan / RebalanceOrderIntent
-> auditable JSON result
```

VS-13 is an operator/demo composition slice.

It must not create a second research, backtest, portfolio, rebalance, or broker architecture.

## Mandatory architecture rules

Do not introduce or modify an alternative:

- `ResearchDecision` contract;
- `InstrumentReference` contract;
- earnings facts contract;
- earnings assessment rule;
- target-weight policy;
- `EarningsEvent`;
- backtester;
- backtest metrics model;
- portfolio snapshot model;
- rebalance planner;
- `RebalancePlan`;
- `RebalanceOrderIntent`;
- broker order;
- IBKR mutation path.

Required existing application boundary:

```text
TranscriptResearchToRebalanceDemoService
```

The runnable consumer must call this service rather than reproducing its logic.

Do not call `EventDrivenBacktester` or `PortfolioRebalancePlanner` directly from the new CLI/harness path.

Do not add an OpenAI or Groq SDK, provider HTTP client, or provider DTO to TradeOps.

DocFlow remains the provider integration owner.

## Preferred bounded implementation

Reuse the existing projects:

```text
tools/TradeOps.ProviderTranscriptResearchDemo
tools/TradeOps.TranscriptResearchDemo
tests/TradeOps.UnitTests
```

No new project should be required.

The existing test project already references both tool projects.

Prefer extending the existing transcript CLI with an explicit rebalance mode/input instead of creating another executable project.

A narrow shape is acceptable, for example:

```text
TradeOps.TranscriptResearchDemo
  existing research-only mode (unchanged by default)
  + explicit research-rebalance mode
```

and:

```text
TradeOps.ProviderTranscriptResearchDemo
  existing provider research mode (unchanged by default)
  + explicit provider -> research-rebalance mode
```

Exact option names may vary if they remain explicit, bounded, and backward-compatible.

## Backward compatibility

Existing VS-12 behavior without new VS-13 options must remain unchanged:

```text
default provider = openai
model = explicit / mandatory
existing provider transcript research path remains runnable
existing runtime file names remain valid
```

Do not silently change existing CLI defaults to the new mode.

The end-to-end rebalance path must require explicit operator intent.

## End-to-end consumer input

VS-11 requires deterministic non-provider inputs in addition to transcript artifacts:

- historical daily market bars;
- initial cash;
- current portfolio snapshot;
- current reference price;
- optional existing backtest/current rebalance constraints.

Use one small versioned demo-only JSON input under the existing provider demo sample area, for example:

```text
samples/research/provider-transcript-demo/rebalance-input.json
```

This is a tool/demo DTO only, not a new shared/domain contract.

Requirements:

- synthetic values only;
- deterministic timestamps;
- instrument identity must match the existing manifest instrument;
- daily stock bars only;
- enough bars to satisfy existing WS-04 semantics;
- current portfolio/reference price must satisfy existing WS-03 semantics;
- target-weight result must come only from the existing earnings policy/ResearchDecision;
- do not encode a second target weight in this input.

Prefer values aligned with the existing VS-11 deterministic test scenario unless a clearer reusable fixture already exists.

## End-to-end consumer algorithm

In explicit research-rebalance mode:

1. Load the existing provider runtime manifest and existing policy.
2. Load prior/current normalized and structured artifacts through the same existing manifest semantics.
3. Load/validate the demo-only rebalance input.
4. Construct the existing `TranscriptResearchDecisionDemoRequest`.
5. Construct the existing `TranscriptResearchToRebalanceDemoRequest`.
6. Call exactly:
   ```text
   TranscriptResearchToRebalanceDemoService.Run(...)
   ```
7. Rely on VS-11 to:
   - preserve the transcript-derived prior/current earnings events;
   - map the same policy downstream;
   - enforce the fail-closed transcript-vs-VS-01 research equivalence gate;
   - invoke the existing research-to-rebalance service;
   - reach the existing backtester and rebalance planner.
8. Emit one deterministic audit JSON artifact.
9. Return non-zero on any invalid input, failed child stage, VS-11 equivalence failure, backtest failure, or rebalance validation failure.

Do not recalculate quantities, metrics, assessment, decision, or target weight in the CLI.

## Auditable JSON result

Add a new runtime artifact for the explicit VS-13 mode, for example:

```text
transcript-research-rebalance-result.json
```

Keep it inside:

```text
.tradeops/provider-transcript-demo/
```

The artifact must be deterministic for the same inputs and must not contain credentials.

Prefer a versioned demo-output DTO local to the tool.

It should expose enough evidence to audit the full chain without duplicating every production model field.

At minimum include:

- schema version;
- policy strategy ID + fingerprint;
- instrument identity;
- prior/current event IDs and fiscal periods;
- DocFlow document IDs/fingerprints;
- extraction engine identifiers and extraction confidence;
- transcript assessment summary;
- existing `ResearchDecision` identity/action/targetWeight/confidence/generatedAt/source event;
- existing backtest performance metrics;
- final backtest portfolio summary sufficient to identify the run outcome;
- existing current `RebalancePlan` status/quantities/notionals;
- existing `RebalanceOrderIntent` fields when present;
- deterministic input provenance such as the rebalance-input file path/name or a content fingerprint if already practical.

Do not claim that the backtest proves future profitability.

Do not serialize secrets, environment variables, raw provider payloads, or full raw transcript text into the audit result.

## Provider/runtime artifacts

Preserve the existing provider artifacts:

```text
prior-normalized.json
prior-structured.json
current-normalized.json
current-structured.json
manifest.json
transcript-research-result.json
```

The existing research-only mode must continue to use them as before.

The new VS-13 mode may additionally produce:

```text
transcript-research-rebalance-result.json
```

Do not commit anything under `.tradeops/`.

The repository `.gitignore` already excludes:

```text
.tradeops/
```

Do not weaken that ignore.

## .NET child-process hardening

VS-12 currently invokes the TradeOps child through bare:

```text
dotnet
```

which depends on PATH and can select an incompatible host/SDK.

VS-13 must harden this boundary.

Requirements:

1. Do not rely solely on bare PATH resolution for the TradeOps child.
2. Support an explicit operator override such as:
   ```text
   --dotnet <path-to-dotnet-host>
   ```
3. Prefer validated runtime/SDK hints when no explicit override is supplied. Appropriate inputs may include:
   - `DOTNET_HOST_PATH`;
   - `DOTNET_ROOT`;
   - `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR`;
   - another portable equivalent justified in code/tests.
4. Validate the selected host before the TradeOps child stage.
5. Respect the repository `global.json` contract:
   ```json
   {
     "sdk": {
       "version": "8.0.400",
       "rollForward": "latestPatch",
       "allowPrerelease": false
     }
   }
   ```
6. If falling back to PATH is retained, preflight it and fail with a clear diagnostic rather than silently using an incompatible SDK.
7. Do not hard-code the local `D:\DotNet` path into repository code or committed configuration.
8. Print the selected dotnet executable path and resolved version only; never print environment contents.

The worker may choose a smaller equivalent implementation if it is cross-platform, deterministic, tested, and prevents the previously observed wrong-host ambiguity.

## Child stdout/stderr diagnostics

The current system runner redirects stdout/stderr and discards it.

VS-13 must improve failure diagnostics without creating a data/secret leak.

Requirements:

- preserve `UseShellExecute = false`;
- preserve `ArgumentList`;
- no shell command concatenation;
- no environment dump;
- never print the selected API-key value;
- do not forward raw DocFlow/provider payloads or raw transcript text;
- make TradeOps/.NET host failures diagnosable with bounded output;
- output capture must be bounded in memory/size;
- any surfaced child output must be safe/sanitized and tested;
- success output remains concise.

A small tool-local `ChildProcessResult` carrying exit code and bounded stdout/stderr is acceptable.

Do not turn this slice into a generic process-execution framework.

## Provider/secret boundary

Credential mapping remains exactly:

```text
openai -> OPENAI_API_KEY
groq   -> GROQ_API_KEY
```

Rules:

- read only the selected provider credential;
- environment-only secret;
- no `--api-key`;
- no secret in child arguments;
- no secret in JSON outputs;
- no secret in error text;
- no secret in GitHub;
- no secret in tests;
- no secret in chat;
- no provider fallback;
- no environment-variable dump.

The child process may inherit the environment normally as in VS-12.

## Privacy boundary

Use only existing synthetic transcript fixtures and synthetic market/portfolio values.

No real client transcript, account, portfolio, broker, investor, or personal data.

Before handoff, scan every changed file for privacy/secret leakage.

Do not include prohibited personal/client identifiers even in statements asserting their absence.

## DocFlow boundary

VS-13 does not modify DocFlow.

Use the integrated DF-07 CLI contract unchanged:

```text
text_artifact_main.py
--provider openai|groq
--input-raw-json ...
--schema-request ...
--model ...
--output-normalized-json ...
--output-structured-json ...
--document-name ...
```

No provider SDK or HTTP logic in TradeOps.

## Expected owned files

Prefer a minimal set such as:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
tools/TradeOps.TranscriptResearchDemo/TranscriptResearchDemo.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
tests/TradeOps.UnitTests/TranscriptResearchDemoTests.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchRebalanceEndToEndTests.cs   # optional new focused test file
samples/research/provider-transcript-demo/rebalance-input.json
docs/orchestration/VS13_PROVIDER_TRANSCRIPT_REBALANCE_E2E.md
```

Modify `Program.cs` only if explicit mode routing requires it.

Do not modify `TradeOps.sln` or project files unless a genuine blocker proves the existing references are insufficient.

Do not modify:

- frozen shared models;
- `TranscriptResearchToRebalanceDemoService`;
- `ResearchToRebalanceDemoService`;
- `EventDrivenBacktester`;
- `PortfolioRebalancePlanner`;
- provider SDK/package references;
- DocFlow repository files;
- IBKR/execution code.

If one of those appears necessary, stop and document the blocker.

## Tests

CI remains network-free and secret-free.

At minimum cover:

1. existing VS-12 default research mode remains backward-compatible;
2. explicit VS-13 research-rebalance mode is required for the new path;
3. provider openai path still selects only `OPENAI_API_KEY`;
4. provider groq path still selects only `GROQ_API_KEY`;
5. no credential appears in child arguments/output JSON/errors;
6. provider prior/current invocations remain unchanged apart from allowed existing provider selection;
7. VS-13 consumer receives the generated runtime manifest;
8. VS-13 consumer receives the same existing policy;
9. rebalance input is required in explicit VS-13 mode;
10. rebalance input instrument mismatch fails closed;
11. invalid/insufficient bars fail through existing VS-11/VS-01/WS-04 semantics;
12. invalid portfolio/reference price fails through existing VS-11/VS-01/WS-03 semantics;
13. consumer calls existing `TranscriptResearchToRebalanceDemoService`;
14. no direct backtester call is introduced in CLI/harness code;
15. no direct rebalance planner call is introduced in CLI/harness code;
16. transcript research assessment/decision remain those returned by VS-11;
17. target weight is not duplicated in the rebalance input;
18. audit JSON contains the existing ResearchDecision;
19. audit JSON contains existing backtest metrics;
20. audit JSON contains existing current RebalancePlan;
21. audit JSON contains RebalanceOrderIntent when produced;
22. audit JSON contains no raw transcript/provider payload;
23. audit JSON deterministic repeated run equality;
24. `.tradeops/` remains ignored;
25. explicit `--dotnet` host override is honored;
26. environment-based dotnet host resolution precedence is deterministic;
27. incompatible/invalid dotnet host fails before TradeOps consumer execution;
28. selected dotnet version/path diagnostics are bounded and safe;
29. child stderr/stdout capture is bounded;
30. provider child output is not blindly dumped;
31. TradeOps host failure produces useful bounded diagnostics;
32. `UseShellExecute = false` and `ArgumentList` remain enforced;
33. existing VS-08 tests remain green;
34. existing VS-11 tests remain green;
35. existing VS-12 tests remain green;
36. full TradeOps suite remains green.

## CI / validation

Worker validation order:

1. targeted VS-13 tests;
2. related VS-08/VS-11/VS-12 tests;
3. full TradeOps test suite;
4. GitHub Actions exact branch-head CI;
5. privacy/secret scan;
6. update this status file;
7. stop for orchestrator review.

Do not make a real provider API call in CI.

A local real Groq VS-13 smoke is a post-integration/operator validation using local environment-only `GROQ_API_KEY`; it is not a worker CI gate.

## Real provider acceptance target

After orchestrated integration, the local operator should be able to run one command/path using:

```text
provider: groq
model: openai/gpt-oss-20b
DocFlow root: local integrated DocFlow checkout
credential: local environment-only GROQ_API_KEY
```

and obtain:

```text
DocFlow prior: success
DocFlow current: success
TradeOps VS-11 research/backtest/rebalance consumer: success
provider transcript -> backtest/rebalance demo: PASS
auditable JSON result: present, non-empty, secret-free
```

Expected research semantics for the current synthetic fixture remain those already observed:

```text
ResearchDecision.Action = SetTargetWeight
TargetWeight = 0.40
Confidence = 1
```

The worker must not hard-code those result values as logic; they are acceptance evidence produced by existing policy/rules.

## Explicitly out of scope

Do not add:

- new ResearchDecision/domain contracts;
- new earnings rules;
- new LLM/provider SDK in TradeOps;
- transcript acquisition/scraping;
- new provider fallback/retry/pricing logic;
- a new backtest engine;
- new backtest metric definitions;
- a new rebalance planner;
- broker execution;
- IBKR mutation;
- Paper order placement;
- database/persistence;
- web API;
- generic process framework;
- production scheduler;
- live-account capability.

VS-13 stops at auditable existing backtest metrics + current `RebalancePlan/RebalanceOrderIntent`.

## Completion protocol

Before handoff update this file with:

- State;
- baseline and current HEAD;
- compare vs current `main`;
- changed files;
- exact end-to-end composition path;
- public/shared contracts changed or not;
- dotnet host hardening behavior;
- child-output safety behavior;
- audit JSON shape and artifact path;
- targeted/full tests;
- exact CI run + conclusion;
- privacy/secret scan;
- blockers;
- next integration action.

Stop after this bounded slice.

Do not merge independently.
