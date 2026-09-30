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
- risk checks for trading enabled, emergency stop, allowed symbols, order size, max positions, projected position size, daily loss state
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

### Day 6
- background `TradeOps.Worker` performs restart recovery immediately on startup
- periodic reconciliation loop
- `IExchangeConnectionManager` abstraction for future REST/WebSocket adapters
- reconnect/recovery retry loop with exponential backoff
- `IAlertService` abstraction
- Telegram alert adapter with fail-safe delivery
- structured `ILogger<T>` events for execution and reconciliation
- alerts for risk rejection, submitted orders, partial fills, fills, rejects, unknown states, reconciliation mismatches, disconnects and reconnects
- Telegram secrets are supplied only through configuration/environment variables
- mock exchange IDs are self-describing so a separate Worker process can reconstruct synthetic exchange state after restart

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
        |
        +---- structured log
        +---- optional Telegram alert
```

## Recovery flow

```text
TradeOps.Worker starts
        |
        v
EnsureConnectedAsync
        |
   success / failure
        |
        +---- failure ---> alert once ---> exponential backoff ---> retry
        |
        v
load reconciliation candidates from PostgreSQL
        |
        v
query exchange state
        |
        +---- valid ------> OrderStateMachine -> persist
        +---- missing ----> Unknown + issue
        +---- mismatch ---> issue; do not overwrite blindly
        |
        v
periodic reconciliation
```

With the current mock, a `PartiallyFilled` order advances to `Filled` during the next exchange lookup used by reconciliation. The mock `ExchangeOrderId` contains enough synthetic state for the separate Worker process to reconstruct that mock order after a process restart. This behavior exists only for the demo mock; real exchange adapters will query the external venue.

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

After reconciliation:

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

Use the same connection string for `TradeOps.Worker`.

## Telegram configuration

Telegram alerts are disabled by default. Do not commit a bot token or chat id.

Example environment variables:

```bash
Telegram__Enabled=true
Telegram__BotToken="YOUR_BOT_TOKEN"
Telegram__ChatId="YOUR_CHAT_ID"
```

The Telegram adapter is fail-safe: delivery errors are logged and do not fail the trading execution path.

## Worker configuration

Defaults:

```json
{
  "Worker": {
    "ReconciliationIntervalSeconds": 30,
    "InitialRetryDelaySeconds": 2,
    "MaxRetryDelaySeconds": 30
  }
}
```

The retry delay grows exponentially until `MaxRetryDelaySeconds`.

## EF Core migration

Generate and apply the migration in a .NET 8 environment:

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
```

Run API:

```bash
dotnet run --project src/TradeOps.Api
```

Run recovery Worker in another terminal:

```bash
dotnet run --project src/TradeOps.Worker
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
