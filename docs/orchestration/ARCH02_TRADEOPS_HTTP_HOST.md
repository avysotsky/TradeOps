# ARCH-02 — TradeOps Research / Rebalance / Risk / Dry-Run HTTP Host

## State

READY — SPECIFICATION ONLY

## Repository

- Repository: `avysotsky/TradeOps`
- Assigned branch: `TradeOps/arch02-research-risk-http-host`
- Initial baseline: `c13e229835bd17e49430afe7d773c2e618e6e5d4`
- DocFlow ARCH-01 integrated: PR #8, merge `d6de1b5168c156b107cb3c3d71ef29983401ad40`.
- Canonical roadmap: `docs/orchestration/DUAL_TOPOLOGY_ARCHITECTURE_PLAN.md`

## Goal

Extend the **existing** TradeOps.Api host to expose the already-integrated deterministic ResearchDecision / rebalance / risk-preview / execution-dry-run application flows via thin HTTP endpoints. Do not duplicate pipeline implementation inside controllers or enable broker/order mutation.

## Candidate API v1

```http
POST /api/v1/research/decisions
POST /api/v1/rebalance/preview
POST /api/v1/risk/preview
POST /api/v1/execution/dry-run
```

Exact request/response DTO shapes must be based on existing frozen application models and validated fixtures; don't freeze speculative contracts before inspecting production services. Avoid large-bang changes.

## Initial live review (mandatory)

1. Re-fetch live TradeOps and DocFlow main HEAD, assigned branch HEAD, compare, relevant PRs, exact CI, and existing routes.
2. Read `docs/orchestration/DUAL_TOPOLOGY_ARCHITECTURE_PLAN.md`, `docs/orchestration/ORCHESTRATION.md`, `docs/orchestration/WORKSTREAM_PROTOCOL.md`, `docs/orchestration/VS17_EXECUTION_DRY_RUN.md`.
3. Inspect `src/TradeOps.Api`, existing application DI/host composition, research decision models, VS-16 risk preview and VS-17 dry-run harness implementations.
4. Identify which parts of the current demo composition need narrow reusable application-service extraction. Prefer production application code reuse over new domain models.
5. Document exact endpoint contract and impacted files before implementing. Never assume an endpoint can directly accept an entire demo filesystem manifest.

## Mandatory boundaries

- Reuse existing TradeOps.Application logic, risk checks, deterministic identities and RebalanceOrderIntent semantics.
- API handlers are transport adapters, not strategy or execution implementations.
- Preserve existing legacy TradeOps.Api endpoints and existing CLI/demo regression harness.
- No DocFlow normalization / provider calls inside TradeOps; ARCH-03 implements future HTTP adapter separately.
- No `PlaceOrderAsync`, `CancelOrderAsync`, `OrderManager.ExecuteSignalAsync`, signal/order repository writes, database mutations, or broker submission.
- Risk must fail closed on invalid/mismatched intent or disallowed policy.
- Structured errors must be sanitized; validate bounded requests; no credentials in DTOs/logs.
- Maintain exact public/shared contract compatibility unless orchestrator explicitly approves an additive versioned DTO.

## Acceptance

1. Existing TradeOps.Api runs with the new additive HTTP routes.
2. Research decision, rebalance preview, risk preview and dry-run route behavior reuse existing validated application semantics; do not silently introduce competing business logic.
3. Offline deterministic fixtures demonstrate direct-vs-HTTP semantic equivalence for every newly exposed route.
4. Rejected risk produces no prepared order identity. Allowed dry-run contains production-style client order ID and explicit `mutationPerformed=false`, `brokerRequestSent=false`, `persistencePerformed=false`.
5. No broker mutation, no persistence, no DocFlow ingestion implementation, and no real IBKR account requirement.
6. Existing full TradeOps tests, API/PostgreSQL smoke, signed webhook demo, deployment/Docker validation pass with zero build warnings.
7. Exact-head CI and CodeQL pass; privacy/secret scan passes; no client/portfolio secrets in test fixtures.
8. No changes to frozen domain contracts without a documented compatibility decision.

## Completion

Update this document with exact HEAD, changed-file ownership, tests, exact CI, contracts, known gaps and next integration action. Create **draft PR only**, then stop for orchestrator review. Do not merge from worker chat.
