# ARCH-02 — TradeOps Research / Rebalance / Risk / Dry-Run HTTP Host

## State

IN_PROGRESS — API composition discovery; production endpoint implementation gated

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

## Live discovery checkpoint — 2026-10-08

TradeOps main observed: `d1f0ed6c2d42562e75c0653c390e1a34b0c5fcb6`.
DocFlow main observed: `d6de1b5168c156b107cb3c3d71ef29983401ad40`.
Initial ARCH-02 branch was ahead 1 / behind 1 because the orchestrator status commit advanced main after branch registration. No overlapping production files were changed.

Confirmed code:
- Existing `src/TradeOps.Api/Program.cs` registers the production `IRiskEngine` with database-backed `IRiskControlService`; the web host runs database migrations on startup.
- `src/TradeOps.Application/Services/RiskEngine.cs` invokes `IRiskControlService.RecordRejectionAsync` when policy rejects a signal. Therefore **injecting the normal production risk engine in a preview endpoint is not read-only**.
- VS-16 demo uses a non-mutating risk-control adapter and isolated position exchange-client adapter; its `RebalanceRiskPreviewSignalProjector` supplies stable `TradingSignal` identity.
- VS-17 dry-run service in the transcript demo owns an additive execution envelope with production `ClientOrderIdGenerator`.
- TradeOps.Api already exists; no new API project or duplicate host should be created.

## Required correction before implementing risk HTTP

Refactor/share the **pure** preview composition into the appropriate reusable application boundary, or isolate it behind a DI-registered query service with explicitly non-mutating `IRiskControlService` and read-only portfolio snapshot adapter. Do not route any HTTP preview through the standard DI-registered, database-backed `IRiskControlService` that records risk rejection. For the same reason, do not mutate operational run state, exchange state, orders, alerts, or persistence while previewing.

All four endpoints must be driven by bounded request DTOs (not local filesystem paths). A proposed plan/risk/dry-run request may carry only validated snapshot data and stable IDs. A real-provider source acquisition or DocFlow transport is not part of ARCH-02.

## Current status and gate

No HTTP endpoint implementation has been committed in this discovery checkpoint. No tests or CI for ARCH-02 can yet be claimed. The next code slice must first provide safe reusable preview services and fixture tests demonstrating **zero calls** to rejection-recording, storage and broker mutation interfaces. Then thin HTTP routing and direct-vs-HTTP parity tests can be added. Do not mark ready for integration until all four contract surfaces are implemented and CI passes.

## Risk-preview HTTP slice — 2026-10-08

Production application + unit-test slice at `6f26abfe94b50aa5c7b36d3e2ff52f0aac28e3db` passed build CI `37794873196` SUCCESS.

The following incremental HTTP slice is committed:
- `src/TradeOps.Api/Controllers/ResearchRiskPreviewController.cs`
- `tests/TradeOps.UnitTests/ResearchRiskPreviewControllerTests.cs`
- HTTP endpoint: `POST /api/v1/risk/preview`; requires existing `[OperatorApiKey]` and `ResearchPreview:Enabled=true` (disabled by default).
- The controller constructs only a transient signal, executes `NonMutatingRiskPreview.CheckAsync` with provided snapshots, and returns risk decision.
- No database-backed IRiskControlService is injected into this endpoint and no broker invocation or order persistence occurs.
- Validated test cases: disabled endpoint, allowed risk, rejected risk, invalid quantity. Tests are committed but their exact-head CI remains PENDING.
- **Security review required before enabling endpoint**: confirm operator authentication is enabled in deployed settings (existing operator auth may itself be optional), and trust/authenticate submitted risk snapshots. An attacker-supplied snapshot must not be mistaken for authoritative live risk approval.
- HTTP risk response is informational only; it **must never authorize production order execution**.
- Other ARCH-02 routes (research/rebalance/dry-run) are not yet implemented. No integration or merge before all gates.
