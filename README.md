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
- lookup/reconciliation by `ClientOrderId` after timeout
- `OrderStatus.Unknown` when exchange state cannot be confirmed

## Current execution flow

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
 persist Submitted state
        |
        v
 IExchangeClient.PlaceOrderAsync
        |
    success / timeout
        |
        +---- success ----> persist exchange state
        |
        +---- timeout ----> lookup by ClientOrderId
                              |
                         found / not found
                              |
                    persist state / Unknown
```

## Idempotency rule

For a retry of the same logical trading signal, the caller should reuse the same
`signalId`. TradeOps derives the same `ClientOrderId` from that signal ID.
PostgreSQL enforces uniqueness on `ClientOrderId`, so concurrent duplicate
requests cannot create two local execution records.

Example:

```json
{
  "symbol": "BTCUSDT",
  "side": "Buy",
  "quantity": 0.01,
  "source": "manual-demo",
  "signalId": "d0f8625f-2ad4-44eb-a1ec-22acbfbb2e58"
}
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

For a password or different environment, override it without committing secrets:

```bash
ConnectionStrings__TradeOpsDb="Host=localhost;Port=5432;Database=tradeops;Username=postgres;Password=YOUR_PASSWORD" dotnet run --project src/TradeOps.Api
```

## EF Core migration

The current execution environment used to prepare this repository does not have
the .NET SDK installed, so the initial migration is intentionally not generated
blindly. Generate and apply it in a .NET 8 environment:

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
```
