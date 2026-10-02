# TradeOps handoff — v1.1.2.21

Date: 2026-10-02

## 1. Current state

Branch:

`TradeOps/v_1.1.2.21`

Verified implementation commit:

`d52713f8c6fd5ceb41a09ac335395fad9064b86f`

Commit message:

`feat: add searchable local order audit`

GitHub Actions:

- workflow: `build`
- run number: **123**
- run id: `36908214946`
- job id: `110524019579`
- result: **success**
- tests: **109 passed / 109 total**

Successful steps:

- Restore
- Build
- Unit/integration tests
- API + PostgreSQL smoke test
- Validate Docker Compose
- Build Docker images

## 2. Scope completed

Added a bounded searchable operator audit surface over persisted local orders.

Endpoint:

`GET /api/orders/local`

This is separate from:

`GET /api/orders`

which reads current open orders from the configured exchange adapter.

The local audit endpoint is read-only and uses PostgreSQL persisted state only.

## 3. Query parameters

Supported filters:

- `symbol` — optional, trimmed and uppercased, max 50 characters
- `status` — optional persisted current `OrderStatus`
- `from` — optional inclusive lower bound on `Orders.CreatedAt`
- `to` — optional exclusive upper bound on `Orders.CreatedAt`
- `limit` — default 50, valid range 1..200

Validation:

- invalid limit => HTTP 400
- symbol length > 50 => HTTP 400
- invalid enum model binding => HTTP 400
- `from >= to` => HTTP 400

Date/time inputs are normalized to UTC.

## 4. Ordering and semantics

Results are ordered by:

1. `CreatedAt DESC`
2. `Id DESC`

The `status` filter is the current persisted local order projection.

This endpoint does not answer when an order entered a state.

For exact lifecycle history use:

`GET /api/orders/local/{idOrClientOrderId}/history`

## 5. Architecture

Added:

`ILocalOrderAuditRepository`

Implementation:

`EfLocalOrderAuditRepository`

The repository:

- uses `AsNoTracking()`
- filters in PostgreSQL
- defensively clamps limit to 1..200
- applies deterministic ordering
- does not call the exchange

Registered in API DI.

## 6. Tests

Added PostgreSQL-backed repository coverage:

`LocalOrderAuditPostgresTests.GetAsync_FiltersByCreationTimeSymbolStatusAndLimitDeterministically`

Added full API integration coverage through `WebApplicationFactory<Program>`:

`LocalOrderAuditApiIntegrationTests.GetLocalOrders_FiltersNormalizesAndRejectsInvalidAuditQueriesAgainstPostgres`

Both passed in Actions #123.

Final suite result:

**109/109 passed**

## 7. Documentation

Added:

`docs/local-order-audit-query.md`

Documents filters, validation, local-vs-exchange semantics, deterministic ordering, lifecycle-history distinction and index policy.

## 8. Database/index decision

No migration or new index was added.

The endpoint initially reuses existing indexes.

No speculative `CreatedAt`/status index was introduced without representative PostgreSQL evidence.

## 9. Safety/project boundaries preserved

Still in force:

- .NET 8
- PostgreSQL 16
- Mock exchange default
- Bybit Testnet only real adapter
- no mainnet / real-money support
- deterministic `ClientOrderId`
- no blind order placement retry after ambiguity
- no blind cancellation retry after ambiguity
- no automatic flattening
- no strategy/alpha generation
- no return guarantees

Commercial positioning:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 10. Recommended next version

Recommended branch:

`TradeOps/v_1.1.2.22`

Recommended scope:

**Signal-id idempotency conflict hardening.**

Inspect current `SignalExecutionService` retry behavior.

Required safety invariant:

- an exact retry using the same `SignalId` and the same immutable execution payload remains idempotent;
- the same `SignalId` reused with materially different execution data must be rejected explicitly;
- the conflict must never cause a second exchange placement;
- existing ambiguous-placement/reconciliation behavior must remain unchanged.

At minimum evaluate immutable comparison of:

- symbol
- side
- requested quantity
- signal type
- risk percent
- stop loss
- take profit

Treat non-execution metadata such as source separately and document the decision.

Prefer an explicit conflict result / HTTP 409 over silently returning execution from a different payload.

Add coverage for exact retry plus conflicting symbol, side and quantity.

Do not mix this milestone with new metrics or exchange adapters.

## 11. Instruction for next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.21.md`

Continue from:

`TradeOps/v_1.1.2.21`

Verified implementation SHA:

`d52713f8c6fd5ceb41a09ac335395fad9064b86f`

GitHub Actions #123 (`36908214946`) is fully green with 109/109 tests passing.
