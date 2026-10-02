# TradeOps handoff — v1.1.2.24

Date: 2026-10-02

## 1. Current state

Branch:

`TradeOps/v_1.1.2.24`

Verified implementation commit:

`bbbbaecd261e17a8adca444a0a1ed16965bf437b`

Commit message:

`feat: harden deterministic order identity reuse`

GitHub Actions validation:

- workflow: `build`
- run number: **130**
- run id: `36990690523`
- job id: `110785942765`
- result: **success**
- tests: **144 passed / 144 total**
- Build: success
- API + PostgreSQL smoke: success
- Docker Compose validation: success
- API/Worker Docker images: success

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed

v1.1.2.24 hardens deterministic `ClientOrderId` reuse.

Before this version, `OrderManager` returned any local order found under the deterministic client order id without verifying that it represented the same execution payload.

Now an existing local order is reused only after immutable order execution identity is verified.

## 3. Deterministic identity fields

For external trading signals, the existing order must match:

- Symbol
- Side
- OrderType = Market
- RequestedQuantity

A mismatch raises:

`ClientOrderIdConflictException`

The exception carries:

- SignalId
- ClientOrderId
- ExistingOrderId
- ConflictingFields

## 4. Guarded paths

`OrderExecutionIdentityGuard.EnsureMatches(...)` is applied to:

1. an existing local order found at the start of `OrderManager.ExecuteSignalAsync`;
2. the persisted winner after `IOrderRepository.TryAddAsync` loses a unique-key race;
3. the linked local order loaded for an already Accepted signal retry in `SignalExecutionService`.

Therefore neither ordinary idempotent reuse nor race resolution can silently return an unrelated order payload.

## 5. API behavior

`POST /api/signals` catches `ClientOrderIdConflictException` and returns HTTP **409 Conflict**.

It reuses the established conflict response shape:

- signalId
- message
- conflictingFields

No exchange placement occurs on a conflicting path.

## 6. Unresolved signal state

For a newly submitted signal the initial audit row is persisted as Received before OrderManager executes.

If a deterministic local-order identity conflict is discovered after that point, TradeOps intentionally leaves:

`TradingSignals.Outcome = Received`

and keeps only the initial Received outcome-history event.

Reason:

- it must not be marked Accepted because the existing order has not been proven to represent the signal;
- it must not be marked Rejected because this is not a trading/risk rejection;
- Received represents an unresolved execution that may be retried after operator correction.

A retry while the conflict remains returns 409 again and does not append duplicate outcome transitions.

For an already Accepted historical signal whose linked local-order projection is inconsistent, retry is blocked with 409; historical Accepted state is not silently rewritten.

## 7. Exact existing-order recovery

If a deterministic local order already exists and matches all execution identity fields:

- it is reused;
- risk evaluation is not repeated;
- no second exchange placement occurs;
- a new Received signal can finalize as Accepted and link to the matching existing order.

This preserves crash/recovery idempotency.

## 8. Tests

Added:

`OrderManagerClientOrderIdentityTests`

Coverage:

- exact existing order is reused without risk or exchange;
- conflict on Symbol;
- conflict on Side;
- conflict on OrderType;
- conflict on RequestedQuantity;
- conflicting duplicate-insert race winner is rejected before exchange placement.

Added to `SignalExecutionServiceTests`:

`AcceptedSignalRetry_WithConflictingLinkedOrder_ThrowsClientOrderIdConflict`

This verifies an Accepted signal cannot silently return a corrupted/mismatched linked local order.

Added:

`ClientOrderIdConflictApiIntegrationTests.Post_ConflictingDeterministicLocalOrder_Returns409AndDoesNotPlaceOnExchange`

Against the real ASP.NET Core pipeline + migrated PostgreSQL it verifies:

- mismatched deterministic local order => 409;
- retry => 409 again;
- Mock exchange remains empty;
- signal remains Received;
- only one Received history event exists;
- no new local order is created;
- exact matching pre-existing local order => 200;
- matching signal finalizes Accepted and links to that order;
- exchange still receives no placement.

## 9. Files changed

Modified:

- `src/TradeOps.Application/Services/OrderManager.cs`
- `src/TradeOps.Application/Services/SignalExecutionService.cs`
- `src/TradeOps.Api/Controllers/SignalsController.cs`
- `tests/TradeOps.UnitTests/SignalExecutionServiceTests.cs`

Added:

- `src/TradeOps.Application/Models/ClientOrderIdConflictException.cs`
- `src/TradeOps.Application/Services/OrderExecutionIdentityGuard.cs`
- `tests/TradeOps.UnitTests/OrderManagerClientOrderIdentityTests.cs`
- `tests/TradeOps.UnitTests/ClientOrderIdConflictApiIntegrationTests.cs`
- `docs/client-order-id-identity.md`

No database migration or schema change was added.

## 10. CI result

GitHub Actions #130 validates:

`bbbbaecd261e17a8adca444a0a1ed16965bf437b`

Results:

- Build succeeded
- **144/144 tests passed**
- all deterministic order identity tests passed
- PostgreSQL/API conflict integration test passed
- existing SignalId conflict/validation tests remained green
- API + PostgreSQL smoke passed
- Docker Compose passed
- API/Worker Docker images passed

## 11. Safety/project boundaries preserved

Still in force:

- .NET 8
- PostgreSQL 16
- Mock exchange default
- Bybit Testnet only real adapter
- no mainnet / real-money support
- deterministic ClientOrderId
- no blind placement retry after ambiguity
- no blind cancellation retry after ambiguity
- no automatic flattening
- no strategy/alpha generation
- no return guarantees

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 12. Known limitation after v1.1.2.24

A new/Received signal blocked by ClientOrderId identity conflict remains correctly unresolved, but the durable signal projection currently does not store *why* it is unresolved.

The caller receives the 409 details at request time, but a later operator reading `GET /api/signals/{id}` sees Received without a structured current execution-issue diagnostic.

## 13. Recommended next version

Recommended branch:

`TradeOps/v_1.1.2.25`

Recommended scope:

**Persist current unresolved signal execution diagnostics without changing SignalOutcome semantics.**

Preferred design:

Add nullable current-projection fields on `TradingSignal`, for example:

- ExecutionIssueCode
- ExecutionIssueMessage
- ExecutionIssueAt

When `ClientOrderIdConflictException` occurs after signal persistence:

- leave Outcome = Received;
- persist an issue code/message/timestamp;
- do not append a fake Accepted/Rejected outcome transition;
- rethrow so API remains 409.

When the same Received signal later resumes successfully or reaches a real risk rejection:

- clear the current execution issue.

For an already Accepted signal whose linked order becomes inconsistent, persist the current issue without rewriting historical Accepted outcome; clear it if a later retry verifies consistency.

Expose current issue fields through the signal audit response.

Do not fabricate transition history and do not add a new SignalOutcome merely for infrastructure diagnostics.

## 14. Instruction for next chat

Use:

`TradeOps-Handsoff-v1.1.2.24.md`

Continue from:

`TradeOps/v_1.1.2.24`

Verified implementation SHA:

`bbbbaecd261e17a8adca444a0a1ed16965bf437b`

GitHub Actions #130 (`36990690523`) is fully green with 144/144 tests passing.
