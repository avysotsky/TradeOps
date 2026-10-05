# TradeOps handoff — current state after paid-pilot offer

## Source of truth

Repository: `avysotsky/TradeOps`

Default branch: `main`

Current main merge commit:

```text
f61480323b4dcb2c54ebaa164838cfbfb071ed85
```

Do not reimplement earlier milestones.

## Latest completed work

### v1.3.2.1 — Paid Pilot Read API Protection

PR #18 added operator-auth protection to the sensitive read path used by the paid-pilot evidence exporter.

Protected evidence reads include:

```text
GET /api/integrations/tradingview/operations/deliveries
GET /api/integrations/tradingview/operations/metrics
GET /api/integrations/tradingview/operations/health
GET /api/signals/{id}
GET /api/orders/local/{idOrClientOrderId}
GET /api/orders/local/{idOrClientOrderId}/history
GET /api/system/reconciliation/status
```

The PilotEvidence, TradingViewDemo and SignedWebhookDemo tools were updated to use the operator credential for those reads.

### v1.3.2.2 — Client Pilot Docs Alignment

PR #19 was docs-only. It aligned the acceptance criteria and client starter kit with the authenticated evidence flow.

### Client-facing Paid Pilot Offer

PR #20 added:

```text
docs/paid-pilot-offer.md
```

and linked it from the README.

The offer explains:
- who the pilot is for;
- the technical outcome;
- included scope;
- optional Bybit Testnet stage;
- required client inputs;
- acceptance criteria;
- handoff deliverables;
- explicit exclusions;
- security and commercial boundaries.

Pricing is intentionally not stored in the repository.

## Current commercial capability

TradeOps can now be presented as a paid execution/integration pilot for a client who already has trading rules or TradingView signals.

The delivery path is:

```text
client intake
-> client-facing pilot offer
-> starter kit / preflight
-> TradingView HTTPS gateway
-> authenticated + idempotent delivery
-> Mock execution
-> optional Bybit Testnet
-> persisted audit/correlation
-> lifecycle + reconciliation
-> metrics / health
-> authenticated evidence package
-> acceptance + handoff
```

## Product boundary

TradeOps is execution/integration engineering.

It does not provide:
- alpha;
- strategy design;
- profitable signals;
- return guarantees;
- Bybit mainnet / real-money execution;
- a second exchange;
- HFT / ultra-low-latency guarantees.

## Development rule

Do not add speculative backend features just to continue development.

Core development should resume only for:
1. a concrete paid-pilot/client requirement;
2. a reproducible reliability/security defect;
3. a small delivery friction discovered while using the current pilot path.

Commercial/client-onboarding material can continue without changing the execution engine.

## Recommended next small task

Create a reusable client intake questionnaire/form derived from Phase 0 of `docs/client-pilot-runbook.md`.

It should collect only the information needed to decide whether the client fits the current TradeOps pilot and to configure the existing system. Do not add product features while creating it.
