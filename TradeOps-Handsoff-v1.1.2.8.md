# TradeOps — Handoff for v1.1.2.8

## 1. Current status

Active development branch:

```text
TradeOps/v_1.1.2.8
```

Stable predecessor:

```text
TradeOps/v_1.1.2.7
predecessor complete branch HEAD used for branching: d806778a645d858f5cfc4a11b9ca52006c9b075c
predecessor authoritative tested code HEAD: 6331d2bf91f2d81d16e8253097cf2b3340787527
```

`v1.1.2.8` tested commits:

```text
abf1c01bc8593f99bca25ec3dedf6d2943199f7d
feat: add fixed-bucket execution metrics series

de53ae92f85ab144a82b7a6cae218d98d4e49c80
test: cover execution metrics series validation
```

Authoritative tested code HEAD:

```text
de53ae92f85ab144a82b7a6cae218d98d4e49c80
```

GitHub Actions #104 is fully green:

```text
Restore                                      ✓
Build                                        ✓
Unit tests, including new series tests       ✓
API + PostgreSQL smoke                       ✓
Existing window/symbol metrics smoke         ✓
Docker Compose validation                    ✓
Docker API/Worker images                     ✓
```

Run metadata:

```text
run number: 104
run id:     36875167414
job id:     110412607006
```

Production commit `abf1c01b...` was also independently validated by fully-green Actions #103 before the test-only commit was added.

This handoff is intended to be added after the tested code HEAD with `[skip ci]` and is documentation-only.

TradeOps remains an execution and automation engineering project. It does not provide alpha, profitable strategies, signals, or return guarantees.

Commercial positioning remains:

```text
You provide the trading rules.
TradeOps provides the execution and automation engineering.
```

---

## 2. Safety and predecessor guarantees preserved

Still in force:

- .NET 8 / PostgreSQL 16;
- `Mock` remains the default exchange provider;
- the only real venue adapter remains `BybitTestnet`;
- no mainnet / real-money support;
- deterministic `ClientOrderId` remains placement identity;
- no blind placement retry after ambiguous outcome;
- no blind cancellation retry after ambiguous outcome;
- persisted order lifecycle history remains unchanged;
- persisted operational-run history remains unchanged;
- `OperationalRunStatuses` remains latest/current projection;
- `OperationalRuns` remains append-per-run history;
- point-in-time execution metrics semantics remain unchanged;
- execution window `[from,to)` semantics remain unchanged;
- optional symbol filtering semantics from `v1.1.2.7` remain unchanged;
- persistent risk state remains unchanged;
- cancellation / EmergencyStop behavior remains unchanged;
- no automatic position flattening;
- no strategy / alpha generation;
- accounting, reconciliation and recovery behavior remains unchanged.

No exchange placement/cancellation implementation, risk-decision logic, recovery orchestration, or strategy code was changed in this milestone.

---

## 3. COMPLETED — fixed time-bucket execution metrics series

New read-only endpoint:

```text
GET /api/metrics/execution/series
    ?from=...
    &to=...
    &bucket=5m
    &symbol=BTCUSDT
```

`symbol` is optional.

Existing endpoints remain unchanged:

```text
GET /api/metrics/execution
GET /api/metrics/execution/window?from=...&to=...&symbol=...
```

Do not merge their meanings:

```text
/api/metrics/execution
    = current point-in-time snapshot

/api/metrics/execution/window
    = one historical persisted-fact aggregate over [from,to)

/api/metrics/execution/series
    = historical persisted facts split into fixed UTC buckets over [from,to)
```

---

## 4. Request validation

Required query parameters:

```text
from
to
bucket
```

Optional:

```text
symbol
```

`from` and `to` are parsed as `DateTimeOffset` and normalized to UTC.

The request interval remains:

```text
[from, to)
```

Invalid or missing `from` / `to`, or `from >= to`, returns HTTP 400.

The accepted bucket whitelist is deliberately small and fixed:

```text
1m
5m
15m
1h
1d
```

Bucket text is trimmed and lower-cased before validation.

Examples:

```text
bucket= 5M  -> 5m
bucket=30m  -> HTTP 400
bucket=2h   -> HTTP 400
missing bucket -> HTTP 400
```

---

## 5. Bucket boundary semantics

Underlying bucket grid is deterministic and aligned to Unix epoch in UTC.

For example, `5m` buckets align to UTC boundaries equivalent to:

```text
... 10:00, 10:05, 10:10, 10:15 ...
```

not to an arbitrary request start.

The request itself still selects only facts satisfying:

```text
timestamp >= from
timestamp <  to
```

The first and/or last returned bucket may therefore be partial when `from` or `to` falls inside an epoch-aligned bucket.

The response bucket boundaries are clipped to the requested interval.

Example:

```text
from   = 10:02
 to    = 10:13
bucket = 5m
```

returns logical response intervals:

```text
[10:02,10:05)
[10:05,10:10)
[10:10,10:13)
```

while fact assignment remains based on the underlying epoch-aligned `5m` grid.

Empty buckets are intentionally returned with zero metrics so chart clients receive a continuous time series rather than having to infer gaps.

---

## 6. Maximum series size

A request may return at most:

```text
500 buckets
```

Exactly 500 buckets is accepted.

A request whose epoch-aligned bucket grid would contain more than 500 buckets returns:

```text
HTTP 400 Bad Request
```

This bound protects response size and avoids accidental requests for extremely dense series.

It is not currently a bound on the number of matching persisted facts inside the requested time range. See the scalability note below.

---

## 7. Symbol semantics

Symbol behavior is inherited from `v1.1.2.7` without change:

```text
null / empty / whitespace -> unscoped series
otherwise -> Trim().ToUpperInvariant()
```

A normalized non-empty symbol longer than 50 characters returns HTTP 400.

The normalized symbol is echoed in the series response.

Signal facts filter directly on persisted:

```text
TradingSignals.Symbol
```

Lifecycle and fill facts use explicit persisted joins:

```text
OrderLifecycleEvents.OrderId -> Orders.Id -> Orders.Symbol
Fills.OrderId                -> Orders.Id -> Orders.Symbol
```

Current `Orders.Status` is never used to reconstruct historical state.

---

## 8. Historical metrics inside each bucket

Each bucket contains the same historical-fact families as the window endpoint.

### Signals

Facts are selected by:

```text
TradingSignals.CreatedAt >= from
TradingSignals.CreatedAt <  to
```

and optionally by `TradingSignals.Symbol`.

Bucket fields:

```text
signals.received
signals.acceptedCurrentOutcome
signals.rejectedCurrentOutcome
signals.pendingCurrentOutcome
```

The same semantic caveat remains: signal outcome transition timestamps are not persisted, so the outcome breakdown is the current persisted outcome of signals whose `CreatedAt` falls inside the bucket/request.

Invariant per bucket:

```text
received == acceptedCurrentOutcome
          + rejectedCurrentOutcome
          + pendingCurrentOutcome
```

### Order lifecycle

Facts are persisted `OrderLifecycleEvents` selected by `OccurredAt`.

Bucket fields:

```text
orderLifecycle.events
orderLifecycle.ordersTouched
orderLifecycle.created
orderLifecycle.submitted
orderLifecycle.accepted
orderLifecycle.partiallyFilled
orderLifecycle.filled
orderLifecycle.cancelled
orderLifecycle.rejected
orderLifecycle.unknown
```

`ordersTouched` is the distinct `OrderId` count inside that individual bucket.

An order appearing in two buckets is counted once in each corresponding bucket; no cross-bucket deduplication is implied.

Invariant per bucket:

```text
orderLifecycle.events == sum(all lifecycle status buckets)
```

### Fills

Facts are persisted `Fill` rows selected by `FilledAt`.

Bucket field:

```text
fillsReceived
```

This remains a persisted Fill-row count, not a count of orders whose current snapshot has a non-zero `FilledQuantity`.

---

## 9. Response model

Top-level model:

```text
ExecutionMetricsSeriesSnapshot
```

Fields:

```text
generatedAt
fromInclusive
toExclusive
bucket
symbol
buckets[]
```

Each `ExecutionMetricsSeriesBucket` contains:

```text
fromInclusive
toExclusive
signals
orderLifecycle
fillsReceived
```

Representative shape:

```json
{
  "generatedAt": "2026-10-01T14:20:00Z",
  "fromInclusive": "2026-10-01T14:00:00Z",
  "toExclusive": "2026-10-01T14:15:00Z",
  "bucket": "5m",
  "symbol": "BTCUSDT",
  "buckets": [
    {
      "fromInclusive": "2026-10-01T14:00:00Z",
      "toExclusive": "2026-10-01T14:05:00Z",
      "signals": {
        "received": 0,
        "acceptedCurrentOutcome": 0,
        "rejectedCurrentOutcome": 0,
        "pendingCurrentOutcome": 0
      },
      "orderLifecycle": {
        "events": 0,
        "ordersTouched": 0,
        "created": 0,
        "submitted": 0,
        "accepted": 0,
        "partiallyFilled": 0,
        "filled": 0,
        "cancelled": 0,
        "rejected": 0,
        "unknown": 0
      },
      "fillsReceived": 0
    }
  ]
}
```

Numbers above are illustrative only.

---

## 10. Repository implementation

`IExecutionMetricsRepository` now additionally exposes:

```text
GetSeriesAsync(
    fromInclusive,
    toExclusive,
    bucket,
    bucketSize,
    symbol = null,
    cancellationToken)
```

`EfExecutionMetricsRepository` was refactored so both window and series paths share the same filtered query builders:

```text
BuildSignalQuery(...)
BuildLifecycleQuery(...)
BuildFillQuery(...)
```

The series implementation does NOT call `GetWindowAsync` once per bucket.

That avoids an N-bucket SQL query pattern.

Instead, for the whole requested `[from,to)` range it executes one filtered minimal-field query for each fact family:

```text
signals   -> CreatedAt, Outcome
lifecycle -> OccurredAt, OrderId, Status
fills     -> FilledAt
```

Only matching rows and required columns are materialized.

They are then assigned to pre-created bucket accumulators in application memory.

`HashSet<Guid>` per bucket supplies distinct `ordersTouched` semantics.

---

## 11. Scalability boundary / known limitation

The API output is bounded to 500 buckets, but `v1.1.2.8` does not yet perform bucket aggregation entirely inside PostgreSQL.

Therefore a wide low-density bucket request can still match a large number of signal/lifecycle/fill rows and materialize those minimal facts into application memory before aggregation.

This is deliberately documented rather than hidden.

The existing PostgreSQL indexes from `v1.1.2.6` and `v1.1.2.7` still bound/filter the source queries efficiently by time/symbol, but they do not eliminate result materialization volume.

No new migration was required in `v1.1.2.8`.

A future performance-hardening milestone should move bucket aggregation to PostgreSQL (for example using PostgreSQL fixed-time bucketing such as `date_bin` or an equivalently deterministic approach) while preserving the exact API semantics defined here.

---

## 12. Database / migration status

No schema change was made.

No migration was added.

Existing useful indexes remain in force, including:

```text
IX_TradingSignals_CreatedAt
IX_TradingSignals_Symbol_CreatedAt
IX_OrderLifecycleEvents_OccurredAt
IX_OrderLifecycleEvents_OrderId_OccurredAt
IX_Orders_Symbol
IX_Fills_FilledAt
IX_Fills_OrderId_FilledAt
```

---

## 13. Tests and CI

Production commit:

```text
abf1c01bc8593f99bca25ec3dedf6d2943199f7d
```

was independently validated by Actions #103:

```text
Restore                     ✓
Build                       ✓
predecessor unit tests      ✓
API + PostgreSQL smoke      ✓
Docker Compose              ✓
Docker API/Worker images    ✓
```

Test-only commit:

```text
de53ae92f85ab144a82b7a6cae218d98d4e49c80
```

adds `MetricsControllerTests` and an API project reference to the unit-test project.

New validation covers:

```text
DateTimeOffset input -> UTC normalization
bucket=" 5M " -> "5m"
symbol=" btcusdt " -> "BTCUSDT"
unsupported / missing bucket -> HTTP 400
501 one-minute buckets -> HTTP 400 and repository not called
exactly 500 one-minute buckets -> accepted
```

Actions #104 is fully green at tested code HEAD `de53ae92...` and also confirms all predecessor PostgreSQL/Compose/Docker behavior remains green.

---

## 14. Files changed

Production commit:

```text
src/TradeOps.Api/Controllers/MetricsController.cs
src/TradeOps.Application/Interfaces/IExecutionMetricsRepository.cs
src/TradeOps.Application/Models/ExecutionMetricsSnapshot.cs
src/TradeOps.Infrastructure/Persistence/Repositories/EfExecutionMetricsRepository.cs
```

Test-only commit:

```text
tests/TradeOps.UnitTests/MetricsControllerTests.cs
tests/TradeOps.UnitTests/TradeOps.UnitTests.csproj
```

No migration files changed.

---

## 15. Definition of Done

- [x] `TradeOps/v_1.1.2.8` created from complete `v1.1.2.7` branch HEAD;
- [x] new read-only `/api/metrics/execution/series` endpoint added;
- [x] `[from,to)` request semantics preserved;
- [x] UTC normalization preserved;
- [x] optional symbol semantics preserved;
- [x] bucket whitelist fixed to `1m`, `5m`, `15m`, `1h`, `1d`;
- [x] bucket text normalized;
- [x] deterministic UTC epoch alignment implemented;
- [x] partial first/last bucket semantics defined;
- [x] empty buckets returned explicitly;
- [x] maximum 500 bucket guard implemented;
- [x] 500 accepted / 501 rejected test coverage added;
- [x] signal persisted-fact semantics preserved;
- [x] lifecycle persisted-fact semantics preserved;
- [x] fill persisted-row semantics preserved;
- [x] current Orders.Status not used for historical reconstruction;
- [x] series avoids one SQL request per bucket;
- [x] no schema migration required;
- [x] production commit independently green in Actions #103;
- [x] final test-inclusive HEAD green in Actions #104;
- [x] PostgreSQL predecessor smoke green;
- [x] Docker Compose green;
- [x] API/Worker Docker images green;
- [x] Mock remains default;
- [x] Bybit remains testnet-only;
- [x] no mainnet introduced;
- [x] no automatic position flattening introduced;
- [x] no strategy/alpha introduced.

`v1.1.2.8` is functionally complete.

---

## 16. Out of scope for v1.1.2.8

Not implemented here:

- PostgreSQL-side bucket aggregation / rollup;
- hard cap on matching raw fact count;
- grouping all symbols in one response;
- arbitrary/custom bucket durations;
- calendar-month buckets;
- signal outcome transition history;
- historical risk-state timeline;
- historical EmergencyStop timeline;
- Prometheus exporter;
- Grafana dashboard;
- materialized metrics rollups / retention policy;
- second exchange adapter;
- Binance integration;
- mainnet / real-money execution;
- position flattening;
- trading strategies / alpha generation;
- ML prediction;
- dashboard/mobile UI;
- SaaS multitenancy/billing;
- Kubernetes/HFT architecture.

---

## 17. Recommended next version

Recommended next branch:

```text
TradeOps/v_1.1.2.9
```

Recommended narrow scope:

```text
Execution-series scalability hardening: PostgreSQL-side fixed-bucket aggregation
```

Goal:

- preserve the exact `/api/metrics/execution/series` contract from `v1.1.2.8`;
- preserve `1m/5m/15m/1h/1d`, UTC epoch alignment, `[from,to)`, empty-bucket and symbol semantics;
- move signal/lifecycle/fill bucket aggregation from raw-fact application materialization into PostgreSQL;
- keep the 500-bucket output guard;
- keep distinct orders-touched semantics per bucket;
- avoid adding grouped-by-symbol output, Prometheus/Grafana, second exchange, or other features in the same milestone.

PostgreSQL 16 provides fixed-time bucketing primitives that can support this, but implementation should be verified against EF/Npgsql behavior before committing to a particular SQL mapping.

After scalability hardening, a later separate milestone can add one-window grouped-by-symbol metrics if useful.

---

## 18. Instruction for the next chat

Start with:

> Continue TradeOps. Read `TradeOps-Handsoff-v1.1.2.8.md` first. `v1.1.2.8` is complete. The authoritative tested code HEAD is `de53ae92f85ab144a82b7a6cae218d98d4e49c80`; GitHub Actions #104 is fully green, and production commit `abf1c01bc8593f99bca25ec3dedf6d2943199f7d` was independently green in #103. New `GET /api/metrics/execution/series?from=...&to=...&bucket=...&symbol=...` supports fixed `1m/5m/15m/1h/1d` buckets, UTC epoch alignment, `[from,to)` selection, clipped partial edge buckets, explicit zero buckets, optional normalized symbol scope and a 500-bucket maximum. Historical metrics remain persisted facts: signals by CreatedAt with CurrentOutcome naming, order lifecycle by OccurredAt, fills by FilledAt; never reconstruct historical status from current Orders.Status. No migration was needed. The current series implementation makes one filtered minimal-field query per fact family and buckets in application memory; this avoids N-bucket SQL calls but can still materialize many facts. Recommended next narrow scope is `v1.1.2.9` PostgreSQL-side series aggregation/scalability hardening while preserving the v1.1.2.8 API contract and all existing Mock-default/Bybit-testnet/no-mainnet/no-flattening/no-strategy boundaries.

No additional context from the previous chat should be required beyond this handoff and repository code.
