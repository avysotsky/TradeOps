# TradeOps

C#/.NET trading automation demo based on the TradeOps handoff.

## Current implementation

### Day 1
- `TradeOps.sln`
- `TradeOps.Api`
- `TradeOps.Application`
- `TradeOps.Domain`
- `TradeOps.Infrastructure`
- `TradeOps.Worker`
- domain types: `Order`, `TradingSignal`, `OrderStatus`, `OrderSide`, `OrderType`

### Day 2
- `IExchangeClient`
- `MockExchangeClient`
- `GET /api/account`
- `GET /api/positions`
- `GET /api/orders`

### Day 3
- `POST /api/signals`
- `IRiskEngine` / `RiskEngine`
- `IOrderManager` / `OrderManager`
- risk checks for trading enabled, emergency stop, allowed symbols,
  order size, max positions, projected position size, daily loss state
- `GET /api/risk`
- temporary in-memory daily PnL source (`IRiskState`)

### Day 4
- EF Core + PostgreSQL order persistence
- `TradeOpsDbContext`
- unique index on `Order.ClientOrderId`
- `IOrderRepository` / `EfOrderRepository`
- atomic duplicate protection via PostgreSQL unique constraint
- dedicated `IClientOrderIdGenerator`
- optional caller-supplied `SignalId` for safe request retries
- no blind order retry after timeout
- lookup by `ClientOrderId` after timeout
- `OrderStatus.Unknown` when exchange state cannot be confirmed

### Day 5
- explicit `IOrderStateMachine` / `OrderStateMachine`
- guarded lifecycle transitions (`Submitted`, `Accepted`, `PartiallyFilled`, `Filled`, terminal states)
- filled-quantity invariants for partial and complete fills
- `IOrderReconciliationService` / `OrderReconciliationService`
- reconciliation candidates loaded from PostgreSQL
- identity checks before applying exchange state
- unresolved exchange orders moved to `Unknown`
- invalid exchange/local transitions reported as reconciliation issues instead of silently overwriting local state
- deterministic mock partial fill: 60% on placement, 100% on subsequent reconciliation
- `POST /api/system/reconcile`
- GitHub Actions build for `main` and `TradeOps/**`

## Execution flow

```text
POST /api/signals
        |
        v
 derive deterministic ClientOrderId
        |
        v
 check persisted order
        |
   existing? -------- yes ------> return existing order state
        |
       no
        v
    RiskEngine
        |
   allow/reject
        |
        v
 persist Created order
        |
        v
 OrderStateMachine: Created -> Submitted
        |
        v
 IExchangeClient.PlaceOrderAsync
        |
        v
 Mock: PartiallyFilled (60%)
        |
        v
 persist partial-fill state
```

Then:

```text
POST /api/system/reconcile
        |
        v
 load Submitted / Accepted / PartiallyFilled / Unknown orders
        |
        v
 query exchange by ExchangeOrderId or ClientOrderId
        |
        +---- missing ----> mark Unknown + report issue
        |
        +---- mismatch ---> report issue, do not overwrite local state
        |
        +---- valid ------> OrderStateMachine -> persist exchange state
```

With the current mock, a `PartiallyFilled` order advances to `Filled` during the next exchange lookup used by reconciliation.

## Idempotency rule

For a retry of the same logical trading signal, reuse the same `signalId`. TradeOps derives the same `ClientOrderId` from that signal ID. PostgreSQL enforces uniqueness on `ClientOrderId`, so concurrent duplicate requests cannot create two local execution records.

Example:

```json
{
  "symbol": "BTCUSDT",
  "side": "Buy",
  "quantity": 0.001,
  "source": "manual-demo",
  "signalId": "d0f8625f-2ad4-44eb-a1ec-22acbfbb2e58"
}
```

Expected first execution result with the current mock:

```text
RequestedQuantity = 0.001
FilledQuantity    = 0.0006
Status            = PartiallyFilled
```

After `POST /api/system/reconcile`:

```text
FilledQuantity = 0.001
Status         = Filled
```

## Project references

- Application -> Domain
- Infrastructure -> Application, Domain
- Api -> Application, Infrastructure
- Worker -> Application, Infrastructure

## PostgreSQL configuration

Default development connection string:

```text
Host=localhost;Port=5432;Database=tradeops;Username=postgres
```

Override credentials without committing secrets:

```bash
ConnectionStrings__TradeOpsDb="Host=localhost;Port=5432;Database=tradeops;Username=postgres;Password=YOUR_PASSWORD" dotnet run --project src/TradeOps.Api
```

## EF Core migration

The environment used to prepare this repository does not have the .NET SDK installed, so migrations are not generated blindly. Generate and apply them in a .NET 8 environment:

```bash
dotnet tool install --global dotnet-ef --version 8.*
dotnet ef migrations add InitialOrders \
  --project src/TradeOps.Infrastructure \
  --startup-project src/TradeOps.Api \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/TradeOps.Infrastructure \
  --startup-project src/TradeOps.Api
```

## Local validation

```bash
dotnet restore
dotnet build TradeOps.sln
dotnet run --project src/TradeOps.Api
```

Endpoints:

```text
GET  /health
GET  /api/account
GET  /api/positions
GET  /api/orders
GET  /api/risk
POST /api/signals
POST /api/system/reconcile
```
