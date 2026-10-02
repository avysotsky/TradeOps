# TradeOps handoff — v1.1.2.25

Date: 2026-10-02

## 1. Current state

Branch:

`TradeOps/v_1.1.2.25`

Verified implementation commit:

`3caec6254aa41c2917e2a0f1e24d0abfb2507036`

Commit message:

`feat: persist current signal execution issues`

GitHub Actions validation:

- workflow: `build`
- run number: **131**
- run id: `36991623365`
- job id: `110788905550`
- result: **success**
- tests: **145 passed / 145 total**
- Build: success
- API + PostgreSQL smoke: success
- Docker Compose validation: success
- API/Worker Docker images: success

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed

v1.1.2.25 adds a durable **current execution issue projection** to trading signals without changing established `SignalOutcome` semantics.

Infrastructure/integrity conditions are not represented as fake Accepted/Rejected transitions.

Current signal outcomes remain:

- Received
- Accepted
- Rejected

## 3. New TradingSignal projection fields

Added nullable fields:

- `ExecutionIssueCode`
- `ExecutionIssueMessage`
- `ExecutionIssueAt`

Current issue code introduced in this version:

`ClientOrderIdConflict`

Persistence configuration:

- ExecutionIssueCode: max 50
- ExecutionIssueMessage: max 1000
- ExecutionIssueAt: nullable timestamp with time zone

## 4. Database migration

Added real EF Core migration:

`20261002094500_AddSignalExecutionIssueProjection`

It adds the three nullable columns to `TradingSignals`.

The model snapshot was updated consistently.

No new index was added because the initial use is point-read/current projection and there is no evidence requiring one.

## 5. ClientOrderId conflict persistence

When `ClientOrderIdConflictException` occurs after the signal audit exists:

- current signal Outcome is left unchanged;
- `ExecutionIssueCode = ClientOrderIdConflict`;
- exception message is persisted;
- issue timestamp is persisted;
- `ITradingSignalRepository.UpdateAsync` updates the projection;
- because Outcome did not change, no new `TradingSignalOutcomeEvent` is appended;
- the exception is rethrown;
- API remains HTTP 409.

For a new unresolved signal this normally means:

- Outcome = Received
- exactly one Received outcome-history event
- current execution issue populated

## 6. Recovery behavior

When the conflicting local order is corrected and a Received signal safely resumes:

- current execution issue fields are cleared;
- execution continues;
- a real Accepted or risk-Rejected outcome is persisted normally;
- only that real outcome transition is appended to history.

The API/PostgreSQL integration test verifies recovery from ClientOrderId conflict to Accepted.

## 7. Already-Accepted signal consistency

An already Accepted signal retry now behaves as follows:

- linked local order identity is verified;
- if inconsistent, current execution issue is persisted while historical Accepted outcome remains unchanged;
- API returns 409;
- no new outcome transition is fabricated;
- after the linked order becomes consistent, verified retry clears the current issue;
- historical Accepted outcome remains Accepted.

This treats the issue as current projection/integrity state rather than rewriting historical execution outcome.

## 8. Signal audit API

`TradingSignalAuditResponse` now includes additive nullable fields:

- `executionIssueCode`
- `executionIssueMessage`
- `executionIssueAt`

Therefore:

`GET /api/signals/{id}`

can explain why a signal is currently unresolved even when Outcome remains Received.

Existing signal list responses receive the same additive fields because they use the same audit response record.

## 9. Outcome-history semantics preserved

`TradingSignalOutcomeEvents` remains strictly outcome-transition history.

Setting, changing or clearing current execution issue fields while Outcome is unchanged must not create an outcome event.

No synthetic issue-to-outcome mapping was introduced.

## 10. Tests

Added:

`TradingSignalExecutionIssuePostgresTests.UpdateAsync_PersistsAndClearsExecutionIssueWithoutAppendingOutcomeTransition`

Against real migrated PostgreSQL it verifies:

- issue fields persist;
- Outcome remains Received;
- history remains one Received event;
- issue fields clear;
- clearing also creates no outcome event.

Extended:

`SignalExecutionServiceTests.AcceptedSignalRetry_WithConflictingLinkedOrder_ThrowsClientOrderIdConflict`

It now verifies:

- Accepted signal keeps Accepted outcome;
- ClientOrderId conflict persists current issue;
- corrected linked order allows retry;
- retry clears current issue;
- OrderManager is still not rerun for the Accepted retry path.

Extended:

`ClientOrderIdConflictApiIntegrationTests`

It now verifies:

1. mismatched deterministic local order => 409;
2. signal remains Received;
3. current issue is persisted;
4. GET signal audit exposes current issue;
5. outcome history remains one Received event;
6. correcting local order quantity;
7. retry => 200;
8. signal transitions to Accepted;
9. issue fields clear;
10. history becomes exactly Received + Accepted;
11. no exchange placement occurred.

## 11. Files changed

Modified:

- `src/TradeOps.Domain/Entities/TradingSignal.cs`
- `src/TradeOps.Infrastructure/Persistence/Configurations/TradingSignalConfiguration.cs`
- `src/TradeOps.Infrastructure/Persistence/Migrations/TradeOpsDbContextModelSnapshot.cs`
- `src/TradeOps.Application/Services/SignalExecutionService.cs`
- `src/TradeOps.Api/Contracts/OperatorReadResponses.cs`
- `src/TradeOps.Api/Controllers/SignalsController.cs`
- `tests/TradeOps.UnitTests/SignalExecutionServiceTests.cs`
- `tests/TradeOps.UnitTests/ClientOrderIdConflictApiIntegrationTests.cs`

Added:

- `src/TradeOps.Infrastructure/Persistence/Migrations/20261002094500_AddSignalExecutionIssueProjection.cs`
- `tests/TradeOps.UnitTests/TradingSignalExecutionIssuePostgresTests.cs`
- `docs/signal-execution-issue-projection.md`

## 12. CI result

GitHub Actions #131 validates:

`3caec6254aa41c2917e2a0f1e24d0abfb2507036`

Results:

- Build succeeded
- **145/145 tests passed**
- migration applied successfully
- current issue persistence/clear PostgreSQL test passed
- ClientOrderId API integration/recovery test passed
- Accepted retry issue/recovery test passed
- existing SignalId conflict/request-validation tests remained green
- API + PostgreSQL smoke passed
- Docker Compose passed
- API/Worker image builds passed

## 13. Safety/project boundaries preserved

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
- no fabricated signal outcome transitions

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 14. Known limitation

Execution issues are now visible on individual/current signal projections, but the searchable signal audit currently cannot filter specifically for unresolved issues.

Operators can filter `outcome=Received`, but that mixes ordinary in-flight Received signals with signals carrying a current execution issue.

## 15. Recommended next version

Recommended branch:

`TradeOps/v_1.1.2.26`

Recommended narrow scope:

**Search/filter current signal execution issues in the existing signal audit surface.**

Prefer extending:

`GET /api/signals`

rather than creating another route.

Candidate filter:

- `executionIssueCode` — optional exact code, bounded length and normalized only if the code contract defines normalization;
- or a simpler `hasExecutionIssue=true|false` filter if that better fits operator use.

For the current product, filtering by exact `executionIssueCode=ClientOrderIdConflict` is more diagnostic.

Requirements:

- preserve existing symbol/outcome/from/to/limit semantics;
- filter the current `TradingSignals` projection, not outcome history;
- deterministic CreatedAt DESC / Id DESC ordering remains;
- add PostgreSQL + API integration coverage;
- do not add an index speculatively; inspect query plan only if representative usage justifies it.

Do not add a new metrics surface or new exchange adapter in the same milestone.

## 16. Instruction for next chat

Use:

`TradeOps-Handsoff-v1.1.2.25.md`

Continue from:

`TradeOps/v_1.1.2.25`

Verified implementation SHA:

`3caec6254aa41c2917e2a0f1e24d0abfb2507036`

GitHub Actions #131 (`36991623365`) is fully green with 145/145 tests passing.
