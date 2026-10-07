# VS-02 — Public-Data Runnable Demo

State: READY_FOR_INTEGRATION

Repository / branch:

```text
avysotsky/TradeOps
TradeOps/vs02-public-data-runnable-demo
```

Assigned baseline:

```text
cfa5c315772465a839b1a095675afd009e7e0dad
```

CI-validated implementation HEAD:

```text
a3bbbef5f0efde5e51e5222578e1b167ef01eccf
```

## Goal

Provide the first runnable client-facing TradeOps demo backed by public historical data rather than synthetic business inputs.

The bounded IBM flow is:

```text
SEC EDGAR submissions + XBRL companyfacts
-> SecStructuredFiling
-> SecStructuredFilingNormalizer
-> SecEarningsEventFactory
-> EarningsEvent

Alpha Vantage IBM TIME_SERIES_DAILY
-> MarketDataBar(Daily)

EarningsEvent[] + MarketDataBar[]
-> existing ResearchToRebalanceDemoService
-> existing EventDrivenBacktester
-> existing PortfolioRebalancePlanner
-> client-readable console + JSON result
```

The slice demonstrates software composition and historical replay. It does not assert investment alpha or future performance.

## Runnable tool

Added:

```text
tools/TradeOps.PublicResearchDemo
```

The tool is part of `TradeOps.sln`, so ordinary CI restores and builds it.

External acquisition is not executed by ordinary CI.

### Network gate

A network fetch fails closed unless:

```text
TRADEOPS_PUBLIC_DEMO_CONFIRM=RUN_PUBLIC_DATA_DEMO
TRADEOPS_SEC_USER_AGENT=<runtime contact-style SEC User-Agent>
```

No SEC User-Agent, contact identity, credential, cookie, account data or private API key is committed.

SEC requests are sequential and throttled with a 350 ms minimum interval between SEC requests, substantially below the SEC 10 requests/second maximum.

### First network run

Linux/macOS example:

```bash
TRADEOPS_PUBLIC_DEMO_CONFIRM=RUN_PUBLIC_DATA_DEMO \
TRADEOPS_SEC_USER_AGENT="TradeOpsPublicDemo/1.0 contact@example.com" \
dotnet run --project tools/TradeOps.PublicResearchDemo --configuration Release
```

PowerShell example:

```powershell
$env:TRADEOPS_PUBLIC_DEMO_CONFIRM = "RUN_PUBLIC_DATA_DEMO"
$env:TRADEOPS_SEC_USER_AGENT = "TradeOpsPublicDemo/1.0 contact@example.com"
dotnet run --project tools/TradeOps.PublicResearchDemo --configuration Release
```

The address above is a documentation placeholder only. Supply an appropriate runtime contact value locally.

To force a new public snapshot after a cache already exists:

```bash
dotnet run --project tools/TradeOps.PublicResearchDemo --configuration Release -- --refresh
```

The same two runtime environment values remain required for a refresh.

### Cached replay

After a successful first acquisition, the same command can run from the verified local snapshot without external network access:

```bash
dotnet run --project tools/TradeOps.PublicResearchDemo --configuration Release
```

Optional arguments:

```text
--cache <path>
--json <path>
--refresh
```

## SEC acquisition and exact-accession semantics

IBM CIK:

```text
0000051143
```

The bounded tool downloads only:

```text
https://data.sec.gov/submissions/CIK0000051143.json
https://data.sec.gov/api/xbrl/companyfacts/CIK0000051143.json
```

It discovers the latest two IBM `10-Q` filings from submissions metadata so that the latest event has a prior comparable event.

For each filing, the parser retains:

```text
CIK
accession number
form type
report date
filing date
AcceptedAt
primary document
SEC Archives source URI
```

XBRL company facts are eligible only when accession and form match the selected filing exactly.

For a `10-Q`, the parser selects the bounded quarterly duration rather than a YTD duration when both are present. Facts from another accession are excluded. Conflicting exact facts remain visible to the existing `SecStructuredFilingNormalizer`, which fails closed on ambiguity.

The selected values still pass through the existing production path:

```text
SecStructuredFiling
-> SecStructuredFilingNormalizer
-> SecFilingEarningsFacts
-> SecEarningsEventFactory
-> EarningsEvent
```

No `EarningsEvent` is constructed from arbitrary business values in the runtime demo.

## Historical availability / provenance

`AcceptedAt` comes from SEC submissions filing metadata.

For this bounded historical reconstruction, the tool uses that SEC acceptance timestamp as the historical availability/observation boundary supplied to the existing factory. It never substitutes the current backfill-download time into an old event.

The actual time at which the raw public payloads are downloaded is retained separately as `RetrievedAt` in the cache manifest.

This separation is intentional:

```text
historical event availability
    -> SEC filing metadata boundary

current raw snapshot acquisition
    -> cache-manifest RetrievedAt
```

It preserves the existing VS-01 rule:

```text
ResearchDecision.GeneratedAt =
max(EarningsEvent.PublishedAt, ResearchSourceProvenance.RetrievedAt)
```

and prevents a 2026 backfill download timestamp from moving an older decision into the future and invalidating historical replay.

The implementation does not change production `PublishedAt`, `GeneratedAt`, provenance or anti-look-ahead semantics.

## Market data

The bounded public market-data source is:

```text
Alpha Vantage
TIME_SERIES_DAILY
symbol=IBM
apikey=demo
```

This is a demo acquisition adapter only; it is not a production market-data architecture.

Provider trading dates are converted to existing `MarketDataBar` with:

```text
Period = Daily
OpenTime  = 09:30 America/New_York
CloseTime = 16:00 America/New_York
```

The conversion uses the New York time-zone rules so DST is handled per date. It does not hardcode one UTC offset for the whole year.

No separate holiday calendar is introduced because the provider supplies the trading dates.

## Local snapshot / cache

Default cache:

```text
.tradeops/public-demo-cache/
```

The entire `.tradeops/` directory is gitignored.

The cache stores only the bounded raw public payloads plus a manifest. It is not a third-party data corpus.

Manifest provenance includes:

```text
provider
source URI
actual snapshot RetrievedAt
symbol
CIK where applicable
selected accession numbers
market-data date range
relative payload path
SHA-256 for every cached payload
```

Every cached payload is checksum-verified before replay. A modified payload fails closed.

## Client-readable result

Console output and JSON include:

```text
Instrument
real SEC EventId
real accession
FiscalPeriod
PublishedAt
Assessment
TargetWeight

Backtest:
period
event count
initial equity
final equity
TotalReturn
CAGR
MaxDrawdown
Sharpe
Sortino
Turnover

Current rebalance example:
current portfolio weight
proposed target weight
side
quantity
estimated notional
status
```

The default JSON artifact is:

```text
.tradeops/public-demo-cache/ibm-result.json
```

The output explicitly labels the current holdings as:

```text
demo portfolio snapshot
```

and includes a software-demo / no-forecast disclaimer.

## Demo portfolio fixture

There is no broker or IBKR dependency in VS-02.

The current-portfolio example is deterministic local input:

```text
Initial NAV: 10,000 USD
IBM quantity: 10 shares
CurrentReferencePrice: latest retrieved IBM daily close
Cash: 10,000 USD - current IBM notional
```

If that fixture would imply non-positive cash at the retrieved reference price, the tool fails closed.

## Existing production pipeline reused

VS-02 calls the integrated:

```text
ResearchToRebalanceDemoService
DeterministicEarningsDecisionRule
EarningsTargetWeightPolicy
EventDrivenBacktester
PortfolioRebalancePlanner
```

The production decision remains:

```text
ResearchDecisionAction.SetTargetWeight
```

The current rebalance remains a `RebalanceOrderIntent` requiring downstream risk approval; it is not a broker order.

## Public/shared contracts changed

None.

VS-02 does not modify:

```text
InstrumentReference
ResearchDecision
EarningsEvent
PortfolioSnapshot
RebalancePlan
RebalanceOrderIntent
MarketDataBar
BacktestPerformanceMetrics
PublishedAt semantics
GeneratedAt semantics
PortfolioRebalancePlanner semantics
EventDrivenBacktester semantics
```

No LLM, transcript parsing, generic web scraping, IBKR order placement/mutation, multi-asset backtesting or optimization is added.

## Deterministic tests

Added `PublicResearchDemoTests` coverage for:

```text
SEC submissions parsing
exact accession / form selection
quarter-duration selection instead of YTD for 10-Q
SEC companyfacts -> SecStructuredFiling
AcceptedAt and source-provenance projection
exclusion of facts from another accession
Alpha Vantage JSON -> MarketDataBar(Daily)
09:30 / 16:00 New York timestamps in both standard time and DST
cache manifest + SHA-256 verification
checksum mismatch fail-closed behavior
network opt-in fail-closed behavior
full public-data composition -> existing VS-01 pipeline
ResearchDecision(Action = SetTargetWeight)
existing backtester / rebalance planner invocation
```

Validated full solution:

```text
Total tests: 349
Passed: 349
Failed: 0
```

## Full CI

CI-validated implementation HEAD:

```text
a3bbbef5f0efde5e51e5222578e1b167ef01eccf
```

GitHub Actions:

```text
workflow: build
run number: 650
run id: 37591360446
conclusion: SUCCESS
```

Validated stages:

```text
restore
build
349/349 tests
API + PostgreSQL smoke
signed webhook end-to-end demo
customer TradingView demo
client pilot starter-kit validation
Docker Compose validation
TradingView gateway deployment validation
Docker image build
```

The external SEC / Alpha Vantage acquisition is deliberately not part of ordinary CI.

## Public-data network smoke result

```text
NOT RUN
```

Reason:

The worker's executable/container environment cannot resolve external hosts, and no runtime SEC contact-style User-Agent was supplied to that environment. The GitHub-connected/web retrieval path is not the same runtime as the compiled CLI and therefore was not used to claim a tool smoke PASS.

No public-data PASS was simulated.

The exact manual smoke is the first-network-run command documented above. A successful run must produce real IBM accessions, a verified cache manifest and the JSON artifact; any missing gate/User-Agent, provider error, malformed payload, checksum mismatch, missing comparable filing or missing next daily bar fails the tool.

## Changed files versus assigned baseline

```text
.gitignore
TradeOps.sln
tests/TradeOps.UnitTests/PublicResearchDemoTests.cs
tests/TradeOps.UnitTests/TradeOps.UnitTests.csproj
tools/TradeOps.PublicResearchDemo/Program.cs
tools/TradeOps.PublicResearchDemo/PublicDataInfrastructure.cs
tools/TradeOps.PublicResearchDemo/PublicResearchDemo.cs
tools/TradeOps.PublicResearchDemo/TradeOps.PublicResearchDemo.csproj
docs/orchestration/VS02_PUBLIC_DATA_DEMO.md
```

No production contract/source file under `src/TradeOps.Application` was changed by VS-02.

## Blockers

No code, test or CI blocker remains for integration review.

The only unvalidated item is the real opt-in external public-data smoke, because the worker execution environment cannot execute the compiled tool against public hosts and no runtime SEC User-Agent was available.

This does not make ordinary CI dependent on SEC or Alpha Vantage.

## Worker handoff

```text
State: READY_FOR_INTEGRATION
Baseline: cfa5c315772465a839b1a095675afd009e7e0dad
CI-validated implementation HEAD: a3bbbef5f0efde5e51e5222578e1b167ef01eccf
Public/shared contracts changed: none
Tests: 349/349 passed
Full CI: build #650 / run 37591360446 / SUCCESS
Public-data smoke: NOT RUN; not simulated
Blocker: real network smoke remains externally runnable when an explicit SEC User-Agent is supplied
Next integration action: Development Orchestrator reviews the bounded diff, reconciles against current main if needed, and integrates VS-02 if review remains green.
```

Do not start transcript ingestion, IBKR mutation, strategy optimization or another research slice from this worker without a new Development Orchestrator decision.
