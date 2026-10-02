# TradeOps handoff — v1.1.2.22

Date: 2026-10-02

## 1. Current state

Branch:

`TradeOps/v_1.1.2.22`

Verified implementation commit:

`17608a02ed1b6980680d59baf5b838e1edf42d17`

Commit message:

`feat: reject conflicting SignalId retries`

GitHub Actions:

- workflow: `build`
- run number: **126**
- run id: `36987727970`
- job id: `110776479728`
- result: **success**
- tests: **118 passed / 118 total**
- Build: success
- API + PostgreSQL smoke: success
- Docker Compose validation: success
- API/Worker Docker images: success

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed

v1.1.2.22 hardens SignalId idempotency semantics.

Previously an already-persisted SignalId was reused even when the incoming request changed execution-relevant data. The old unit test explicitly changed RequestedQuantity and still expected the persisted execution to be returned.

That behavior is removed.

A SignalId now identifies one immutable execution intent.

## 3. Execution identity

Before reusing an existing persisted signal, TradeOps compares:

- Symbol
- Side
- SignalType
- RequestedQuantity
- RiskPercent
- StopLoss
- TakeProfit

The same comparison is also performed after a concurrent duplicate-insert race resolves to the persisted winner.

The following are intentionally not part of immutable execution identity:

- Source
- CreatedAt

They are metadata / request-receipt context rather than execution parameters.

## 4. Exact retry behavior

An exact retry with the same SignalId and same execution identity retains existing idempotent behavior.

For a persisted Accepted signal:

- the linked local order is loaded and returned;
- OrderManager is not executed again;
- no second exchange placement occurs.

For a persisted Rejected signal:

- the persisted risk rejection is returned;
- risk/execution is not run again.

For a persisted Received signal:

- existing logical execution resumes under the same intent.

Changing Source alone remains allowed and does not overwrite the originally persisted Source.

## 5. Conflict behavior

If the same SignalId is submitted with different execution-relevant fields:

- `SignalIdConflictException` is raised in the application service;
- API returns HTTP **409 Conflict**;
- response contains:
  - `signalId`
  - human-readable message
  - `conflictingFields`
- OrderManager is not called;
- no second logical signal is created;
- no second local order is created.

## 6. Concurrent duplicate race

The same invariant applies after `TryAddAsync` loses a unique-key race.

The persisted winner is loaded and compared with the incoming execution intent before reuse.

A materially different race winner causes a conflict instead of being silently treated as an idempotent retry.

## 7. Files changed

Modified:

- `src/TradeOps.Application/Services/SignalExecutionService.cs`
- `src/TradeOps.Api/Controllers/SignalsController.cs`
- `tests/TradeOps.UnitTests/SignalExecutionServiceTests.cs`

Added:

- `src/TradeOps.Application/Models/SignalIdConflictException.cs`
- `src/TradeOps.Api/Contracts/SignalIdConflictResponse.cs`
- `tests/TradeOps.UnitTests/SignalIdConflictApiIntegrationTests.cs`
- `docs/signal-id-idempotency.md`

No migration or schema change was added.

## 8. Unit-test coverage

The service test suite now verifies conflicts independently for:

- Symbol
- Side
- SignalType
- RequestedQuantity
- RiskPercent
- StopLoss
- TakeProfit

All conflict cases require:

- explicit SignalIdConflictException;
- expected conflicting field;
- zero OrderManager calls;
- one persisted logical signal.

A separate test verifies conflicting duplicate-insert race behavior.

The existing accepted retry test was corrected so that it changes only Source metadata, not RequestedQuantity.

## 9. API/PostgreSQL integration coverage

Added:

`SignalIdConflictApiIntegrationTests.Post_SameSignalIdWithDifferentExecutionPayload_ReturnsConflictWithoutSecondOrder`

Against a real migrated PostgreSQL database and actual ASP.NET Core pipeline it verifies:

1. first POST with caller-supplied SignalId succeeds;
2. exact retry with the same execution payload and different Source succeeds idempotently;
3. both successful responses reference the same client order;
4. retry with different quantity returns HTTP 409;
5. conflict identifies `RequestedQuantity`;
6. PostgreSQL still contains exactly:
   - 1 TradingSignal
   - 1 Order
   - 2 outcome-history events (Received + Accepted)

This demonstrates that a conflicting retry does not create a second execution.

## 10. CI result

GitHub Actions #126 validates final implementation commit:

`17608a02ed1b6980680d59baf5b838e1edf42d17`

Results:

- Build succeeded
- **118/118 tests passed**
- all seven immutable-field conflict theory cases passed
- concurrent duplicate-race conflict test passed
- API/PostgreSQL conflict test passed
- runtime smoke passed
- Docker Compose validation passed
- API/Worker Docker image builds passed

## 11. Existing execution safety rules preserved

Still in force:

- deterministic ClientOrderId
- no blind order-placement retry after ambiguous outcome
- no blind cancellation retry after ambiguous outcome
- no automatic flattening
- Mock exchange default
- Bybit Testnet only real adapter
- no mainnet / real-money support
- no strategy / alpha generation
- no return guarantees

## 12. Recommended next version

Recommended branch:

`TradeOps/v_1.1.2.23`

Recommended direction:

Inspect the external-signal ingestion/API surface for the next concrete operator/customer safety gap.

High-value candidates, in priority order:

1. explicit request validation before persistence/execution for malformed execution instructions (empty symbol, non-positive quantity, invalid optional price/risk values);
2. bounded batch submission only if it can preserve per-signal idempotency and never introduce blind retries;
3. local-order audit query-plan characterization only if performance evidence is needed.

Prefer request validation before adding a new broad feature surface.

Do not add strategy/alpha logic.

## 13. Instruction for next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.22.md`

Continue from:

`TradeOps/v_1.1.2.22`

Verified implementation SHA:

`17608a02ed1b6980680d59baf5b838e1edf42d17`

GitHub Actions #126 (`36987727970`) is fully green with 118/118 tests passing.
