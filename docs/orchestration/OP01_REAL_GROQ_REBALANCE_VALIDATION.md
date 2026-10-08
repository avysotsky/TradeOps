# OP-01 — Real Groq Provider → Research → Backtest/Rebalance Validation

## State

DONE

## Validation date

2026-10-08

## Repository baseline used by the operator

```text
TradeOps main: 8afc2528eb672a424bbee3635afff2484a7a8fc4
DocFlow root: local integrated checkout
dotnet host: D:\DotNet\dotnet.exe
dotnet SDK: 8.0.421
provider: groq
model: openai/gpt-oss-20b
credential source: GROQ_API_KEY environment variable only
```

No credential value is recorded.

## Command path

The operator ran the integrated VS-15 provider harness with the deterministic rebalance input:

```text
tools/TradeOps.ProviderTranscriptResearchDemo
--provider groq
--model openai/gpt-oss-20b
--docflow-root <local DocFlow root>
--dotnet <local dotnet host>
--rebalance-input samples/research/provider-transcript-demo/rebalance-input.json
```

## Observed stage result

```text
Stage: initialize
Provider: groq
Model: openai/gpt-oss-20b
dotnet SDK: 8.0.421

Stage: DocFlow prior
Exit status: 0

Stage: DocFlow current
Exit status: 0

Stage: TradeOps VS-13
Exit status: 0

PROVIDER TRANSCRIPT RESEARCH DEMO: PASS
```

## Result artifact

Runtime-only artifact:

```text
.tradeops/provider-transcript-demo/transcript-research-rebalance-result.json
```

Observed size:

```text
7838 bytes
```

Observed parsed values:

```text
instrument.symbol = SAMP
researchDecision.targetWeight = 0.40
currentRebalancePlan.status = Ready
```

The integrated VS-13 result type always serializes the backtest metrics, final backtest portfolio and current rebalance plan from `TranscriptResearchToRebalanceDemoService`; the successful consumer exit and non-empty JSON artifact confirm the composition completed. No runtime artifact is committed.

## Acceptance result

```text
REAL_GROQ_PROVIDER_RESEARCH_BACKTEST_REBALANCE_VALIDATED: true
```

Validated live path:

```text
Groq
-> DocFlow prior/current normalization + schema extraction
-> provider-compatible structured transcript artifacts
-> TradeOps transcript research
-> ResearchDecision
-> existing research/backtest/rebalance composition
-> RebalancePlan / RebalanceOrderIntent
-> auditable runtime JSON
```

## Privacy / credential boundary

PASS.

- `GROQ_API_KEY` stayed environment-only.
- No credential value was printed in the supplied operator transcript.
- No provider request/response payload was committed.
- No raw runtime artifact is committed.
- `.tradeops/` remains local runtime state.

## Non-blocking compiler warning observed

The operator run emitted:

```text
CS8604: Possible null reference argument for parameter 'RollForward'
```

at the `DotNetSdkPolicy` construction path in:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
```

This did not affect the E2E result and is not an OP-01 blocker, but should be removed in a small warning-cleanup slice before treating the provider demo as polished client-facing CLI output.

## Final verdict

```text
OP-01 PASS
```

The real Groq provider-backed transcript path through research, backtest and rebalance is validated on the integrated TradeOps/DocFlow stack.

## Next orchestrator action

1. Record OP-01 as DONE.
2. Preserve runtime artifacts locally only.
3. Address the CS8604 warning in a bounded follow-up.
4. Continue the next client-facing slice without enabling broker mutation.
5. Keep IBKR mutation blocked until the independent real Paper read-path gate passes.
