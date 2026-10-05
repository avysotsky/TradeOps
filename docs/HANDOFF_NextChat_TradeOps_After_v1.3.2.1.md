# TradeOps handoff — after v1.3.2.1

## Source of truth

Repository: `avysotsky/TradeOps`

Default branch: `main`

Release PR:

```text
#18 — TradeOps v1.3.2.1 — Paid Pilot Read API Protection
```

Working branch:

```text
TradeOps/v_1.3.2.1_Paid_Pilot_Read_API_Protection
```

v1.3.2.1 is the security-hardening follow-up to v1.3.2.0. Do not reimplement earlier milestones.

## What v1.3.2.1 changes

The existing operator API credential now protects the sensitive read path used to build paid-pilot evidence when operator authentication is enabled.

Protected paid-pilot reads:

```text
GET /api/integrations/tradingview/operations/deliveries
GET /api/integrations/tradingview/operations/metrics
GET /api/integrations/tradingview/operations/health
GET /api/signals/{id}
GET /api/orders/local/{idOrClientOrderId}
GET /api/orders/local/{idOrClientOrderId}/history
GET /api/system/reconciliation/status
```

Existing protected mutating operator actions remain protected.

The following boundaries remain intentionally unchanged:

- `/health/live` and `/health/ready` remain usable as probes.
- TradingView webhook ingress keeps its gateway boundary.
- External signed signal ingress keeps its separate signal-ingress credential/HMAC/replay boundary.
- Operator and signal-ingress credentials remain independent.
- Operator authentication is still disabled by default for the local Mock development configuration.

## Evidence exporter

`tools/TradeOps.PilotEvidence` now reads:

```text
TRADEOPS_OPERATOR_API_KEY
```

and sends it using the default header:

```text
X-TradeOps-Operator-Key
```

The header name can be overridden with:

```text
TRADEOPS_OPERATOR_API_HEADER
```

The exporter is still read-only. It does not place, cancel or reconcile orders.

## Demo/CI compatibility

Both demos were updated for the protected reads:

- `TradeOps.TradingViewDemo`
- `TradeOps.SignedWebhookDemo`

CI run #220 validated the implementation before the final documentation-only commits:

- restore/build;
- unit/integration tests;
- API + PostgreSQL smoke;
- Signed Webhook E2E;
- Customer TradingView demo + paid-pilot evidence;
- client starter-kit validation;
- Docker Compose validation;
- TradingView gateway validation;
- Docker image builds.

Run final CI after this handoff/docs commit before merging PR #18.

## Current commercial capability

TradeOps now has a complete first paid-pilot delivery path:

```text
client intake
-> starter-kit/preflight
-> TradingView HTTPS gateway
-> authenticated/idempotent delivery
-> Mock execution
-> optional Bybit Testnet
-> audit/correlation
-> reconciliation/lifecycle
-> health/metrics
-> operator-authenticated evidence package
-> client sign-off/handoff
```

This is execution/integration engineering. It does not provide or claim trading alpha, profitable strategy logic, or guaranteed returns.

## Scope constraints

Keep these unless a real client requirement explicitly changes them:

- Mock / Bybit Testnet only.
- No mainnet or real-money execution.
- No second exchange.
- No alpha/strategy generation.
- No profitability claims.
- No HFT/low-latency claims.
- No speculative SaaS billing/multitenancy/dashboard/Kubernetes expansion.

## Development decision after v1.3.2.1

Do not start a speculative v1.3.3.0 feature milestone just to keep coding.

The preferred next step is to freeze the reusable core and use TradeOps for customer acquisition / a first paid pilot.

Resume product development only for one of these reasons:

1. a concrete requirement from a real pilot/client;
2. a reproducible security/reliability defect;
3. a small delivery friction discovered while running the current pilot runbook.

Use `v1.3.2.x` for small hardening/bugfix work. Introduce a larger version only when a real client requirement justifies a new capability.

## Where to continue

Read this file first, then inspect current `main` and the latest CI before making changes.
