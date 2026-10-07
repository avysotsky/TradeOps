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

Current main synchronized before the correctness fix:

```text
main @ 80e736ab9f89c593c8b0b06ee4e655b6903d7dd6
review commit: Record VS-02 availability semantics review
branch synchronization merge: f2e4c21122db9700e83956af330355067f0c46a5
```

CI-validated correctness implementation HEAD:

```text
877c90c814ac47679f78fe1d056ddc55f4941b38
```

## Goal

Provide the first runnable client-facing TradeOps demo backed by public historical data rather than synthetic business inputs.

The bounded IBM flow remains:

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

This demonstrates software composition and historical replay. It does not assert investment alpha or future performance.

## Runnable tool

Added:

```text
tools/TradeOps.PublicResearchDemo
```

The project is part of `TradeOps.sln`, so normal CI restores and builds it. Ordinary CI never performs the external SEC / Alpha Vantage acquisition.

External fetch is fail-closed unless both runtime values are present:

```text
TRADEOPS_PUBLIC_DEMO_CONFIRM=RUN_PUBLIC_DATA_DEMO
TRADEOPS_SEC_USER_AGENT=<runtime contact-style SEC User-Agent>
```

No personal SEC User-Agent, credential, cookie, broker/account data or private API key is committed.

SEC requests are sequential and conservatively throttled.

## Exact runnable command

Linux/macOS first network acquisition:

```bash
TRADEOPS_PUBLIC_DEMO_CONFIRM=RUN_PUBLIC_DATA_DEMO \
TRADEOPS_SEC_USER_AGENT="TradeOpsPublicDemo/1.0 contact@example.com" \
dotnet run --project tools/TradeOps.PublicResearchDemo --configuration Release
```

PowerShell:

```powershell
$env:TRADEOPS_PUBLIC_DEMO_CONFIRM = "RUN_PUBLIC_DATA_DEMO"
$env:TRADEOPS_SEC_USER_AGENT = "TradeOpsPublicDemo/1.0 contact@example.com"
dotnet run --project tools/TradeOps.PublicResearchDemo --configuration Release
```

The address above is only a placeholder. A real local run must provide an appropriate runtime contact-style value.

After a verified cache exists, replay does not require external network access:

```bash
dotnet run --project tools/TradeOps.PublicResearchDemo --configuration Release
```

Optional CLI arguments:

```text
--refresh
--cache <path>
--json <path>
```

## Data sources

IBM SEC CIK:

```text
0000051143
```

Bounded SEC resources:

```text
https://data.sec.gov/submissions/CIK0000051143.json
https://data.sec.gov/api/xbrl/companyfacts/CIK0000051143.json
```

Bounded daily market-data source:

```text
Alpha Vantage
TIME_SERIES_DAILY
symbol=IBM
apikey=demo
```

No generic crawler or production market-data architecture is introduced.

## SEC exact-accession semantics

The tool discovers the latest two IBM `10-Q` filings required for one comparable earnings decision.

For each filing it retains:

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

Company facts are eligible only when accession and form match the selected filing exactly.

For a `10-Q`, the parser selects the bounded quarterly duration rather than a YTD duration when both are present. Facts from another accession are excluded. Ambiguous exact facts continue to fail closed inside the existing `SecStructuredFilingNormalizer`.

The runtime path remains:

```text
SecStructuredFiling
-> SecStructuredFilingNormalizer
-> SecFilingEarningsFacts
-> SecEarningsEventFactory
-> EarningsEvent
```

No runtime `EarningsEvent` is constructed from arbitrary business values.

## Corrected SEC historical availability / provenance semantics

The integration-review blocker is resolved.

The three timestamps now have separate meanings.

### SEC AcceptedAt

```text
ResearchSourceProvenance.SourceTimestamp = SEC AcceptedAt
```

`AcceptedAt` is the SEC provider/source acceptance timestamp.

It is **not** treated as an exact market-publication or dissemination timestamp.

### HistoricalPublicAvailabilityAt

VS-02 adds a bounded tooling policy:

```text
SecHistoricalAvailabilityPolicy
```

For historical replay it deliberately defines:

```text
HistoricalPublicAvailabilityAt =
22:00 America/New_York
on the official SEC FilingDate
```

This timestamp is passed through:

```text
SecStructuredFiling.PubliclyAvailableAt
-> SecEarningsEventFactory
-> EarningsEvent.PublishedAt
```

It is a **conservative replay availability boundary**, not a claim about the exact time at which SEC first disseminated the filing.

The New York time zone is resolved per date, including DST.

If the derived conservative boundary would precede the filing's `AcceptedAt`, the policy fails closed rather than violating causal ordering.

### Actual snapshot RetrievedAt

The raw network/cache snapshot retains the real acquisition timestamp:

```text
PublicDemoRawSnapshot.RetrievedAt
```

That exact timestamp is passed to:

```text
SecEarningsEventFactory.Create(facts, snapshot.RetrievedAt)
```

Therefore:

```text
ResearchSourceProvenance.RetrievedAt =
actual raw public-data snapshot acquisition time
```

It is no longer rewritten to historical `AcceptedAt`.

Required invariant:

```text
SourceTimestamp <= PublishedAt <= RetrievedAt
```

For the bounded SEC demo this means:

```text
SourceTimestamp = SEC AcceptedAt
PublishedAt     = conservative HistoricalPublicAvailabilityAt
RetrievedAt     = actual snapshot.RetrievedAt
```

## Historical decision timing mode

The existing VS-01 default behavior remains unchanged.

`ResearchToRebalanceDemoRequest` now has an explicit backward-compatible timing option:

```text
ResearchDecisionTimingMode.ObservedRetrieval
ResearchDecisionTimingMode.HistoricalPublishedAvailability
```

Default:

```text
ObservedRetrieval

GeneratedAt =
max(EarningsEvent.PublishedAt,
    EarningsEvent.Provenance.RetrievedAt)
```

That preserves the original VS-01 observation behavior for all existing callers that do not specify a mode.

VS-02 explicitly selects:

```text
HistoricalPublishedAvailability

GeneratedAt =
EarningsEvent.PublishedAt
```

This mode is only for historical reconstruction/replay. It does not change `ResearchSourceProvenance.RetrievedAt`, does not alter `DeterministicEarningsDecisionRule`, and does not relax the frozen `ResearchDecision` semantics.

The backtester still uses the normal causal execution boundary, so an after-hours historical availability timestamp cannot execute against an earlier daily bar. The IBM fixture verifies execution only at the next eligible regular-session open.

## Market-data timing

Alpha Vantage provider trading dates are converted to existing `MarketDataBar` values with:

```text
Period = Daily
OpenTime  = 09:30 America/New_York
CloseTime = 16:00 America/New_York
```

DST is resolved per trading date. No fixed UTC offset is used for the entire year.

A separate holiday calendar is unnecessary in this bounded slice because the provider already supplies the trading dates.

## Local snapshot / cache

Default location:

```text
.tradeops/public-demo-cache/
```

The entire `.tradeops/` directory is gitignored.

The manifest retains:

```text
provider
source URI
actual snapshot RetrievedAt
symbol
CIK where applicable
selected accession numbers
market-data date range
relative payload path
SHA-256 for every raw payload
```

Every cached payload is checksum-verified before replay. A mismatch fails closed.

## Demo portfolio

No IBKR account or broker data is used.

The explicit local fixture remains:

```text
Initial NAV: 10,000 USD
IBM quantity: 10 shares
CurrentReferencePrice: latest retrieved IBM daily close
Cash: 10,000 USD - current IBM notional
```

It is labelled:

```text
demo portfolio snapshot
```

The resulting `RebalanceOrderIntent` remains broker-neutral and requires downstream risk approval.

## Client-readable result

Console and JSON output contain:

```text
Instrument
real SEC EventId/accession
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

Default JSON artifact:

```text
.tradeops/public-demo-cache/ibm-result.json
```

The output is explicitly described as a software-pipeline demonstration and not as evidence of future returns.

## Public/shared contracts changed

Frozen/public contracts changed:

```text
none
```

Unchanged frozen contracts include:

```text
ResearchSourceProvenance
EarningsEvent
ResearchDecision
MarketDataBar
PortfolioSnapshot
RebalancePlan
RebalanceOrderIntent
InstrumentReference
BacktestPerformanceMetrics
```

The only application-layer production-source change is the explicitly authorized backward-compatible extension to `ResearchToRebalanceDemoService` / `ResearchToRebalanceDemoRequest` for decision timing mode.

No change was made to:

```text
DeterministicEarningsDecisionRule
PortfolioRebalancePlanner
EventDrivenBacktester
ResearchDecision semantics
SetTargetWeight semantics
```

## Tests

The corrected suite covers:

```text
SEC submissions parsing
exact accession/form matching
quarter-duration selection instead of YTD
SEC companyfacts -> SecStructuredFiling
SourceTimestamp == AcceptedAt
PublishedAt is not automatically AcceptedAt
PublishedAt == SecHistoricalAvailabilityPolicy result
RetrievedAt == actual snapshot.RetrievedAt
SourceTimestamp <= PublishedAt <= RetrievedAt
default VS-01 timing remains ObservedRetrieval
historical timing generates ResearchDecision at PublishedAt
historical timing does not rewrite provenance RetrievedAt
after-hours historical availability fills only at next eligible daily open
no execution before GeneratedAt
New York daily-session DST conversion
snapshot SHA-256 verification
network gate fail-closed behavior
full public-data composition through existing VS-01/backtester/planner
```

Validated result:

```text
Total tests: 352
Passed: 352
Failed: 0
```

## Full CI

CI-validated correctness implementation:

```text
HEAD: 877c90c814ac47679f78fe1d056ddc55f4941b38
workflow: build
run number: 654
run id: 37593688233
conclusion: SUCCESS
```

Validated stages:

```text
restore
build
352/352 unit tests
API + PostgreSQL smoke
signed webhook end-to-end demo
customer TradingView demo
client pilot starter-kit validation
Docker Compose validation
TradingView gateway deployment validation
Docker image build
```

External SEC / Alpha Vantage acquisition remains deliberately outside ordinary CI.

## Public-data network smoke

```text
NOT RUN
```

The worker executable environment still does not have the runtime prerequisites for a real compiled-tool public fetch, including an explicit SEC contact-style User-Agent. No network PASS is simulated.

## Changed files versus synchronized main

```text
.gitignore
TradeOps.sln
docs/orchestration/VS02_PUBLIC_DATA_DEMO.md
src/TradeOps.Application/Services/ResearchToRebalanceDemoService.cs
tests/TradeOps.UnitTests/PublicResearchDemoTests.cs
tests/TradeOps.UnitTests/ResearchToRebalanceDemoTests.cs
tests/TradeOps.UnitTests/TradeOps.UnitTests.csproj
tools/TradeOps.PublicResearchDemo/Program.cs
tools/TradeOps.PublicResearchDemo/PublicDataInfrastructure.cs
tools/TradeOps.PublicResearchDemo/PublicResearchDemo.cs
tools/TradeOps.PublicResearchDemo/TradeOps.PublicResearchDemo.csproj
```

## Blockers

No code, semantic, test or CI blocker remains for integration review.

The real opt-in external public-data smoke remains an external/manual validation item and is not required by ordinary CI.

## Worker handoff

```text
State: READY_FOR_INTEGRATION
Assigned baseline: cfa5c315772465a839b1a095675afd009e7e0dad
Synchronized main: 80e736ab9f89c593c8b0b06ee4e655b6903d7dd6
Synchronization merge: f2e4c21122db9700e83956af330355067f0c46a5
Validated correctness implementation HEAD: 877c90c814ac47679f78fe1d056ddc55f4941b38
Public/shared frozen contracts changed: none
Tests: 352/352 passed
Full CI: build #654 / run 37593688233 / SUCCESS
Public-data network smoke: NOT RUN; no PASS simulated
Blockers: none for integration review
Next integration action: Development Orchestrator reviews the corrected bounded diff and integrates VS-02 if review remains green.
```

Do not begin transcript ingestion, IBKR mutation, strategy optimization or another slice from this worker without a new Development Orchestrator decision.
