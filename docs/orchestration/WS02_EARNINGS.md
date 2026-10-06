# WS-02 — Earnings Intelligence / Research Boundary

State: INTEGRATED

Repository / branch:

```text
avysotsky/TradeOps
TradeOps/ws02-earnings-intelligence-contract
```

Synchronized base:

```text
main @ 902072924bbfd5f35a7c506192407915deacd29c
sync commit: e3fe13c42e3feadf3c463ccaa88cbb78813fa500
```

Current validated implementation HEAD:

```text
784e00470a101928ba29750c970f25653d8472f5
```

## Goal

Prepare the consumer-side Earnings Intelligence MVP for:

```text
company/watchlist
-> earnings / filings / company data
-> structured facts
-> deterministic earnings assessment
-> explicit target-weight policy
-> ResearchDecision v1 (SetTargetWeight)
-> PortfolioRebalancePlanner
-> RebalancePlan
```

without turning TradeOps into a crawler, transcript system, generic document parser or LLM application.

## Completed

### Main synchronization

WS-02 was rebased/synchronized onto current TradeOps main after WS-03 integration.

The branch therefore contains the integrated portfolio-planning boundary:

```text
ResearchDecision(Action = SetTargetWeight)
-> PortfolioRebalancePlanner
-> RebalancePlan
-> RebalanceOrderIntent
```

No WS-03-owned file was modified by this slice.

### Earnings research boundary

Existing WS-02 contracts remain:

```text
EarningsEvent v1
EarningsSnapshot v1
EarningsGuidanceSnapshot
ResearchSourceProvenance
SecStructuredFiling / SecStructuredFact
SecFilingEarningsFacts
```

The SEC normalization, provenance, conservative PublishedAt semantics and anti-look-ahead rules remain unchanged.

### Semantic mismatch fixed

The previous deterministic earnings rule emitted:

```text
Buy / Sell / NoAction
```

That was incompatible with integrated WS-03, whose RebalancePlan v1 accepts only:

```text
ResearchDecisionAction.SetTargetWeight
```

The rule now separates two layers.

Research assessment:

```text
structured earnings facts
-> revenue growth / diluted EPS growth / operating-margin delta
-> score
-> EarningsAssessment.Positive | Neutral | Negative
```

Explicit integration policy:

```text
EarningsTargetWeightPolicy
  PositiveTargetWeight
  NeutralTargetWeight
  NegativeTargetWeight
```

Final portfolio-facing output:

```text
ResearchDecision
Action = SetTargetWeight
TargetWeight = configured weight for the assessment
```

No hidden portfolio strategy is hardcoded. The caller must provide the target-weight policy explicitly.

### Long-only v1

Every configured target weight is validated as:

```text
0 <= TargetWeight <= 1
```

and with the same maximum decimal scale accepted by frozen ResearchDecision v1.

The demo tests use an explicit policy:

```text
Positive -> 0.04
Neutral  -> 0.02
Negative -> 0.00
```

Those values are test/integration policy only and are not asserted to be profitable or production-optimal.

### Determinism

Decision identity now includes both:

- earnings comparison settings;
- explicit target-weight policy.

Therefore changing a threshold or target mapping cannot silently produce a different portfolio decision under the same deterministic DecisionId.

Metadata records:

- assessment;
- score;
- comparable signal count;
- KPI deltas/growth;
- source/provenance timestamps;
- positive/neutral/negative target-weight settings.

## Shared contracts changed

No.

Frozen shared contracts remain unchanged:

```text
InstrumentReference v1
ResearchDecision v1
ResearchDecisionAction v1
ResearchDecisionValidationResult v1
```

Integrated WS-03 contracts were also not changed:

```text
PortfolioSnapshot
RebalanceConstraints
TargetPosition
RebalancePlan
RebalanceOrderIntent
PortfolioRebalancePlanner
```

## Tests

WS-02 coverage includes all prior SEC/provenance/look-ahead tests plus:

- positive earnings assessment -> configured positive target weight;
- neutral assessment -> configured neutral target weight;
- negative assessment -> configured non-negative long-only target;
- invalid target-weight policy outside [0, 1] rejected;
- final decision uses `ResearchDecisionAction.SetTargetWeight`;
- final decision passes frozen `ResearchDecisionValidator`;
- end-to-end application-layer test:
  `EarningsEvent -> deterministic assessment -> ResearchDecision(SetTargetWeight) -> PortfolioRebalancePlanner -> RebalancePlanStatus.Ready`;
- planner produces a non-null `RebalanceOrderIntent` for the positive demo case;
- deterministic DecisionId/output for identical events, rules and policy.

## CI

Validated implementation:

```text
HEAD: 784e00470a101928ba29750c970f25653d8472f5
GitHub Actions run: 37514197922
Conclusion: success
```

The successful full workflow includes:

- restore/build;
- full unit suite;
- API + PostgreSQL smoke;
- signed webhook E2E;
- customer TradingView demo;
- client-pilot starter-kit validation;
- Docker Compose/gateway validation;
- API/Worker Docker image builds.

## Blockers

The semantic WS-02 -> WS-03 contract blocker is resolved.

No blocker remains for Development Orchestrator integration review of this bounded TradeOps slice.

Live SEC acquisition remains a separate bounded-context task and should be implemented in a separate Earnings Research repository/service rather than inside TradeOps.

## Next integration action

Development Orchestrator should:

1. repeat integration review against current `main`;
2. verify the WS-02 diff is limited to earnings/research-owned files;
3. verify the new end-to-end planner compatibility test;
4. integrate the bounded WS-02 slice if review remains green;
5. preserve the explicit target-weight-policy requirement for any future research provider;
6. reuse the same `SetTargetWeight -> PortfolioRebalancePlanner` boundary in WS-04 historical replay.

## Critical correctness rule

Historical evaluation must use only information known to be available by `PublishedAt`.

Target-weight mapping is deterministic configuration, not inferred alpha and not an executable-order bypass.


## Integration record

Integrated by Development Orchestrator:

```text
PR #51
worker HEAD: 07ce98c6481b426631707bc91b22143b14b7326e
merge commit: c739d3b9645e02cb32795ceb35d2436dc6bc057b
current-head CI before merge: 37514678283 — success
```

The following semantics are now downstream integration invariants unless explicitly changed by Development Orchestrator:

```text
EarningsEvent.PublishedAt
ResearchSourceProvenance.SourceTimestamp / RetrievedAt
exact-accession SEC fact selection
anti-look-ahead availability rules
explicit EarningsTargetWeightPolicy
ResearchDecision(Action = SetTargetWeight)
```

WS-04 must reuse these semantics for historical replay rather than define a second event-time or decision contract.
