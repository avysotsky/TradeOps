# TradeOps handoff — v1.1.2.23

Date: 2026-10-02

## 1. Current state

Branch:

`TradeOps/v_1.1.2.23`

Feature commit:

`df26be78e405118f4255ccf58d1617b7112f8024`

Commit message:

`feat: validate external signal requests`

Final corrective commit:

`50ee941d040854c0694b22f4c3d721965b9d8ea3`

Commit message:

`fix: return signal validation problem details`

GitHub Actions validation:

- workflow: `build`
- run number: **129**
- run id: `36989739166`
- job id: `110782899759`
- result: **success**
- tests: **136 passed / 136 total**
- Build: success
- API + PostgreSQL smoke: success
- Docker Compose validation: success
- API/Worker Docker images: success

This handoff commit is documentation-only and uses `[skip ci]`.

## 2. Scope completed

v1.1.2.23 adds explicit malformed-request validation for:

`POST /api/signals`

Malformed execution instructions now return HTTP **400 Bad Request before persistence or execution**.

This is deliberately separate from:

- HTTP 422 — syntactically/semantically valid signal rejected by trading/risk controls;
- HTTP 409 — existing SignalId reused with a conflicting immutable execution payload;
- HTTP 200 — accepted/idempotently reused execution.

## 3. Validation rules

Before creating a `TradingSignal`, TradeOps validates:

- Symbol is required after trimming;
- Symbol length <= 50;
- Side is a defined `OrderSide`;
- Quantity > 0;
- optional RiskPercent > 0 and <= 100;
- optional StopLoss > 0;
- optional TakeProfit > 0;
- optional Source length <= 100;
- caller-supplied SignalId must not be `Guid.Empty`.

Symbol continues to be normalized by trim + uppercase after validation.

## 4. PostgreSQL precision/idempotency guards

The validator also protects the persisted decimal contracts.

`Quantity`, `StopLoss`, `TakeProfit` map to:

`numeric(28,12)`

Therefore the API rejects:

- more than 12 decimal places;
- magnitude >= 10^16.

`RiskPercent` maps to:

`numeric(18,8)`

and is rejected when it has more than 8 decimal places.

This is not only database-error prevention. It protects SignalId idempotency from silent database rounding: an accepted first request must not persist a numerically changed execution payload that later causes an exact retry to appear different.

## 5. API error contract

Validation failure returns HTTP 400 with `ValidationProblemDetails`.

Title:

`External signal request validation failed.`

Errors are keyed by request field.

No signal execution service call occurs after validation failure.

## 6. Persistence invariant

The real PostgreSQL/API integration test verifies that a sequence of malformed POST requests leaves exactly:

- 0 `TradingSignals`;
- 0 `TradingSignalOutcomeEvents`;
- 0 `Orders`.

This confirms malformed input is rejected before audit persistence and before order execution.

## 7. Risk-rejection semantics preserved

The same integration test then submits a structurally valid request:

- BTCUSDT;
- Buy;
- quantity 0.11.

Default `RiskSettings.MaxOrderSize` is 0.10.

Expected and verified result:

- HTTP 422 Unprocessable Entity;
- 1 persisted `TradingSignal`;
- 2 persisted signal outcome events (Received + Rejected);
- 0 Orders.

Therefore malformed input and business/risk rejection remain deliberately distinct.

## 8. Tests

Added:

`TradingSignalRequestValidatorTests`

Coverage includes:

- valid maximum-shape request;
- blank symbol;
- overlong symbol;
- undefined numeric side;
- zero/negative quantity;
- quantity with excessive decimal scale;
- quantity outside persisted numeric magnitude;
- invalid RiskPercent bounds;
- excessive RiskPercent scale;
- invalid StopLoss;
- invalid TakeProfit;
- overlong Source;
- empty SignalId.

Added:

`SignalRequestValidationApiIntegrationTests.Post_InvalidExecutionInstructions_Returns400BeforePersistence`

This runs through the real ASP.NET Core pipeline and a real migrated PostgreSQL database.

## 9. Files changed

Modified:

- `src/TradeOps.Api/Controllers/SignalsController.cs`

Added:

- `src/TradeOps.Api/Contracts/TradingSignalRequestValidator.cs`
- `tests/TradeOps.UnitTests/TradingSignalRequestValidatorTests.cs`
- `tests/TradeOps.UnitTests/SignalRequestValidationApiIntegrationTests.cs`
- `docs/external-signal-request-validation.md`

No database migration or schema change was added.

## 10. CI result

GitHub Actions #129 validates final commit:

`50ee941d040854c0694b22f4c3d721965b9d8ea3`

Results:

- Build succeeded;
- **136/136 tests passed**;
- request validator theory cases passed;
- PostgreSQL/API pre-persistence validation test passed;
- existing SignalId conflict tests remained green;
- API + PostgreSQL runtime smoke passed;
- Docker Compose validation passed;
- API/Worker Docker images passed.

## 11. Safety/project boundaries preserved

Still in force:

- .NET 8;
- PostgreSQL 16;
- Mock exchange default;
- Bybit Testnet only real adapter;
- no mainnet / real-money support;
- deterministic ClientOrderId;
- no blind order-placement retry after ambiguity;
- no blind cancellation retry after ambiguity;
- no automatic flattening;
- no strategy/alpha generation;
- no return guarantees;
- SignalId immutable execution-intent semantics preserved.

Commercial positioning remains:

`You provide the trading rules. TradeOps provides the execution and automation engineering.`

## 12. Recommended next version

Recommended branch:

`TradeOps/v_1.1.2.24`

Before implementing a new broad API surface, inspect the current execution/operator controllers and choose the next concrete customer-facing execution-safety gap.

Preferred direction:

**pre-existing deterministic ClientOrderId identity hardening in OrderManager.**

Current SignalId conflict hardening protects persisted signals. The next check should determine whether OrderManager can ever reuse an existing local order with the deterministic ClientOrderId without verifying that the order's immutable execution fields match the signal.

If such blind reuse exists, harden at least:

- Symbol;
- Side;
- RequestedQuantity;
- OrderType as applicable.

Required invariant:

- exact deterministic-order retry may reuse the order;
- conflicting local order identity must never be treated as successful execution;
- no exchange placement on conflict;
- duplicate local-order insert races must apply the same identity check.

Design how a conflict discovered after signal audit persistence should be represented before coding; do not silently leave misleading Accepted state.

Do not mix this milestone with batch submission, new metrics, or a new exchange adapter.

## 13. Instruction for next chat

Use this file as the authoritative continuation point:

`TradeOps-Handsoff-v1.1.2.23.md`

Continue from:

`TradeOps/v_1.1.2.23`

Verified implementation SHA:

`50ee941d040854c0694b22f4c3d721965b9d8ea3`

GitHub Actions #129 (`36989739166`) is fully green with 136/136 tests passing.
