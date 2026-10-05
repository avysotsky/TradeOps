# TradeOps handoff — after v1.3.2.0

## Source of truth

Repository: `avysotsky/TradeOps`

Default branch: `main`

Current `main` HEAD:

```text
d41a56e76996eca3e9db824b5453c57fbeb720ab
```

This is the merge commit for PR #17:

```text
TradeOps v1.3.2.0 — Paid Pilot Evidence & Handoff Package
```

PR #17 branch head:

```text
a5342db714497e815334144fdce0ac34c7872ef7
```

GitHub Actions run #198 for that head completed successfully.

Do not reimplement earlier milestones.

## Completed commercial/pilot path

After v1.1.3.1 the repository advanced through:

- v1.1.4.0 Secure Signal Ingress
- v1.1.4.1 Operator API Protection
- v1.1.4.2 Webhook Replay Protection
- v1.1.5.0 HMAC Signed Webhook Contract
- v1.1.6.0 Signed Webhook E2E Demo
- v1.1.7.0 Webhook Request Audit & Correlation
- v1.2.0.0 TradingView Webhook Adapter
- v1.2.0.1 Trusted TradingView Gateway Deployment
- v1.2.1.0 TradingView Delivery Audit & Operations
- v1.2.1.1 TradingView Delivery Health & Alerting
- v1.3.0.0 Customer TradingView Demo Package
- v1.3.1.0 Client Integration Starter Kit
- v1.3.2.0 Paid Pilot Evidence & Handoff Package

v1.3.2.0 adds a read-only evidence exporter that generates `pilot-evidence.json` and `pilot-evidence.md` for an approved TradingView event and validates delivery outcomes, eventId -> SignalId -> ClientOrderId correlation, lifecycle, reconciliation, metrics and health.

## Current scope constraints

Keep these constraints unless a future milestone explicitly changes them:

- Mock / Bybit Testnet only.
- No Bybit mainnet or real-money execution.
- No second exchange.
- No alpha/strategy generation or profitability claims.
- No HFT/low-latency claims.
- No unrelated SaaS billing/multitenancy, React dashboard or Kubernetes work.

## Current security gap

The paid-pilot evidence path reads persisted operational data from read-only endpoints. The existing operator API key currently protects mutating operator actions, while the relevant GET endpoints remain open.

For a client-facing pilot this is an unnecessary exposure of signal/order/audit identifiers and operational state.

## Next milestone

```text
v1.3.2.1 — Paid Pilot Read API Protection
```

Work in small slices to avoid long-running chat/tool steps.

First slice:

1. Require the existing operator API key for TradingView operational GET endpoints:
   - deliveries
   - metrics
   - health
2. Make the pilot evidence exporter send the configured operator key when present.
3. Update focused integration tests.
4. Keep health/liveness endpoints and TradingView webhook ingress behavior unchanged.
5. Run CI before expanding protection to the remaining signal/order/system read endpoints.

Planned later slice (not part of the first change-set): protect the remaining sensitive read endpoints consumed by the evidence exporter.

## Working branch

```text
TradeOps/v_1.3.2.1_Paid_Pilot_Read_API_Protection
```
