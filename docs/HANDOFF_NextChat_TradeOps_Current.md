# TradeOps handoff — current state

## Source of truth

Repository: `avysotsky/TradeOps`

Default branch: `main`

Current main merge commit:

```text
b107bed05eb90ef0c481e5dd18ed258c01b10dd0
```

Do not reimplement earlier milestones.

## Stable product baseline

The current reusable core is intentionally feature-frozen unless a real client requirement or reproducible defect justifies code changes.

Supported execution stages:

```text
Mock
BybitTestnet
```

Current product boundary:

- execution/integration engineering;
- no strategy/alpha generation;
- no profitability guarantees;
- no Bybit mainnet / real-money execution;
- no second exchange;
- no HFT / ultra-low-latency claims.

## Security / evidence baseline

v1.3.2.1 protects the sensitive paid-pilot evidence read path with the independent operator credential.

The evidence exporter remains read-only and supports an authenticated evidence handoff.

## Client-facing commercial path

The repository now contains a complete pre-sale and delivery sequence:

```text
docs/paid-pilot-offer.md
-> docs/client-demo-script.md
-> docs/client-intake-questionnaire.md
-> docs/paid-pilot-scope-template.md
-> docs/client-pilot-runbook.md
-> docs/client-pilot-acceptance-criteria.md
-> docs/paid-pilot-evidence-handoff.md
```

### Paid Pilot Offer

Defines:
- who the pilot is for;
- included engineering scope;
- optional Bybit Testnet stage;
- inputs;
- deliverables;
- exclusions;
- security/commercial boundary.

### Client Demo Script

Defines a 10–15 minute prospect demo focused on:
- stable event identity;
- idempotent redelivery;
- conflict rejection;
- signal/order correlation;
- reconciliation;
- evidence;
- security boundaries;
- clear product exclusions.

### Client Intake Questionnaire

Classifies the prospect as:

```text
FIT
FIT WITH SMALL ADAPTATION
SEPARATE SCOPE REQUIRED
NOT A CURRENT FIT
```

The questionnaire must be completed before implementation.

### Paid Pilot Scope Template

Records the agreed:
- signal contract;
- symbols/action semantics;
- quantity semantics;
- eventId rule;
- deployment responsibility;
- included work;
- bounded adaptations;
- optional testnet stage;
- acceptance;
- deliverables;
- exclusions and change-control boundary.

## Delivery path

For a qualified client:

```text
qualify
-> agree scope
-> preflight
-> Mock acceptance
-> optional Bybit Testnet
-> audit / lifecycle / reconciliation
-> metrics / health
-> authenticated evidence
-> client sign-off
-> handoff
```

## Development rule

Do not add speculative backend features to make the repository look larger.

Resume core development only for:

1. a concrete client/pilot requirement;
2. a reproducible security/reliability defect;
3. a bounded delivery friction discovered while running the existing pilot.

Do not absorb separate-scope requests into the standard pilot.

## Recommended next work

The next work is commercial, not backend:

1. choose the actual pilot price/rate outside the public repository;
2. use the current offer + demo + intake package against real prospects;
3. evaluate new market requests against the intake classification;
4. only create a new code milestone when a real requirement reaches FIT WITH SMALL ADAPTATION or exposes a reproducible defect.

Before continuing in a new chat, read this file and inspect the current `main` HEAD and latest CI/PR state.
