# TradeOps handoff — v1.1.2.12

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.12`

Verified implementation commit:

`543877cbabc2c40886c0f72ed44f9120ddfffdb9`

Commit message:

`feat: persist trading signal outcome history`

GitHub Actions validation:

- workflow: `build`
- run number: **108**
- run id: `36888860229`
- job id: `110459050195`
- result: **success**

Successful steps:

- Restore
- Build
- Unit tests, including PostgreSQL-backed trading-signal outcome-history coverage
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed in v1.1.2.12

The narrow goal from `TradeOps-Handsoff-v1.1.2.11.md` is complete:

**Persisted trading-signal outcome transition history.**

A new append-only history table records signal outcome transitions while preserving `TradingSignals` as the current/latest projection.

The existing execution metrics endpoint semantics were deliberately not changed in this version.

## 3. New persisted history model

New entity:

`TradingSignalOutcomeEvent`

New table:

`TradingSignalOutcomeEvents`

Persisted fields:

- `Id`
- `TradingSignalId`
- `PreviousOutcome` nullable
- `Outcome`
- `OccurredAt`
- `RiskRejectionReasons`
- `OrderId` nullable
- `ClientOrderId` nullable

The initial event has:

- `PreviousOutcome = null`
- `Outcome = Received`

Terminal transitions normally have:

- `PreviousOutcome = Received`
- `Outcome = Accepted` or `Rejected`

The event snapshots terminal context relevant to the outcome:

- risk-rejection reasons for rejected signals;
- local `OrderId` / deterministic `ClientOrderId` linkage for accepted signals.

## 4. Append-only and atomic write behavior

`EfTradingSignalRepository` remains the write repository used by `SignalExecutionService`.

Its public application interface was intentionally not changed.

### Initial signal persistence

`TryAddAsync(...)` now adds both:

- the `TradingSignals` current projection;
- the initial `TradingSignalOutcomeEvents` `Received` event.

Both are committed by the same `DbContext.SaveChangesAsync(...)` call.

Therefore the current projection and initial history event are part of the same database transaction boundary.

On a PostgreSQL unique-violation duplicate race, both tracked entities are detached before returning `false`; the existing idempotent duplicate-resolution flow remains in force.

### Terminal signal persistence

`UpdateAsync(...)` first reads the currently persisted outcome.

When the outcome changes, it adds a `TradingSignalOutcomeEvent` and updates the `TradingSignals` projection in the same `SaveChangesAsync(...)` call.

When the outcome does not change, no extra history row is created.

This protects repeated same-outcome updates from producing duplicate transition events.

## 5. Current projection is preserved

`TradingSignals` remains the latest/current projection.

Existing execution/idempotency behavior continues to inspect the current projection:

- `Received` means the logical signal can be resumed;
- `Accepted` means the linked local order can be reused;
- `Rejected` means the persisted rejection can be reused.

The new history table does not replace that projection and does not change the current retry contract.

## 6. New history read endpoint

New operator-facing route:

`GET /api/signals/{id}/history`

Behavior:

- if the `TradingSignals` projection does not exist, returns HTTP 404;
- if the signal exists, returns its persisted outcome events ordered by `OccurredAt`, then `Id`;
- an existing legacy signal with no history rows can legitimately return an empty array.

Response item fields:

- `id`
- `signalId`
- `previousOutcome`
- `outcome`
- `occurredAt`
- `riskRejectionReasons`
- `orderId`
- `clientOrderId`

New read abstraction:

`ITradingSignalOutcomeHistoryRepository`

Implementation:

`EfTradingSignalOutcomeHistoryRepository`

## 7. Database migration

Added migration:

`20261001155500_AddTradingSignalOutcomeHistory`

It creates `TradingSignalOutcomeEvents` and an index:

`(TradingSignalId, OccurredAt)`

The table has a foreign key to `TradingSignals` with restrictive delete behavior.

The EF Core model snapshot was updated accordingly.

## 8. No historical backfill

The migration intentionally does **not** synthesize history rows for existing `TradingSignals` records.

Reason:

The old schema did not persist real outcome-transition timestamps. Backfilling an `Accepted` or `Rejected` event with migration time, signal creation time, or another guessed timestamp would create false historical data.

Therefore:

- signals processed from v1.1.2.12 onward can accumulate truthful transition events;
- pre-v1.1.2.12 signals may have an empty outcome history;
- do not later interpret absence of history on legacy rows as proof that no transition occurred.

If migration-era coverage metadata is ever needed, add it explicitly rather than fabricating event timestamps.

## 9. PostgreSQL integration coverage

Added:

`tests/TradeOps.UnitTests/TradingSignalOutcomeHistoryPostgresTests.cs`

The test creates an isolated PostgreSQL database, applies the real migrations and verifies:

- initial `Received` event persistence;
- `PreviousOutcome = null` for the initial event;
- `Received -> Rejected` persistence;
- rejected-event risk-reason snapshot;
- repeated `UpdateAsync` with unchanged `Rejected` outcome does not create another event;
- `Received -> Accepted` persistence;
- accepted-event `OrderId` and `ClientOrderId` snapshot;
- current `TradingSignals` projections still store terminal latest state;
- expected append-only event count;
- real migration execution on PostgreSQL 16.

This test passed in GitHub Actions #108.

## 10. Execution metrics semantics are unchanged in v1.1.2.12

The new outcome-transition history is **not yet used** by:

- `GET /api/metrics/execution/window`
- `GET /api/metrics/execution/series`
- `GET /api/metrics/execution/by-symbol`

Their signal counters retain the established current-outcome semantics:

- select signals by `TradingSignals.CreatedAt` within the requested window;
- count their current persisted `TradingSignals.Outcome` values.

This remains intentionally different from an outcome-transition timeline.

Do not silently change that contract when continuing from this version.

## 11. Important semantic distinction for the next metrics stage

Two different time axes now exist and must not be mixed implicitly:

### Signal creation time

`TradingSignals.CreatedAt`

This answers questions such as:

- how many signals were received/created in a selected interval;
- what their current outcomes are now.

### Outcome transition time

`TradingSignalOutcomeEvents.OccurredAt`

This can answer questions such as:

- how many signals transitioned to Accepted during an interval;
- how many signals transitioned to Rejected during an interval;
- when a specific signal moved through its persisted outcome lifecycle.

A future historical metrics implementation must state clearly which axis each counter uses.

For example, it would be misleading to keep `Received` bucketed by signal `CreatedAt` while silently bucketting `Accepted` / `Rejected` by transition `OccurredAt` inside one object without documenting that mixed-time-axis contract.

## 12. Files changed in the verified implementation commit

Added:

- `src/TradeOps.Domain/Entities/TradingSignalOutcomeEvent.cs`
- `src/TradeOps.Infrastructure/Persistence/Configurations/TradingSignalOutcomeEventConfiguration.cs`
- `src/TradeOps.Application/Interfaces/ITradingSignalOutcomeHistoryRepository.cs`
- `src/TradeOps.Infrastructure/Persistence/Repositories/EfTradingSignalOutcomeHistoryRepository.cs`
- `src/TradeOps.Api/Contracts/TradingSignalOutcomeEventResponse.cs`
- `src/TradeOps.Infrastructure/Persistence/Migrations/20261001155500_AddTradingSignalOutcomeHistory.cs`
- `tests/TradeOps.UnitTests/TradingSignalOutcomeHistoryPostgresTests.cs`

Modified:

- `src/TradeOps.Infrastructure/Persistence/Repositories/EfTradingSignalRepository.cs`
- `src/TradeOps.Infrastructure/Persistence/TradeOpsDbContext.cs`
- `src/TradeOps.Infrastructure/Persistence/Migrations/TradeOpsDbContextModelSnapshot.cs`
- `src/TradeOps.Api/Controllers/SignalsController.cs`
- `src/TradeOps.Api/Program.cs`

No execution-metrics repository was modified.

## 13. Safety and project boundaries preserved

Still in force:

- .NET 8;
- PostgreSQL 16;
- Mock exchange remains the default provider;
- Bybit Testnet remains the only real venue adapter;
- no mainnet / real-money support;
- deterministic `ClientOrderId` remains placement identity;
- no blind order-placement retry after ambiguous outcome;
- no blind cancellation retry after ambiguous outcome;
- no automatic position flattening;
- no strategy / alpha generation;
- no return guarantees;
- persisted order lifecycle semantics unchanged;
- operational-run history/projection semantics unchanged;
- persistent risk-state semantics unchanged;
- recovery/reconciliation semantics unchanged.

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 14. Known limitations after v1.1.2.12

- outcome history is truthful only for transitions persisted after the new table exists; legacy signals are not backfilled;
- existing execution metrics still expose current-outcome-of-created-signals semantics, not transition-event metrics;
- the history ordering has no explicit integer sequence column; it currently orders by `OccurredAt`, then `Id`;
- no formal production latency SLO;
- no materialized metrics rollups;
- no Prometheus/OpenTelemetry metrics export;
- no Grafana dashboard;
- no `symbol x bucket` matrix;
- no second exchange adapter;
- no mainnet;
- no strategy/alpha implementation.

## 15. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.13`

Recommended narrow scope:

**Historical signal transition metrics, without silently breaking the established current-outcome metrics contract.**

Preferred approach:

Introduce a separate bounded/read-only transition-metrics surface based on `TradingSignalOutcomeEvents.OccurredAt` rather than immediately redefining the existing `/window`, `/series`, or `/by-symbol` counters.

A sensible first contract could provide transition counts for a requested `[from,to)` window:

- `receivedTransitions`
- `acceptedTransitions`
- `rejectedTransitions`

with optional symbol scoping resolved through the parent `TradingSignals.Symbol`.

Important rules:

- event-time selection must use `TradingSignalOutcomeEvents.OccurredAt`;
- explicitly document that pre-v1.1.2.12 legacy history is incomplete;
- do not reconstruct transition timestamps from `TradingSignals.Outcome`;
- do not silently reinterpret existing metrics endpoints;
- keep the result bounded if a series form is introduced;
- reuse the representative query-plan discipline from v1.1.2.11 before adding speculative indexes.

Alternative narrow scope: add a monotonic per-signal sequence field only if deterministic same-timestamp transition ordering becomes operationally necessary. Do not combine unrelated observability/export work into the same milestone.

## 16. Instruction for the next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.12.md`

Continue from branch:

`TradeOps/v_1.1.2.12`

Verified implementation SHA:

`543877cbabc2c40886c0f72ed44f9120ddfffdb9`

GitHub Actions #108 (`36888860229`) is fully green.

Do not fabricate signal outcome history for legacy records.

Do not silently switch existing execution metrics from signal-creation/current-outcome semantics to outcome-transition-time semantics.
