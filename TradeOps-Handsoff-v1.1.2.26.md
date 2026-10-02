# TradeOps handoff — v1.1.2.26

Date: 2026-10-02

## 1. Current state

Branch:

`TradeOps/v_1.1.2.26`

Verified implementation commit:

`3206292c001d3cde3b84d5bf8145f83e9f3c14b7`

Commit message:

`feat: filter signal audit by execution issue`

GitHub Actions:

- workflow: `build`
- run number: **132**
- run id: `36992122597`
- job id: `110790504758`
- result: **success**
- tests: **145 passed / 145 total**
- Build: success
- API + PostgreSQL smoke: success
- Docker Compose validation: success
- API/Worker Docker images: success

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed

v1.1.2.26 extends the existing searchable signal audit surface with a current execution-issue filter.

Endpoint remains:

`GET /api/signals`

New optional query parameter:

`executionIssueCode`

No new route was added.

## 3. Filter semantics

`executionIssueCode` filters:

`TradingSignals.ExecutionIssueCode`

It is a filter over the **current signal projection**, not signal outcome history.

Behavior:

- optional;
- whitespace-only => no filter;
- input is trimmed;
- exact case-sensitive comparison;
- maximum length 50;
- longer value => HTTP 400;
- unknown valid-length code => HTTP 200 with an empty collection.

Current diagnostic example:

`ClientOrderIdConflict`

Example:

`GET /api/signals?executionIssueCode=ClientOrderIdConflict&outcome=Received&limit=50`

## 4. Existing filters remain unchanged

The same endpoint continues to support:

- `symbol`
- `outcome`
- `from`
- `to`
- `limit`

The new issue-code filter can be combined with them.

Existing time semantics remain:

`TradingSignals.CreatedAt`

with half-open bounds:

`[from, to)`

Existing deterministic ordering remains:

1. `CreatedAt DESC`
2. `Id DESC`

## 5. Repository compatibility

`IOperatorReadRepository` retains:

- the original `GetSignalsAsync(int limit, ...)`;
- the previous filtered overload without issue code.

A new overload adds:

`string? executionIssueCode`

The interface provides a compatibility default implementation that delegates to the previous filtered overload.

`EfOperatorReadRepository` implements the new overload, while the old overloads delegate to it with `executionIssueCode: null`.

This preserves existing fakes/consumers.

## 6. PostgreSQL implementation

Filtering is performed in PostgreSQL:

`signal.ExecutionIssueCode == normalizedExecutionIssueCode`

No in-memory filtering is introduced.

The endpoint remains bounded to the existing maximum limit of 200.

## 7. Index decision

No database migration or index was added.

Reason:

- this milestone is a bounded current-projection correctness/operator feature;
- there is not yet representative evidence that an index led by `ExecutionIssueCode` is needed;
- indexing remains evidence-driven.

## 8. PostgreSQL test coverage

Extended:

`SignalAuditPostgresTests.GetSignalsAsync_FiltersByCreationTimeSymbolOutcomeAndLimitDeterministically`

The fixture now includes:

- one signal with `ExecutionIssueCode = ClientOrderIdConflict`;
- another signal with a different issue code.

The test verifies:

- whitespace trimming of issue-code input;
- exact current issue filtering;
- combination with symbol;
- combination with current outcome;
- existing creation-time bounds and deterministic ordering;
- old overload behavior remains intact.

## 9. API integration coverage

Extended:

`SignalAuditApiIntegrationTests.GetSignals_FiltersNormalizesAndRejectsInvalidAuditQueriesAgainstPostgres`

Against the actual ASP.NET Core pipeline and migrated PostgreSQL it verifies:

- `executionIssueCode=%20ClientOrderIdConflict%20` is trimmed;
- combination with `outcome=Rejected`;
- exactly the signal carrying that current issue is returned;
- response exposes `executionIssueCode`;
- unknown issue code returns empty HTTP 200;
- issue code length 51 returns HTTP 400;
- all previous signal-audit validation/filter tests remain green.

## 10. Documentation

Updated:

`docs/signal-audit-query.md`

It now documents the issue-code filter and explicitly distinguishes it from outcome-transition history.

## 11. Files changed

Modified:

- `src/TradeOps.Application/Interfaces/IOperatorReadRepository.cs`
- `src/TradeOps.Infrastructure/Persistence/Repositories/EfOperatorReadRepository.cs`
- `src/TradeOps.Api/Controllers/SignalsController.cs`
- `tests/TradeOps.UnitTests/SignalAuditPostgresTests.cs`
- `tests/TradeOps.UnitTests/SignalAuditApiIntegrationTests.cs`
- `docs/signal-audit-query.md`

No schema/migration file changed.

## 12. CI result

GitHub Actions #132 validates:

`3206292c001d3cde3b84d5bf8145f83e9f3c14b7`

Results:

- Build succeeded
- **145/145 tests passed**
- PostgreSQL signal-audit test passed
- API signal-audit integration test passed
- existing signal request/idempotency/execution-issue tests remained green
- API + PostgreSQL smoke passed
- Docker Compose validation passed
- API/Worker Docker image builds passed

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
- no fabricated outcome history

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 14. Recommended next version

Recommended branch:

`TradeOps/v_1.1.2.27`

Before implementing another endpoint, inspect the bounded operator list surfaces for one concrete usability gap.

Preferred direction:

**stable cursor pagination for signal audit** if current list-only `limit <= 200` materially prevents operators from walking historical audit data.

If implemented:

- preserve existing endpoint and filters;
- use keyset/cursor semantics based on the existing deterministic ordering `CreatedAt DESC, Id DESC`;
- avoid offset pagination;
- cursor must be opaque or strictly validated;
- no duplicate/skip across equal timestamps;
- retain bounded page size;
- add real PostgreSQL and API integration coverage;
- add no index until existing `CreatedAt` / `(Symbol, CreatedAt)` access paths are inspected with representative plans.

Alternative if pagination is not yet justified: inspect the current cancellation/reconciliation operator surfaces for a more immediate customer-facing safety gap.

Do not add metrics merely for completeness.

## 15. Instruction for next chat

Use:

`TradeOps-Handsoff-v1.1.2.26.md`

Continue from:

`TradeOps/v_1.1.2.26`

Verified implementation SHA:

`3206292c001d3cde3b84d5bf8145f83e9f3c14b7`

GitHub Actions #132 (`36992122597`) is fully green with 145/145 tests passing.
