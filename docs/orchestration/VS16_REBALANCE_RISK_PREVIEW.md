# VS-16 — Rebalance Order Intent → Existing RiskEngine Preview

## State
READY_FOR_INTEGRATION

## Repository / branch
avysotsky/TradeOps
TradeOps/vs16-rebalance-risk-preview

## Baseline
Orchestration baseline: 8a0f33d4f2198429f454b9faff7b3eb3bb90506c
Latest code-bearing integrated baseline: 54e981d51bc7de5746371106509cc92030767916
VS-15 post-merge CI: 37669861416 — SUCCESS
Full suite: 519 / 519 tests passed

OP-01 real-provider acceptance:
- provider: groq
- model: openai/gpt-oss-20b
- DocFlow prior exit: 0
- DocFlow current exit: 0
- TradeOps VS-13 exit: 0
- instrument: SAMP
- targetWeight: 0.40
- rebalance status: Ready
- runtime JSON size: 7838 bytes

## Purpose
Extend the validated provider transcript → research → backtest/rebalance demo by one NON-MUTATING step:

RebalancePlan.OrderIntent
→ deterministic tool-local TradingSignal projection
→ existing RiskEngine.CheckAsync(...)
→ auditable risk-preview result

This slice proves that the existing rebalance intent can reach the existing TradeOps risk boundary.

It MUST NOT place, submit, cancel or persist broker orders.

This is not the IBKR mutation bridge.

## Existing contracts to reuse unchanged
Reuse:
- RebalancePlan
- RebalanceOrderIntent
- TradingSignal
- RiskDecision
- RiskSettings
- RiskControlSnapshot
- RiskEngine
- IRiskEngine
- IExchangeClient
- IRiskControlService
- TranscriptResearchToRebalanceDemoService

Do not change the frozen ResearchDecision contract.
Do not change IBKR capabilities.

## Preview architecture
The VS-16 risk path is tool/demo-local.

Do NOT call:
- OrderManager.ExecuteSignalAsync
- SignalExecutionService.ExecuteSignalAsync
- IExchangeClient.PlaceOrderAsync
- IExchangeClient.CancelOrderAsync
- IbkrPaperExchangeClient.PlaceOrderAsync

The only production risk operation allowed is RiskEngine.CheckAsync(...).

## Deterministic intent → signal projection
Project the integrated RebalanceOrderIntent into a tool-local TradingSignal:

Symbol = OrderIntent.Instrument.Symbol
Side = OrderIntent.Side
RequestedQuantity = OrderIntent.Quantity
SignalType = RebalanceRiskPreview
CreatedAt = CurrentRebalancePlan.PlannedAt
Source = transcript-research-rebalance-risk-preview
RiskPercent = null
StopLoss = null
TakeProfit = null

Signal ID must be deterministic from stable non-secret inputs such as DecisionId, Symbol, Side, Quantity and PlannedAt.
Guid.NewGuid() is prohibited for the preview signal.

## Deterministic risk environment
Add a strict synthetic input at:
samples/research/provider-transcript-demo/risk-preview-input.json

It must be schema-versioned and contain deterministic RiskSettings and RiskControlSnapshot-equivalent values.

Recommended fixture semantics:
- maxPositionSize = 50
- maxOrderSize = 50
- maxDailyLoss = 1000
- maxOpenPositions = 5
- allowedSymbols = [SAMP]
- tradingEnabled = true
- emergencyStop = false
- settlementCurrency = USD
- dailyNetRealizedPnL = 0
- activePositionMismatchCount = 0
- deterministic updatedAt

Current exchange positions must be derived from the already validated VS-13 currentPortfolio. Do not duplicate current position quantities in the risk-preview input.

Strict parsing requirements:
- exact schemaVersion
- case-sensitive properties
- unknown members rejected
- comments/trailing junk rejected
- positive limits
- no blank/duplicate allowed symbols

## Tool-local adapters
Use a narrow read-only IExchangeClient adapter:
- GetPositionsAsync returns positions derived from currentPortfolio
- all mutation methods throw if called
- tests prove no mutation method is called

Use a narrow IRiskControlService adapter:
- GetSnapshotAsync returns deterministic preview snapshot
- RecordRejectionAsync may capture an in-memory rejection
- state mutation methods must not be used

Do not build a generic exchange simulator.

## CLI
Add an additive option:
--risk-preview-input <path>

Rules:
1. --risk-preview-input requires --rebalance-input.
2. Without --risk-preview-input, VS-15 behavior remains unchanged.
3. With risk preview, validate the input before provider work where practical.
4. Run existing provider/DocFlow stages unchanged.
5. Run existing VS-13 research/backtest/rebalance composition unchanged.
6. Require CurrentRebalancePlan.OrderIntent for actionable preview.
7. Invoke existing RiskEngine.CheckAsync exactly once.
8. Produce a separate auditable runtime result.

Recommended result filename:
transcript-research-risk-preview-result.json

Do not overwrite research-only or rebalance-only result contracts.

## Output
Preserve existing research/backtest/rebalance evidence and add a riskPreview object with:
- mode = deterministicSyntheticState
- signalId
- decisionId
- symbol
- side
- requestedQuantity
- requiresRiskApproval
- allowed
- reasons

For the current SAMP fixture:
riskPreview.allowed = true
riskPreview.reasons = []

Do not duplicate planner logic.

## Provider harness wiring
The provider harness may pass --risk-preview-input into the existing transcript consumer.

Preserve VS-14/VS-15 guarantees:
- provider selection openai|groq
- explicit model
- environment-only credential
- explicit --dotnet
- deterministic host resolution
- global.json preflight
- bounded stdout/stderr
- provider output suppression
- no provider fallback
- no --api-key

## OP-01 warning cleanup
OP-01 emitted CS8604 at the DotNetSdkPolicy RollForward construction path.

Because VS-16 may touch the provider harness, remove this warning narrowly.
Do not suppress nullable analysis.
Preserve current fail-closed global.json behavior and latestPatch semantics.

## Ownership
VS-16 may modify:
- tools/TradeOps.TranscriptResearchDemo/**
- tools/TradeOps.ProviderTranscriptResearchDemo/ProviderTranscriptResearchDemo.cs
- tests/TradeOps.UnitTests/ProviderTranscriptResearchDemoTests.cs
- tests/TradeOps.UnitTests/ProviderTranscriptResearchRebalanceEndToEndTests.cs
- samples/research/provider-transcript-demo/risk-preview-input.json
- docs/orchestration/VS16_REBALANCE_RISK_PREVIEW.md
- one new focused VS-16 test file if useful

Avoid application/domain production modifications unless a concrete blocker is proven.
If such a change appears necessary, stop and document the blocker before broadening scope.

## Explicitly out of scope
Do not:
- change ResearchDecision
- change RebalancePlan or RebalanceOrderIntent contracts
- add IBKR placement/cancellation
- change exchange capability flags
- call OrderManager
- call SignalExecutionService
- persist TradingSignal or Order
- create broker ClientOrderId
- add provider backends
- change DocFlow
- add live market/account state
- add API endpoints
- add database migrations

## Tests
CI remains network-free and secret-free.

At minimum cover:
1. research-only path unchanged
2. rebalance path unchanged
3. --risk-preview-input parses
4. missing value fails closed
5. risk preview without rebalance input fails closed
6. missing risk file fails before provider work where practical
7. strict risk JSON rejects malformed/unknown fields
8. deterministic intent→signal mapping
9. deterministic signal ID
10. current portfolio mapped to preview exchange positions correctly
11. existing RiskEngine invoked exactly once
12. SAMP fixture allowed
13. trading disabled rejects
14. emergency stop rejects
15. disallowed symbol rejects
16. max-order-size violation rejects
17. projected max-position violation rejects
18. incomplete daily accounting rejects
19. preview exchange mutations never called
20. no OrderManager/SignalExecutionService call in production preview code
21. no direct IBKR mutation
22. result preserves rebalance evidence plus risk preview
23. privacy/secret guarantees preserved
24. CS8604 removed without suppression
25. full TradeOps suite green

## Real-provider validation
After exact-head CI, run one local Groq preview smoke if GROQ_API_KEY is already available in the environment.

Expected path:
Groq
→ DocFlow prior/current
→ TradeOps research/backtest/rebalance
→ RebalanceOrderIntent
→ existing RiskEngine
→ riskPreview.allowed = true

No order is placed.
Runtime output stays under .tradeops/ and is not committed.

## Privacy
Committed samples/tests must be synthetic only.
Never commit or print real broker/account/order/provider/client/private trading data.

## Completion
Before handoff:
- fetch live TradeOps main
- fetch live DocFlow main
- fetch SEC-02 state without touching its files
- compare VS-16 vs current main
- verify exact branch/PR-head CI
- verify ownership
- verify no frozen contract change
- verify no mutation path
- run privacy/secret scan
- record real Groq preview smoke status
- update this document
- create draft PR only

Do not merge independently.
Stop after the bounded preview slice.


## Completion status — 2026-10-08

Implementation HEAD before this status-only update:
`fc62bf0b9f298578894b7f92bfd6de8d5ce03bf9`

Completed:
- reused the existing VS-13 composition through a tool-local `TranscriptResearchRebalanceDemoRunner.Create(...)` extraction; no planner/backtester duplication
- added strict versioned synthetic risk-preview input parsing
- projected the existing `CurrentRebalancePlan.OrderIntent` to a deterministic `TradingSignal`
- reused the existing production `RiskEngine.CheckAsync(...)` exactly once per preview
- derived preview exchange positions from the existing VS-13 `currentPortfolio`
- added read-only tool-local `IExchangeClient` / `IRiskControlService` adapters whose mutation methods throw
- emitted a separate `transcript-research-risk-preview-result.json` artifact preserving research/backtest/rebalance evidence
- forwarded `--risk-preview-input` through the provider harness without changing provider/DocFlow stages
- removed the provider-harness `CS8604` warning by narrowing the validated `rollForward` value; no pragma/nullable suppression

Public/shared contracts changed:
`NO`

Application/Domain production files changed:
`NO`

Broker/execution mutation path added or invoked:
`NO`

Implementation-head validation:
- GitHub Actions build run: `37759718404 — SUCCESS`
- full test suite: `537 / 537 passed`
- build warnings: `0`
- `CS8604`: absent from the build log
- API + PostgreSQL smoke: success
- signed webhook demo: success
- customer TradingView demo: success
- Docker/deployment validation: success
- changed-file ownership: within the VS-16 allowlist only
- privacy scan: no committed key/private-key material detected
- preview runtime source contains no `OrderManager`, `SignalExecutionService`, or `IbkrPaperExchangeClient` bridge and does not use `Guid.NewGuid()` for the signal ID

Real Groq preview smoke:
`NOT RUN in this worker environment` — `GROQ_API_KEY` and a local `dotnet` host are not available here. This does not replace OP-01; the requested real-provider run remains conditional on an already-available local credential/runtime.

Draft PR:
`#70 — VS-16: Rebalance risk preview`

Blockers:
`NONE for orchestrator review`

Next integration action:
orchestrator reviews draft PR #70 and the final branch-tip CI. Do not merge from the worker chat.
