# OP-02 — Real Groq → Rebalance → Risk Preview Validation

## State

DONE

## Validation date

2026-10-08

## Repository baseline used by the operator

```text
TradeOps main: 084624291ca3a0554bcd6a31e8541fc187490e6f
dotnet host: D:\DotNet\dotnet.exe
dotnet SDK: 8.0.421
provider: groq
model: openai/gpt-oss-20b
credential source: GROQ_API_KEY environment variable only
```

No credential value is recorded.

## Observed stage result

```text
Stage: initialize
Provider: groq
Model: openai/gpt-oss-20b
Dotnet SDK: 8.0.421

Stage: DocFlow prior
Exit status: 0

Stage: DocFlow current
Exit status: 0

Stage: TradeOps VS-16
Exit status: 0

PROVIDER TRANSCRIPT RESEARCH DEMO: PASS
```

## Result artifact

Runtime-only artifact:

```text
.tradeops/provider-transcript-demo/transcript-research-risk-preview-result.json
```

Observed size:

```text
8168 bytes
```

Observed parsed values:

```text
instrument.symbol = SAMP
researchDecision.targetWeight = 0.40
currentRebalancePlan.status = Ready

riskPreview.symbol = SAMP
riskPreview.side = Buy
riskPreview.requestedQuantity = 30.00
riskPreview.requiresRiskApproval = true
riskPreview.allowed = true
riskPreview.reasons = []
riskPreview.signalId = f30bfe9f-c3f1-39b1-5cd5-7d9b98ffea4f
```

## Transient provider diagnostic note

The first OP-02 attempt stopped at:

```text
DocFlow current exit 1
```

before TradeOps VS-16 was invoked.

A direct isolated rerun of the same DocFlow current fixture with the same provider/model then succeeded:

```text
provider: groq
model: openai/gpt-oss-20b
validationStatus: valid
exit: 0
```

The subsequent full OP-02 rerun succeeded end-to-end. This is classified as a transient provider-stage failure, not a VS-16/RiskEngine defect. No retry behavior was added to production code as part of OP-02.

## Acceptance result

```text
REAL_GROQ_REBALANCE_RISK_PREVIEW_VALIDATED: true
```

Validated live path:

```text
Groq
-> DocFlow prior/current
-> structured earnings facts
-> ResearchDecision
-> existing backtest
-> current RebalancePlan
-> RebalanceOrderIntent
-> deterministic TradingSignal projection
-> existing RiskEngine.CheckAsync
-> riskPreview.allowed = true
```

## Mutation boundary

PASS.

This validation does not place an order.

The VS-16 preview path uses read-only adapters and does not invoke:

```text
OrderManager
SignalExecutionService
IbkrPaperExchangeClient.PlaceOrderAsync
IExchangeClient.PlaceOrderAsync
IExchangeClient.CancelOrderAsync
```

The result is risk approval preview only.

## Privacy / credential boundary

PASS.

- GROQ_API_KEY remained environment-only.
- No credential value was printed in the supplied operator transcript.
- No raw provider request/response was committed.
- No runtime result artifact is committed.
- .tradeops remains local runtime state.
- Synthetic SAMP fixture only.

## Final verdict

```text
OP-02 PASS
```

The real Groq provider-backed transcript path through research, backtest, rebalance and the existing TradeOps RiskEngine is validated.

## Next orchestrator action

1. Record OP-02 as DONE.
2. Preserve runtime artifacts locally only.
3. Keep broker mutation blocked until the independent real IBKR Paper gate passes.
4. With VS-16 complete, SEC-001 history remediation may now move from planning to an explicit owner-approval execution gate.
