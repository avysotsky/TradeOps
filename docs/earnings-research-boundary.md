# Earnings research boundary

WS-02 defines the v1 integration boundary between an external earnings/filings research service and TradeOps.

## Boundary

```text
SEC EDGAR filing metadata + structured XBRL facts
-> external acquisition service
-> SecStructuredFiling
-> deterministic accession/period normalization
-> SecFilingEarningsFacts
-> EarningsEvent v1 / EarningsSnapshot v1
-> deterministic research rule
-> ResearchDecision v1
-> portfolio/risk translation
-> execution
```

TradeOps does not crawl SEC, parse arbitrary HTML/transcripts, run an LLM over documents, persist a raw research corpus, or place orders directly from raw filing text.

## EarningsEvent v1

`EarningsEvent` is the consumer-side event contract usable by both a live research pipeline and historical replay.

It contains:

- broker-neutral instrument identity through `InstrumentReference`;
- stable `EventId`;
- `PublishedAt`: the earliest timestamp at which this research event is allowed to be consumed;
- `FiscalPeriod`;
- `EarningsSnapshot`;
- `ResearchSourceProvenance`.

For SEC v1, source identity is:

```text
Provider = SEC
SourceDocumentId = accession number
IssuerId = CIK:<CIK>
```

## EarningsSnapshot v1

The first snapshot contains:

- revenue;
- diluted EPS;
- net income;
- gross margin;
- operating margin;
- optional guidance direction;
- optional guidance revenue range;
- optional guidance diluted-EPS range.

Guidance is optional because standardized SEC XBRL does not guarantee issuer guidance. A later release/transcript provider can populate the same normalized field only after deterministic extraction/classification.

## SEC acceptance vs public availability

These timestamps have different meanings and must not be collapsed.

`AcceptedAt` is the SEC/EDGAR acceptance timestamp for the submission. It is retained as `ResearchSourceProvenance.SourceTimestamp`.

It is **not** treated as proof that the filing content was already publicly retrievable at that exact instant.

The SEC states that filings are often available on sec.gov 1–3 minutes after the EDGAR system timestamp, that lag can increase, and that SEC has no timestamp indicating exactly when filing content first becomes available on sec.gov.

Therefore the v1 availability rule is conservative:

```text
if acquisition service has a verified PubliclyAvailableAt:
    PublishedAt = PubliclyAvailableAt
else:
    PublishedAt = first observed RetrievedAt
```

Required invariant:

```text
AcceptedAt / SourceTimestamp <= PublishedAt <= RetrievedAt
ResearchDecision.GeneratedAt >= PublishedAt
```

A `PubliclyAvailableAt` supplied by the external acquisition service must be evidence-backed and cannot precede SEC acceptance or be later than the observation that retrieved the source.

For ordinary polling, using first successful retrieval as `PublishedAt` is deliberately conservative and prevents a backtest from acting during an unknown dissemination lag.

An after-hours filing therefore becomes eligible only at `PublishedAt`; market-session logic later decides the first tradable bar/order opportunity.

SEC reference:

```text
https://www.sec.gov/files/about/webmaster-faq.htm
Developers -> EDGAR lag/timestamps
```

## SEC structured-data MVP

The live acquisition service remains outside TradeOps.

Its minimum responsibilities are:

1. discover the filing and obtain CIK, accession, form and SEC acceptance timestamp;
2. retrieve SEC structured XBRL facts;
3. record first successful retrieval and, when independently established, verified public-availability time;
4. preserve source URI/raw-source evidence outside TradeOps;
5. emit `SecStructuredFiling`.

TradeOps then performs deterministic normalization.

The normalizer selects only facts matching:

- `us-gaap` namespace;
- exact filing accession;
- exact filing form;
- exact fiscal-period start/end;
- expected unit;
- no dimensional member.

This guards historical replay against later filings that repeat, revise or restate an older fiscal period: a fact from another accession is not eligible for the earlier event.

The v1 concept priority is intentionally small:

```text
Revenue:
  RevenueFromContractWithCustomerExcludingAssessedTax
  SalesRevenueNet
  Revenues

Diluted EPS:
  EarningsPerShareDiluted

Net income:
  NetIncomeLoss
  ProfitLoss

Gross profit:
  GrossProfit

Operating income:
  OperatingIncomeLoss
```

If more than one exact fact exists for a selected concept/accession/period/context, normalization fails instead of choosing silently.

## Provenance

Every normalized event carries:

- provider;
- source URI;
- source timestamp;
- retrieval timestamp;
- extraction method;
- source document identifier;
- issuer identifier.

For SEC:

```text
provider = SEC
sourceTimestamp = EDGAR AcceptedAt
sourceDocumentId = accession number
issuerId = CIK:<CIK>
extractionMethod = sec-filing-xbrl-v1
```

`PublishedAt` belongs to the event because it is the consumer eligibility boundary; it may be later than the provider's acceptance/source timestamp.

## Deterministic research rule

`DeterministicEarningsDecisionRule` compares a current event with a prior comparable event for the same symbol.

The default demo signals are:

- revenue growth versus the configured threshold;
- diluted EPS growth versus the configured threshold;
- operating-margin change versus the configured threshold.

The result is a broker-neutral `ResearchDecision v1` with `Buy`, `Sell`, or `NoAction`. It is not an executable order. WS-03 remains responsible for deterministic portfolio/rebalance translation before execution.

The rule is a reproducible integration fixture, not a profitability claim or production alpha model. An LLM may later extract or classify facts, but it must not bypass normalized facts/rules and emit executable orders directly.

## SEC-anchored fixtures

Tests use filing metadata and GAAP values anchored to real SEC filings for:

- Apple Q3 FY2026 — accession `0000320193-26-000020`, period ended 2026-06-27;
- Microsoft Q3 FY2026 — accession `0001193125-26-191507`, period ended 2026-03-31;
- NVIDIA Q2 FY2027 — accession `0001045810-26-000075`, period ended 2026-07-26.

Fixture monetary values are represented in SEC-style base units (USD and USD/shares), not display-table millions.

The fixture acceptance timestamp is source metadata. Test event creation intentionally defaults `PublishedAt` to a later simulated first-retrieval timestamp unless a verified `PubliclyAvailableAt` is explicitly supplied.

## Separate repository decision

Live SEC acquisition, HTTP policy/rate limiting, caching, filing discovery, raw payload persistence/evidence, public-availability observation and future transcript/LLM extraction belong in a separate Earnings Research service/repository.

TradeOps owns only the consumer-side contract, deterministic normalization, availability/provenance guards and translation into frozen `ResearchDecision v1`.
