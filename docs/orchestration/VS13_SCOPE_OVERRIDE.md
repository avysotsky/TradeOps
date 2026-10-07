# VS-13 Scope Override — Parallel Split

This file is authoritative over conflicting ownership statements in the initial VS-13 spec.

Development Orchestrator split the original broad VS-13 scope before implementation.

## VS-13 owns

- `tools/TradeOps.TranscriptResearchDemo/**`
- deterministic synthetic rebalance input
- auditable research/backtest/rebalance JSON
- VS-13 consumer tests
- VS-13 handoff documentation

## VS-14 owns

- `tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs`
- `tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs`
- dotnet host resolution / explicit `--dotnet`
- global.json / SDK preflight
- bounded child stdout/stderr capture and safe diagnostics

## Deferred VS-15

After VS-13 and VS-14 are integrated, VS-15 will perform the small provider-harness -> VS-13 consumer wiring.

## Mandatory prohibition for VS-13

VS-13 MUST NOT modify:

```text
tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
```

VS-13 MUST NOT implement dotnet host resolution, `--dotnet`, child-process output capture, or provider-harness process diagnostics.

Those concerns belong exclusively to VS-14.

VS-13 should implement the explicit research-rebalance consumer mode in `TradeOps.TranscriptResearchDemo`, plus deterministic rebalance input and audit JSON.

If the initial VS-13 spec conflicts with this file, this file wins.
