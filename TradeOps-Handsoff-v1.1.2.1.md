# TradeOps — Handoff for v1.1.2.1

## 1. Current status

Active development branch:

```text
TradeOps/v_1.1.2.1
```

Stable predecessor branch:

```text
TradeOps/v_1.1.1.3
predecessor HEAD used for branching: c640562224062429e6209beea93c915108d2f018
```

`v1.1.2.1` functional implementation commits:

```text
a8caf85ea3d762e63d882fc69d4e1a524e50a7ef
feat: add idempotent local order cancellation
GitHub Actions #86: fully green

93763be13afc51fa0c5b467425cb6bf4fdd40239
test: cover persisted order cancellation workflow
GitHub Actions #87: fully green
```

Actions #87 passed:

```text
Restore                     ✓
Build                       ✓
Unit tests                  ✓
API + PostgreSQL smoke      ✓
Cancellation runtime smoke  ✓
OpenAPI validation          ✓
Docker Compose validation   ✓
Docker API/Worker images    ✓
```

This handoff is added after the functional commits with `[skip ci]`; therefore the authoritative tested code HEAD for `v1.1.2.1` is `93763be13afc51fa0c5b467425cb6bf4fdd40239`.

TradeOps remains an execution and automation engineering project. It does not provide alpha, profitable strategies, trading signals, or profitability guarantees.

Commercial positioning remains:

```text
You provide the trading rules.
TradeOps provides the execution and automation engineering.
```

---

## 2. Safety and architecture boundaries preserved

The following predecessor guarantees remain in force:

- .NET 8 and PostgreSQL 16;
- `Mock` is the default exchange provider;
- the only real venue adapter is `BybitTestnet`;
- no mainnet / real-money support;
- no exchange secrets committed or logged;
- deterministic `ClientOrderId` remains the placement idempotency identity;
- ambiguous placement does not trigger blind resubmission;
- order state transitions continue through `IOrderStateMachine`;
- persistent Orders / Fills / PositionSnapshots / RiskEvents / TradingSignals remain unchanged;
- persistent operational risk controls remain unchanged;
- fee-aware accounting and signal audit from `v1.1.1.3` remain unchanged;
- operator read APIs, Swagger/OpenAPI and health/readiness remain available.

No EF Core migration or database-schema change was introduced in `v1.1.2.1`.

The existing order-placement execution semantics were not modified.

---

## 3. COMPLETED in v1.1.2.1 — persisted local-order cancellation

### New preferred cancellation API

```text
POST /api/orders/local/{idOrClientOrderId}/cancel
```

`idOrClientOrderId` may be either:

- local PostgreSQL `Order.Id` (`Guid`); or
- deterministic `ClientOrderId`.

The operation starts from the persisted local order rather than accepting an exchange order ID as its primary identity.

### Existing legacy route retained

The predecessor exchange-facing route remains for compatibility:

```text
DELETE /api/orders/{exchangeOrderId}
```

It continues to operate directly against the configured `IExchangeClient` and is not the preferred persisted local-order cancellation workflow.

---

## 4. Cancellation orchestration

New application abstraction:

```text
IOrderCancellationService
    ↓
OrderCancellationService
```

Dependencies:

```text
IOperatorReadRepository
IOrderRepository
IOrderStateMachine
IExchangeClient
IAlertService
ILogger<OrderCancellationService>
```

Normal active-order path:

```text
POST local cancel
    ↓
load persisted Order by local Guid / ClientOrderId
    ↓
validate current local state
    ↓
resolve ExchangeOrderId if necessary
    ↓
IExchangeClient.CancelOrderAsync
    ↓
read exchange state
    ↓
OrderStateMachine
    ↓
persist reconciled local state
    ↓
return explicit cancellation outcome
```

---

## 5. Idempotency and terminal-state behavior

### Already cancelled

If the persisted local order is already:

```text
Cancelled
```

TradeOps returns:

```text
Outcome = AlreadyCancelled
HTTP 200
```

No second exchange cancellation request is sent.

### Filled / Rejected

If the local order is already:

```text
Filled
Rejected
```

TradeOps returns:

```text
Outcome = NotCancellable
HTTP 409
```

No exchange cancellation request is sent.

### Created before exchange submission

`OrderStateMachine` now explicitly permits:

```text
Created -> Cancelled
```

A persisted `Created` order can therefore be cancelled locally before exchange submission. It is persisted as `Cancelled` and no exchange cancel request is required.

This is the only order-state transition added in `v1.1.2.1`.

---

## 6. Ambiguous cancellation safety

Cancellation follows the same safety principle already used by placement: do not blindly repeat an exchange mutation when its outcome is uncertain.

If `CancelOrderAsync` throws after the request may have reached the exchange:

```text
cancel exception / ambiguous outcome
    ↓
NO blind second CancelOrderAsync
    ↓
lookup exchange order
    ↓
reconcile observed state
    ↓
persist local state when safe
```

Possible outcomes include:

```text
Cancelled
AlreadyCancelled
CancellationRequested
NotCancellable
Unresolved
NotFound
```

HTTP mapping:

```text
Cancelled              -> 200
AlreadyCancelled       -> 200
CancellationRequested  -> 202
NotFound               -> 404
NotCancellable         -> 409
Unresolved             -> 503
```

`CancellationRequested` means the exchange cancel request was acknowledged or attempted, but a final terminal cancellation state could not yet be confirmed during the current request.

`Unresolved` means the exchange identity/state could not be resolved safely enough to claim a cancellation result.

---

## 7. Exchange-state reconciliation rules

When applying a fresh exchange snapshot to the persisted local order:

- invalid/stale state regressions are rejected through `IOrderStateMachine.CanTransition`;
- local filled quantity is never reduced merely because a later exchange read contains a smaller value;
- an exchange `Cancelled` state is persisted through the state machine;
- if the order becomes `Filled` or `Rejected` before cancellation completes, TradeOps reports the resulting terminal state rather than falsely claiming cancellation;
- exchange lookup may fall back from `ExchangeOrderId` to deterministic `ClientOrderId` when needed.

The Bybit testnet adapter itself was not redesigned in this version. The orchestration safety lives in the application-level cancellation service.

---

## 8. API/DI changes

Added application files:

```text
src/TradeOps.Application/Interfaces/IOrderCancellationService.cs
src/TradeOps.Application/Models/OrderCancellationResult.cs
src/TradeOps.Application/Services/OrderCancellationService.cs
```

Added API response model:

```text
OrderCancellationResponse
```

DI registration added:

```text
IOrderCancellationService -> OrderCancellationService
```

`OrdersController` now exposes the persisted local cancellation endpoint while retaining all predecessor order endpoints.

---

## 9. Unit-test coverage added

`OrderCancellationServiceTests` covers:

1. active order cancellation and confirmed persistent `Cancelled` state;
2. retry of already-cancelled order returns `AlreadyCancelled` without another exchange call;
3. filled order returns `NotCancellable` without an exchange call;
4. ambiguous exchange cancellation failure reconciles exchange state without blind retry;
5. `Created` order can be cancelled locally before exchange submission.

All unit tests pass in Actions #86 and #87.

---

## 10. Runtime CI coverage added

Actions #87 extends the credential-free Mock/PostgreSQL runtime smoke with a dedicated second logical signal so the existing predecessor reconciliation scenario remains intact.

Cancellation smoke flow:

```text
create second signal
    ↓
Mock order = PartiallyFilled
    ↓
read signal audit and deterministic ClientOrderId
    ↓
POST /api/orders/local/{clientOrderId}/cancel
    ↓
assert Outcome = Cancelled
    ↓
assert persisted Order.Status = Cancelled
    ↓
POST same cancel again
    ↓
assert Outcome = AlreadyCancelled
    ↓
assert persisted Order.Status still = Cancelled
```

The generated OpenAPI document is also checked for:

```text
/api/orders/local/{idOrClientOrderId}/cancel
```

Ordinary CI remains credential-free. Actions #87 did not authenticate to Bybit and did not perform a real Bybit cancellation. It validates Mock/PostgreSQL runtime behavior plus compilation of the full Bybit-capable solution.

---

## 11. Documentation

`docs/operator-api.md` now distinguishes:

```text
DELETE /api/orders/{exchangeOrderId}
```

as the legacy exchange-facing cancellation path, and:

```text
POST /api/orders/local/{idOrClientOrderId}/cancel
```

as the preferred persisted local-order cancellation workflow.

The documentation also describes cancellation outcomes, HTTP mappings, reconciliation semantics and the no-blind-cancel-retry rule.

---

## 12. Definition of Done for v1.1.2.1 — COMPLETE

- [x] new branch created from actual `v1.1.1.3` HEAD;
- [x] persisted local order is the primary cancellation identity;
- [x] local Guid lookup supported;
- [x] deterministic ClientOrderId lookup supported;
- [x] active order can be cancelled through `IExchangeClient`;
- [x] confirmed exchange cancellation is persisted locally;
- [x] already-cancelled retry is idempotent;
- [x] terminal Filled/Rejected order is not cancelled;
- [x] Created order can be cancelled before submission;
- [x] ambiguous cancel outcome is reconciled instead of blindly retried;
- [x] explicit cancellation outcome model exists;
- [x] HTTP status mapping exists;
- [x] Swagger/OpenAPI contains the new route;
- [x] focused unit tests added;
- [x] runtime Mock/PostgreSQL cancellation smoke added;
- [x] existing placement/recovery/risk/accounting/signal-audit smoke remains green;
- [x] Docker Compose validates;
- [x] API/Worker Docker images build;
- [x] no database migration introduced;
- [x] no mainnet support introduced;
- [x] no secret committed;
- [x] Actions #87 fully green.

`v1.1.2.1` is functionally complete. Do not add more capabilities to this version without explicitly changing its scope.

---

## 13. Out of scope for v1.1.2.1

Not implemented here:

- cancel-all;
- cancellation by symbol as a bulk operation;
- automatic cancel-all when EmergencyStop activates;
- automatic position flattening;
- mainnet / real-money execution;
- second exchange adapter;
- trading strategy / alpha generation;
- generalized multi-currency FX conversion;
- React/dashboard UI;
- SaaS multitenancy/billing;
- Kubernetes/HFT architecture.

---

## 14. CURRENT NEXT TASK — define v1.1.2.2 before coding

The clean next version is:

```text
TradeOps/v_1.1.2.2
```

Recommended narrow scope:

```text
Bulk open-order cancellation + EmergencyStop cancellation integration
```

Candidate work items, to be explicitly approved before implementation:

1. define safe `cancel-all` semantics over persisted/open exchange orders;
2. optionally scope cancellation by symbol;
3. make repeated bulk cancellation idempotent;
4. integrate cancellation with persistent `EmergencyStop` without flattening positions;
5. preserve no-blind-retry semantics for every individual cancellation;
6. add unit tests and credential-free Mock/PostgreSQL runtime smoke;
7. document exact behavior and partial-failure reporting.

Do not implement position flattening as part of this next step unless separately specified.

---

## 15. Instruction for the next chat

Start with:

> Continue TradeOps. Read `TradeOps-Handsoff-v1.1.2.1.md` first. `v1.1.2.1` is complete. Branch `TradeOps/v_1.1.2.1` added persisted/idempotent local-order cancellation through `POST /api/orders/local/{idOrClientOrderId}/cancel`. Functional commits are `a8caf85...` and `93763be...`; Actions #87 is fully green including the dedicated cancellation runtime smoke. Do not reimplement single-order cancellation. Preserve Mock-default, Bybit-testnet-only, no-mainnet, deterministic ClientOrderId, placement no-blind-retry and cancellation no-blind-retry boundaries. Before coding, create/define `v1.1.2.2`; the candidate scope is bulk open-order cancellation plus EmergencyStop cancellation integration, explicitly without automatic position flattening.

No additional context from the previous chat should be required beyond this handoff and the repository code.
