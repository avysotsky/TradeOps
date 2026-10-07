# VS-12 — Provider-Selectable Two-Repository Demo Harness

## State

READY_FOR_INTEGRATION

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs12-provider-selectable-transcript-demo
```

## Baseline

```text
f4cb916810790e80245b9a025a62b92ea3efd1bf
```

Latest validated TradeOps production baseline before this docs launch:

```text
main: 9dcc77b0cf216eb721065e5c7e7e12bf4786c1ff
CI: 37630929243 — SUCCESS
```

Parallel dependency:

```text
DocFlow DF-07 — Groq Schema-Driven Text Extraction Backend
branch: DocFlow/df07-groq-schema-backend
contract: text_artifact_main.py --provider openai|groq
```

## Purpose

Extend the already integrated VS-10 operator harness so the same two-repository demo can select either OpenAI or Groq at the DocFlow CLI boundary.

Target:

```text
--provider openai
+ OPENAI_API_KEY
+ explicit model
-> existing DocFlow text artifact CLI
-> existing VS-08
-> ResearchDecision

OR

--provider groq
+ GROQ_API_KEY
+ explicit model
-> existing DocFlow text artifact CLI
-> existing VS-08
-> ResearchDecision
```

This slice changes only provider selection and credential preflight at the operator boundary.

It must not add any provider SDK to TradeOps and must not alter research semantics.

## Ownership

VS-12 owns only the existing integrated VS-10 tool and its tests/docs.

Expected files:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
docs/orchestration/VS12_PROVIDER_SELECTABLE_TRANSCRIPT_DEMO.md
```

Modify `Program.cs` only if required for argument plumbing.

Do not modify:

- TradeOps shared/domain models;
- TranscriptResearchDecisionDemoService;
- TranscriptResearchToRebalanceDemoService;
- VS-08/VS-11 semantics;
- earnings schema;
- DocFlow code;
- OpenAI/Groq SDK packages.

No new project/solution wiring should be needed.

## CLI

Extend the integrated VS-10 command with:

```text
--provider openai|groq
```

Recommended command:

```text
dotnet run --project tools/TradeOps.ProviderTranscriptResearchDemo -- \
  --provider groq \
  --docflow-root <path-to-DocFlow-checkout> \
  --model <explicit-model>
```

Backward compatibility:

```text
default provider = openai
```

Existing VS-10 invocation without `--provider` must retain its current behavior.

Model remains mandatory and has no default.

Unknown provider must fail before any child process starts.

Provider parsing should be case-insensitive only if that behavior is explicit and tested; otherwise prefer exact lowercase values for a smaller contract.

## Provider representation

Use a narrow internal representation.

A small enum is appropriate, for example:

```csharp
public enum ProviderTranscriptResearchProvider
{
    OpenAi,
    Groq
}
```

or an equivalent private/internal value object.

Do not introduce provider identity into frozen/shared TradeOps domain contracts.

## Credential mapping

Credential names are fixed at the operator boundary:

```text
openai -> OPENAI_API_KEY
groq   -> GROQ_API_KEY
```

Before any child process invocation:

- resolve the selected provider;
- read only the matching environment variable;
- fail closed if missing/blank;
- do not require the other provider's credential.

Never:

- accept `--api-key`;
- copy the credential into child arguments;
- copy the credential into manifest/artifacts;
- print the credential;
- print all environment variables;
- include the credential value in error messages.

It is acceptable to mention the environment-variable NAME in a bounded error, e.g. `GROQ_API_KEY is not configured`.

## DocFlow invocation

VS-12 depends on the DF-07 CLI contract:

```text
python text_artifact_main.py
  --provider <openai|groq>
  --input-raw-json ...
  --schema-request ...
  --model ...
  --output-normalized-json ...
  --output-structured-json ...
  --document-name ...
```

For both prior/current producer calls, insert exactly:

```text
--provider <selected-provider>
```

into the existing DF-06/DF-07 CLI argument list.

Do not alter any other artifact path, document name, model, schema, or work-directory semantics from integrated VS-10.

The child process still inherits the environment normally.

Do not manually set or copy the secret into `ProcessStartInfo.Environment`.

## Process safety

Preserve integrated VS-10 behavior:

- `UseShellExecute = false`;
- `ArgumentList`;
- no shell;
- no command-string concatenation;
- bounded/no child payload forwarding;
- no transcript/provider payload logging.

Provider name and model name are safe to print.

## No provider SDK in TradeOps

TradeOps must not reference:

- OpenAI SDK;
- Groq SDK;
- HTTP client provider endpoints;
- Groq/OpenAI request/response DTOs.

TradeOps only selects a string CLI provider and verifies the expected credential environment variable exists.

DocFlow remains the provider integration owner.

## Runtime outputs

Preserve all integrated VS-10 runtime files and locations unchanged:

```text
prior-normalized.json
prior-structured.json
current-normalized.json
current-structured.json
manifest.json
transcript-research-result.json
```

Do not add provider-specific committed result fixtures.

Do not write the provider/API-key identity into structured research output or manifest.

Bounded console status may state selected provider/model.

## Failure behavior

Return non-zero before process execution for:

- unknown provider;
- missing selected provider credential;
- existing invalid DocFlow root/entrypoint;
- other existing VS-10 preflight failures.

Existing VS-10 child failure semantics remain unchanged:

- prior provider failure stops pipeline;
- current provider failure stops before VS-08;
- schema-invalid exit 2 remains failure;
- VS-08 failure remains failure;
- partial runtime artifacts remain preserved.

Do not fall back automatically from Groq to OpenAI or vice versa.

## Tests

CI remains network-free.

Use existing fake process runner and environment reader.

At minimum cover:

1. default provider is OpenAI;
2. explicit `--provider openai` resolves OpenAI;
3. explicit `--provider groq` resolves Groq;
4. unknown provider rejected before child process;
5. model remains mandatory;
6. OpenAI requires only `OPENAI_API_KEY`;
7. Groq requires only `GROQ_API_KEY`;
8. Groq succeeds when OpenAI key is absent;
9. OpenAI succeeds when Groq key is absent;
10. missing selected key fails before child process;
11. error names missing environment variable but not its value;
12. neither credential value appears in any child argument;
13. prior DocFlow call includes exact `--provider openai` when OpenAI selected;
14. current DocFlow call includes exact `--provider openai`;
15. prior DocFlow call includes exact `--provider groq` when Groq selected;
16. current DocFlow call includes exact `--provider groq`;
17. explicit model forwarded unchanged for both providers;
18. existing Python executable/workdir behavior unchanged;
19. existing no-shell ArgumentList contract unchanged;
20. existing short-circuit/failure tests remain green for both provider selections where practical;
21. existing runtime artifact names/locations unchanged;
22. existing consumer manifest copy unchanged;
23. existing VS-08 child invocation unchanged;
24. no provider SDK/HTTP endpoint string introduced in TradeOps;
25. no research/earnings decision logic added;
26. no API-key CLI argument added;
27. no secret/transcript console leak;
28. full TradeOps suite passes.

## Integration dependency

VS-12 implementation/CI can complete in parallel with DF-07 because tests use a fake process runner.

However, do not perform the real Groq end-to-end smoke until DF-07 is integrated into the supplied DocFlow checkout.

At integration review, Development Orchestrator must verify the final DF-07 CLI contract still matches:

```text
--provider openai|groq
```

If DF-07 changes that contract, synchronize VS-12 before merge.

## Real Groq smoke

Not a CI gate for VS-12 implementation.

After both DF-07 and VS-12 are integrated, the local operator can run:

```text
dotnet run --project tools/TradeOps.ProviderTranscriptResearchDemo -- \
  --provider groq \
  --docflow-root <local-DocFlow> \
  --model <explicit-supported-model>
```

The user's `GROQ_API_KEY` is expected to exist only in their local environment.

Do not request or expose the key in GitHub or chat.

## Explicitly out of scope

Do not add:

- provider fallback;
- provider/model registry;
- Gemini/Ollama;
- retries;
- pricing/rate-limit logic;
- transcript acquisition;
- domain-model provider fields;
- backtest/rebalance changes;
- broker execution;
- API secret storage;
- web API.

## Privacy

Use existing synthetic samples only.

Before handoff scan all changed files according to orchestration privacy rules.

Do not mention prohibited personal/client names even in absence statements.

## Completion protocol

Before handoff update this file with:

- State;
- Current HEAD;
- changed files;
- provider CLI/env mapping;
- public/shared contracts changed or not;
- tests;
- exact CI;
- privacy/secret scan;
- blockers;
- next integration action.

Stop after the bounded slice.

Do not merge independently.

## Worker handoff — 2026-10-07

Validated implementation HEAD:

```text
0f9fb0b2e23dd91ea4485efe07f7aa7627590621
```

Changed files in the bounded VS-12 slice:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
docs/orchestration/VS12_PROVIDER_SELECTABLE_TRANSCRIPT_DEMO.md
```

Implemented operator contract:

```text
default provider: openai
--provider openai -> OPENAI_API_KEY
--provider groq   -> GROQ_API_KEY
model: explicit and mandatory
```

Only the selected provider credential is read. Credential values are not accepted by CLI, not placed in child arguments, not copied into runtime manifest/config, and not written to bounded console output. Child processes inherit the environment normally; `ProcessStartInfo.Environment` is not modified.

Both prior/current DocFlow invocations now contain exactly:

```text
--provider <selected-provider>
```

All existing VS-10 artifact paths, document names, model forwarding, Python executable/workdir behavior, bounded child-output behavior, `UseShellExecute = false`, `ArgumentList`, no-shell execution, and the VS-08 child invocation remain unchanged.

Public/shared TradeOps contracts changed:

```text
none
```

TradeOps provider SDK/HTTP/domain changes:

```text
none
```

Validation:

```text
CI: 37639424120 — SUCCESS
full TradeOps tests: 490/490 passed
restore/build: SUCCESS
API + PostgreSQL smoke: SUCCESS
signed webhook demo: SUCCESS
customer TradingView demo: SUCCESS
client pilot starter validation: SUCCESS
Docker/deployment validation: SUCCESS
Docker image build: SUCCESS
```

Privacy/secret scan:

```text
PASS
- no OPENAI_API_KEY/GROQ_API_KEY values committed
- no bearer/key-shaped secret values introduced
- no provider HTTP endpoint introduced into TradeOps
- no personal/client data introduced
- existing synthetic fixtures only
```

Live DF-07 integration check at handoff:

```text
DocFlow branch: DocFlow/df07-groq-schema-backend
DF-07 HEAD: 171830b97174e55824de280eef280daa8f7b8b96
DF-07 CI: 37639653424 — SUCCESS
CLI contract verified in live code:
  --provider choices=("openai", "groq")
  default="openai"
```

Real Groq smoke:

```text
NOT RUN
```

Reason: it is intentionally outside CI and must use the user's local `GROQ_API_KEY` only after orchestrated DF-07 + VS-12 integration.

Blockers:

```text
No implementation blocker.
Real Groq smoke remains gated on orchestrated integration of DF-07 and VS-12.
```

Next integration action:

```text
Development Orchestrator reviews VS-12 against the integrated/final DF-07 CLI contract,
then merges in orchestrated order. After both are integrated, run one local Groq smoke
with synthetic VS-09 inputs and an explicit supported model, then compose the produced
artifacts through the existing VS-11 research -> backtest/rebalance path.
```

