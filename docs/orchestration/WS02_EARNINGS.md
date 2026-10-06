# WS-02 — Earnings Intelligence / Research Boundary

State: READY_FOR_INTEGRATION

Repository / branch:

```text
avysotsky/TradeOps
TradeOps/ws02-earnings-intelligence-contract
```

TradeOps base:

```text
main @ e4466fecab999d83b7b9f9b5353514125b793251
```

Validated implementation HEAD:

```text
25b46806bf9b4f5b57ff03d106e2633ef7e97406
```

## Goal

Prepare the consumer-side Earnings Intelligence MVP for:

```text
company/watchlist
-> earnings / filings / company data
-> structured facts
-> deterministic analysis/rules
-> ResearchDecision v1
-> TradeOps
```

without turning TradeOps into a crawler, document parser, transcript system or LLM application.

## Completed

### Contract

Implemented:

```text
EarningsEvent v1
EarningsSnapshot v1
EarningsGuidanceSnapshot
ResearchSourceProvenance
SecStructuredFiling / SecStructuredFact
SecFilingEarningsFacts
```

The event/snapshot boundary carries:

- symbol via broker-neutral `InstrumentReference`;
- stable eventId;
- publication/consumer-availability timestamp;
- fiscal period;
- source provider/URI/document identity;
- revenue;
- diluted EPS;
- net income;
- gross margin;
- operating margin;
- optional guidance direction/ranges;
- provenance.

### SEC deterministic normalization

Implemented:

```text
SecStructuredFiling
-> SecStructuredFilingNormalizer
-> SecFilingEarningsFacts
-> SecEarningsEventFactory
-> EarningsEvent v1
```

Fact selection requires:

- `us-gaap`;
- exact accession;
- exact form;
- exact fiscal-period start/end;
- expected unit;
- consolidated/non-dimensional context.

Later-accession facts are ignored for the earlier event. Ambiguous exact facts fail closed.

### Availability / look-ahead semantics

SEC `AcceptedAt` is retained as provider/source timestamp, but is **not** treated as exact public dissemination time.

SEC documents that sec.gov availability can lag EDGAR acceptance, commonly by 1–3 minutes, with no exact sec.gov first-availability timestamp.

Therefore:

```text
SourceTimestamp = SEC AcceptedAt

if verified PubliclyAvailableAt exists:
    PublishedAt = PubliclyAvailableAt
else:
    PublishedAt = first successful RetrievedAt
```

Enforced invariants:

```text
SourceTimestamp <= PublishedAt <= RetrievedAt
ResearchDecision.GeneratedAt >= PublishedAt
```

This makes historical replay conservative when exact dissemination time is unknown and prevents after-hours decisions from being timestamped before observed availability.

### Structured research -> frozen ResearchDecision v1

`DeterministicEarningsDecisionRule` compares current/prior structured events using:

- revenue growth;
- diluted-EPS growth;
- operating-margin delta.

It emits validated existing `ResearchDecision v1`.

It does not create executable orders. WS-03 owns portfolio/rebalance translation.

The rule is an integration fixture only, not a profitability/alpha claim.

### Fixtures

Added SEC-anchored AAPL/MSFT/NVDA fixtures with real filing identities and realistic SEC base-unit facts:

```text
AAPL FY2026 Q3
accession 0000320193-26-000020
period end 2026-06-27

MSFT FY2026 Q3
accession 0001193125-26-191507
period end 2026-03-31

NVDA FY2027 Q2
accession 0001045810-26-000075
period end 2026-07-26
```

## Shared contracts changed

No.

Frozen contracts remain unchanged:

```text
InstrumentReference v1
ResearchDecision v1
ResearchDecisionAction v1
ResearchDecisionValidationResult v1
```

## Tests

WS-02 coverage now includes:

- exact accession selection;
- exact fiscal-period selection;
- consolidated/non-dimensional fact selection;
- later-accession fact exclusion;
- ambiguous exact XBRL fact rejection;
- SEC URI validation;
- SEC event/KPI normalization;
- deterministic margin calculation;
- retrieval-before-SEC-acceptance rejection;
- conservative fallback `PublishedAt = RetrievedAt`;
- verified `PubliclyAvailableAt` propagation;
- public availability before acceptance rejection;
- public availability after retrieval rejection;
- decision-before-`PublishedAt` rejection;
- invalid `PublishedAt < SourceTimestamp` rejection;
- deterministic research decision identity/output;
- positive/negative deterministic decision examples.

## CI

Validated implementation:

```text
HEAD: 25b46806bf9b4f5b57ff03d106e2633ef7e97406
GitHub Actions run: 37511648743
Conclusion: success
```

Successful workflow includes:

- restore/build;
- full unit suite;
- API + PostgreSQL smoke;
- signed webhook E2E;
- customer TradingView demo;
- client-pilot starter-kit validation;
- Docker Compose/gateway validation;
- API/Worker image builds.

A prior intermediate timestamp-semantics commit `00ae9051020931469aeadcfd6f8bf27245bd87f7` failed one WS-02 assertion because that assertion still encoded the superseded `PublishedAt == AcceptedAt` assumption. The corrected tests are included in the successful HEAD above.

## Blockers

No blocker for integration of this TradeOps consumer-side slice.

**Live production SEC acquisition is blocked by bounded-context ownership:** it should not be implemented inside TradeOps.

Development Orchestrator should create/assign a separate Earnings Research repository/service for:

- filing discovery;
- SEC submissions/metadata access;
- structured XBRL retrieval;
- polling/RSS/PDS or other availability observation;
- HTTP rate limiting/caching;
- raw-source persistence/evidence;
- future release/transcript ingestion;
- optional LLM fact extraction/classification.

Audio/transcript/LLM work is explicitly later and is not required for the SEC MVP.

## Next integration action

Development Orchestrator should:

1. compare this branch with current `main`;
2. verify no shared-contract/file conflict with WS-03;
3. merge this bounded consumer-side slice when integration order permits;
4. freeze `EarningsEvent v1` availability/provenance semantics as an input candidate for WS-04;
5. create/assign the separate Earnings Research repository;
6. implement live SEC acquisition there and output `SecStructuredFiling` to this boundary.

## Critical correctness rule

Historical evaluation must use only information known to be available by `PublishedAt`.

Never backdate a filing to SEC acceptance when actual dissemination/observation occurred later. Never use later accessions, later restatements/revisions or post-event market data as though they were known at the earlier event.
