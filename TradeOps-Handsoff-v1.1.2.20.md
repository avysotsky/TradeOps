# TradeOps handoff — v1.1.2.20

Date: 2026-10-01

## 1. Current state

Branch:

`TradeOps/v_1.1.2.20`

Feature commit:

`fd9d0412a3cdf8601e9bb6626108c930e72c9c0d`

`feat: add searchable signal audit`

Compatibility corrective commit:

`bf10ad45f09dc9cb85178dbe0812af2871aba387`

`fix: preserve operator read compatibility`

Final verified commit:

`b3a1d16917a819c6702d97e50475ace49e05580d`

`test: align signal audit json assertions`

The final corrective commit changes only API integration-test assertions from `id` to the existing public response property `signalId`; production behavior did not change.

GitHub Actions:

- workflow: `build`
- run number: **122**
- run id: `36907217186`
- job id: `110520659360`
- result: **success**
- tests: **107/107 passed**
- Build: **0 warnings / 0 errors**
- API + PostgreSQL smoke: success
- Docker Compose validation: success
- API/Worker Docker images: success

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed

v1.1.2.20 turns the existing signal-list endpoint into a bounded searchable operator audit surface without adding a new route.

Endpoint remains:

`GET /api/signals`

Supported query parameters:

- `symbol` — optional, trimmed + uppercased, maximum 50 characters;
- `outcome` — optional current persisted `SignalOutcome` (`Received`, `Accepted`, `Rejected`);
- `from` — optional inclusive lower bound on `TradingSignals.CreatedAt`;
- `to` — optional exclusive upper bound on `TradingSignals.CreatedAt`;
- `limit` — default 50, valid range 1..200.

Validation:

- `limit < 1` or `limit > 200` => HTTP 400;
- symbol longer than 50 => HTTP 400;
- invalid enum model binding => HTTP 400;
- when both bounds are present, `from >= to` => HTTP 400.

Date/time inputs are normalized to UTC.

Results are ordered deterministically by:

1. `CreatedAt DESC`;
2. `Id DESC`.

## 3. Time semantics

This endpoint is a signal-submission/current-projection audit query.

Time axis:

`TradingSignals.CreatedAt`

The `outcome` filter is the current persisted `TradingSignals.Outcome` projection.

This is deliberately separate from transition history/metrics, which use:

`TradingSignalOutcomeEvents.OccurredAt`

Do not reinterpret the signal audit as transition-time history.

## 4. Repository compatibility

`IOperatorReadRepository` retains the original method:

`GetSignalsAsync(int limit, CancellationToken)`

A filtered overload was added for the new API query. The interface provides a compatibility fallback to the old overload so existing fakes/consumers are not source-broken.

`EfOperatorReadRepository` implements both overloads; the old overload delegates to the filtered implementation with null filters.

The repository defensively clamps the limit to 1..200 even though the API validates it.

## 5. Database/index decision

No schema change and no migration were required.

Existing `TradingSignals` indexes already cover the primary audit shapes, including:

- `CreatedAt`;
- `Symbol`;
- `(Symbol, CreatedAt)`.

No speculative index was added for `Outcome`.

## 6. Tests added

### `SignalAuditPostgresTests`

Validates against a real migrated PostgreSQL database:

- `[from,to)` half-open creation-time filtering;
- symbol normalization;
- current-outcome filtering;
- deterministic ordering;
- limit behavior;
- exact exclusion of rows before `from` and at `to`.

### `SignalAuditApiIntegrationTests`

Runs the real ASP.NET Core pipeline through `WebApplicationFactory<Program>` against isolated PostgreSQL.

Validates:

- `symbol= btcusdt ` normalization;
- `outcome=Accepted` binding/filtering;
- UTC normalization of `+02:00` request timestamps;
- result ordering and limit;
- `limit=0` => 400;
- `limit=201` => 400;
- symbol length 51 => 400;
- invalid outcome enum => 400;
- equal `from`/`to` => 400;
- existing public JSON contract uses `signalId`.

## 7. Documentation

Added:

`docs/signal-audit-query.md`

It documents query parameters, validation, deterministic ordering, creation-time semantics, and the difference from transition history.

## 8. Files changed

Modified:

- `src/TradeOps.Application/Interfaces/IOperatorReadRepository.cs`
- `src/TradeOps.Infrastructure/Persistence/Repositories/EfOperatorReadRepository.cs`
- `src/TradeOps.Api/Controllers/SignalsController.cs`

Added:

- `tests/TradeOps.UnitTests/SignalAuditPostgresTests.cs`
- `tests/TradeOps.UnitTests/SignalAuditApiIntegrationTests.cs`
- `docs/signal-audit-query.md`

No production schema/migration file changed.

## 9. CI result

Final validation on `b3a1d16917a819c6702d97e50475ace49e05580d`:

- Build succeeded;
- 0 warnings;
- 0 errors;
- 107/107 tests passed;
- `SignalAuditPostgresTests` passed;
- `SignalAuditApiIntegrationTests` passed;
- existing transition metrics/history/query-plan tests remained green;
- API + PostgreSQL smoke passed;
- Docker Compose passed;
- API/Worker image builds passed.

The legacy main workflow still emits GitHub's external Node.js 20 deprecation warning for `actions/checkout@v4` and `actions/setup-dotnet@v4`. It is not a TradeOps compiler warning.

## 10. Historical limitation remains

Transition history began in v1.1.2.12 without synthetic backfill.

Pre-v1.1.2.12 signals may have a current terminal outcome and no persisted transition events.

The new audit endpoint must not reconstruct transition timestamps from `CreatedAt` or current outcome.

## 11. Safety/project boundaries remain

Still in force:

- .NET 8;
- PostgreSQL 16;
- Mock exchange default;
- Bybit Testnet only real venue adapter;
- no mainnet / real-money support;
- deterministic `ClientOrderId`;
- no blind placement retry after ambiguity;
- no blind cancellation retry after ambiguity;
- no automatic flattening;
- no strategy/alpha generation;
- no return guarantees;
- execution, reconciliation, recovery and persistent risk semantics unchanged.

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 12. Suggested next version

Recommended next branch:

`TradeOps/v_1.1.2.21`

Before implementation, inspect the current local-order operator surface. The likely next product-facing gap is a bounded searchable order audit/list endpoint or equivalent extension if only point lookup/history/cancel routes exist.

Prefer filters useful for operator diagnosis (for example symbol, status, creation window and bounded limit), using existing persisted local order state.

Do not add a feature until the current order routes/repository/indexes are inspected.

Do not mix this with strategy logic, transition metrics, or speculative indexing.

## 13. Instruction for next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.20.md`

Continue from:

`TradeOps/v_1.1.2.20`

Verified final implementation SHA:

`b3a1d16917a819c6702d97e50475ace49e05580d`

GitHub Actions #122 (`36907217186`) is fully green with 107/107 tests passing.
