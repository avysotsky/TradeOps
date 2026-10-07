# VS-04 — Configurable Public-Data Client Demo

## State

READY_FOR_INTEGRATION

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/vs04-configurable-public-demo
```

## Baseline

```text
e5fd5762822b38f4e6eb9194ea21c4da34615508
```

## Exact CI-validated implementation HEAD

```text
58f66558c65813e757dd12d48d0003d78aac3d0e
```

This is the exact code/test HEAD validated by the full CI run below. This status-file commit follows that implementation HEAD and is documentation-only.

## Goal

Connect the already integrated VS-02 public IBM data path with the already integrated VS-03 strict research-policy configuration without introducing another research, portfolio or backtest pipeline.

The runnable client-facing flow is now:

```text
client policy JSON
-> EarningsResearchPolicyConfiguration.Load
-> strict deterministic validation
-> normalized policy fingerprint
-> existing EarningsDecisionRuleSettings
-> existing EarningsTargetWeightPolicy
-> validated PublicResearchDemoPolicy input

IBM SEC submissions/companyfacts + IBM Alpha Vantage daily bars
-> existing VS-02 parsing/provenance path
-> existing ResearchToRebalanceDemoService
-> existing EventDrivenBacktester
-> existing PortfolioRebalancePlanner
-> console output
-> JSON artifact
```

No alpha, optimization or profitability claim is made by the sample configuration.

## Composer boundary

`PublicResearchDemoComposer` no longer owns hardcoded research thresholds, target weights or StrategyId.

It now accepts an explicit validated input:

```text
PublicResearchDemoPolicy
  SchemaVersion
  StrategyId
  Fingerprint
  EarningsDecisionRuleSettings
  EarningsTargetWeightPolicy
```

The CLI owns:

```text
--policy <path>
-> file read
-> EarningsResearchPolicyConfiguration.Load
-> structured validation
-> existing VS-03 conversions
-> PublicResearchDemoPolicy
```

The composer owns:

```text
PublicDemoRawSnapshot
+ validated PublicResearchDemoPolicy
-> existing public-data parsing
-> existing research/rebalance/backtest pipeline
```

The composer does not read policy files and remains directly testable with in-memory fixtures.

## Policy validation behavior

`--policy <path>` is required for client-ready execution.

Policy loading and validation happen before:

- public network acquisition;
- cache read/write mutation that would be caused by this run;
- composition;
- backtest execution.

Malformed JSON is handled by the existing `EarningsResearchPolicyConfiguration`; raw `JsonException` is not emitted as the policy UX.

Validation failure is structured as:

```text
POLICY VALIDATION: FAIL
code=<code>
path=<json-or-cli-path>
message=<human-readable message>
```

Examples of transport-level failures include:

```text
policy_required
policy_file_not_found
policy_file_read_failed
```

Schema/content validation continues to use the existing VS-03 validation codes, including `invalid_json`, `unsupported_schema_version`, `unknown_property` and the existing threshold/weight validation codes.

Any policy failure returns a non-zero exit code and prevents the public-data pipeline from starting.

## Policy fingerprint behavior

The CLI does not implement a second fingerprint algorithm.

Fingerprint comes from the existing:

```text
EarningsResearchPolicyConfiguration.Load(...).Fingerprint
```

Therefore VS-03 semantics remain authoritative:

- SHA-256 over normalized logical policy fields;
- 64 lowercase hexadecimal characters;
- JSON whitespace does not affect the fingerprint;
- JSON property order does not affect the fingerprint;
- equivalent decimal forms do not affect the fingerprint;
- a logical policy change such as Positive target weight 0.40 -> 0.20 changes the fingerprint.

The fingerprint is carried through the validated composer input and written to both console output and the machine-readable JSON artifact.

## Output / auditability

Console and JSON output now expose:

```text
Policy
  SchemaVersion
  StrategyId
  Fingerprint

Research result
  Instrument
  SEC EventId
  Accession
  FiscalPeriod
  PublishedAt
  Assessment
  TargetWeight

Backtest
  Period
  EventCount
  InitialEquity
  FinalEquity
  TotalReturn
  CAGR
  MaxDrawdown
  Sharpe
  Sortino
  Turnover

Current rebalance
  PortfolioSource
  CurrentPortfolioWeight
  ProposedTargetWeight
  Side
  Quantity
  EstimatedNotional
  Status
```

The JSON artifact includes `policy.fingerprint`, allowing a stored run artifact to identify the exact normalized client policy used for that run.

## Determinism / policy-effect acceptance coverage

Tests prove that the same public-data snapshot plus logically equivalent policy JSON yields the same:

```text
policy fingerprint
ResearchDecision.DecisionId
assessment
target weight
backtest TotalReturn
current rebalance quantity
```

The commercial acceptance test also runs the same public-data fixture with two valid policies:

```text
Policy A PositiveTargetWeight = 0.40
Policy B PositiveTargetWeight = 0.20
```

and verifies:

- fingerprints differ;
- `ResearchDecision.DecisionId` differs;
- resulting target weights are 0.40 vs 0.20;
- current rebalance quantity differs;
- current rebalance notional differs.

No assertion is made that either policy is better, optimized or more profitable.

## Sample policy

The existing sample remains the only sample format:

```text
samples/research/earnings-policy.sample.json
```

No competing sample policy schema was added.

The sample values are configuration examples only, not recommendations or alpha claims.

## Runnable commands

Cached/offline-capable execution when a verified VS-02 cache already exists:

```bash
dotnet run --project tools/TradeOps.PublicResearchDemo \
  --configuration Release -- \
  --policy samples/research/earnings-policy.sample.json
```

PowerShell equivalent:

```powershell
dotnet run --project tools/TradeOps.PublicResearchDemo `
  --configuration Release -- `
  --policy samples/research/earnings-policy.sample.json
```

First opt-in public network acquisition still requires the existing VS-02 runtime gates.

Linux/macOS:

```bash
TRADEOPS_PUBLIC_DEMO_CONFIRM=RUN_PUBLIC_DATA_DEMO \
TRADEOPS_SEC_USER_AGENT="TradeOpsPublicDemo/1.0 contact@example.com" \
dotnet run --project tools/TradeOps.PublicResearchDemo \
  --configuration Release -- \
  --policy samples/research/earnings-policy.sample.json
```

PowerShell:

```powershell
$env:TRADEOPS_PUBLIC_DEMO_CONFIRM = "RUN_PUBLIC_DATA_DEMO"
$env:TRADEOPS_SEC_USER_AGENT = "TradeOpsPublicDemo/1.0 contact@example.com"
dotnet run --project tools/TradeOps.PublicResearchDemo `
  --configuration Release -- `
  --policy samples/research/earnings-policy.sample.json
```

The contact value above is a placeholder. A real SEC acquisition must provide an appropriate runtime contact-style User-Agent. No such personal value is committed.

Existing optional arguments remain:

```text
--refresh
--cache <path>
--json <path>
```

## Network / cache boundaries

Unchanged from VS-02:

- network acquisition remains explicit opt-in;
- SEC User-Agent remains runtime-only;
- cache resource checksums remain in force;
- ordinary CI does not access SEC or Alpha Vantage;
- historical availability/provenance semantics are unchanged;
- `SecHistoricalAvailabilityPolicy`, `AcceptedAt`, `PublishedAt`, `RetrievedAt` and `ResearchDecisionTimingMode` semantics are unchanged.

## Frozen/public/shared contracts changed

No.

The following frozen/production contracts and semantics were not changed:

```text
InstrumentReference
ResearchDecision
ResearchDecisionAction
ResearchDecisionValidationResult
EarningsEvent
ResearchSourceProvenance
MarketDataBar
PortfolioSnapshot
RebalancePlan
RebalanceOrderIntent
BacktestPerformanceMetrics
DeterministicEarningsDecisionRule
PortfolioRebalancePlanner
EventDrivenBacktester
SecEarningsEventFactory
```

The new records are tool-local demo/CLI boundary records only.

## Changed files

Implementation/test HEAD:

```text
tools/TradeOps.PublicResearchDemo/PublicResearchDemo.cs
tests/TradeOps.UnitTests/PublicResearchDemoTests.cs
```

Completion documentation:

```text
docs/orchestration/VS04_CONFIGURABLE_PUBLIC_DEMO.md
```

No client/person names or personal client data were added to code, docs, tests, fixtures, sample policy, branch name or commit messages.

## Tests

Added/extended deterministic coverage for:

- existing public-data fixture with explicit policy input;
- existing historical after-hours availability behavior with explicit policy input;
- valid repository sample policy file load path;
- valid policy mapping to existing `EarningsDecisionRuleSettings`;
- valid policy mapping to existing `EarningsTargetWeightPolicy`;
- missing policy file structured error;
- malformed JSON structured error;
- invalid policy preventing cache mutation/composition/network path;
- StrategyId propagation into `ResearchDecision`;
- fingerprint propagation into client-readable output;
- fingerprint propagation into JSON artifact;
- same logical policy -> same fingerprint/run identity/results;
- different target-weight policy -> different fingerprint;
- different target-weight policy -> different `ResearchDecision.DecisionId`;
- target weight changes according to policy;
- rebalance quantity/notional changes according to policy;
- existing VS-01/VS-02/VS-03 and solution tests remain green through full CI.

## Full CI run

Exact validated implementation HEAD:

```text
58f66558c65813e757dd12d48d0003d78aac3d0e
```

GitHub Actions:

```text
workflow: build
run number: 676
run id: 37598254032
event: push
conclusion: SUCCESS
```

Validated stages:

```text
restore
build
unit tests
API + PostgreSQL smoke test
signed webhook end-to-end demo
customer TradingView demo
client pilot starter-kit validation
Docker Compose validation
TradingView gateway deployment validation
Docker image build
```

Ordinary CI did not contact SEC or Alpha Vantage.

## External network smoke status

```text
NOT RUN
```

No real external SEC / Alpha Vantage smoke PASS is claimed for this slice. The existing opt-in network path remains available for a separate explicit manual run.

## Blockers

No VS-04 code, contract or CI blocker remains.

A real external network smoke was intentionally not required for ordinary CI and was not simulated.

## Next integration action

Development Orchestrator should:

1. compare `TradeOps/vs04-configurable-public-demo` with current `main`;
2. confirm the diff remains limited to the public demo CLI/composer, its deterministic tests and this status file;
3. confirm no frozen contract or VS-02 historical-availability semantic changed;
4. if `main` advanced, synchronize according to `WORKSTREAM_PROTOCOL.md`;
5. run/verify exact synchronized-head CI if synchronization changes the branch;
6. integrate VS-04 if review remains green;
7. only after integration decide whether to start VS-05 / transcript or earnings-call ingestion.

Do not start VS-05 from this worker branch.
