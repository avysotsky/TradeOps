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

## Current execution flow

```text
POST /api/signals
        |
        v
   OrderManager
        |
        v
    RiskEngine
        |
   allow/reject
        |
        v
 IExchangeClient
        |
        v
MockExchangeClient
```

## Project references

- Application -> Domain
- Infrastructure -> Application, Domain
- Api -> Application, Infrastructure
- Worker -> Application, Infrastructure

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

Example signal:

```json
{
  "symbol": "BTCUSDT",
  "side": "Buy",
  "quantity": 0.01,
  "source": "manual-demo"
}
```

## Important temporary limitation

The Day 3 client order ID is derived from the signal ID. Day 4 should replace
this with a dedicated client-order-id generator, persistence, duplicate
protection and safe timeout reconciliation.
