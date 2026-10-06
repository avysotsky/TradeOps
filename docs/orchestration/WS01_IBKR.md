# WS-01 — Interactive Brokers Paper Adapter

State: WAITING_FOR_REAL_PAPER_ACCOUNT

Repository:

```text
avysotsky/TradeOps
```

Branch:

```text
TradeOps/ws01-ibkr-paper-adapter
```

Synchronized baseline for this slice:

```text
main @ 5ae7f0fe3b83c908e3a387f8e81786cf90ecdbed
```

Current implementation HEAD:

```text
b420b327726ec7365fa06da3643a1a733afde8a3
```

Previously integrated read-only adapter:

```text
PR #50
worker HEAD: 78345b1d219ac0924886f61f17cc8b6243682f8d
merge commit: 2c9acfa9a19b1ebe82add786c66656a4458e885d
```

## Completed in this bounded slice

1. Fast-forwarded WS-01 to the actual current main before implementation.
2. Revalidated the integrated IBKR read-only adapter against the current repository.
3. Added an explicit opt-in manual smoke tool:
   `tools/TradeOps.IbkrPaperSmoke`.
4. Added the smoke tool to `TradeOps.sln` so ordinary CI compiles it.
5. Ordinary CI never executes the real CPGW smoke.
6. Added a specific unit test that preserves the IBKR capability boundary:
   - account/position/open-order/order-lookup reads enabled;
   - order placement disabled;
   - order cancellation disabled;
   - private event stream disabled.
7. Existing mutation test continues to require:
   - `PlaceOrderAsync -> NotSupportedException`;
   - `CancelOrderAsync -> NotSupportedException`.
8. Added exact future local CPGW setup, Paper login, runtime environment, PASS output and fail-closed conditions to:
   `docs/ibkr/IBKR_PAPER_ADAPTER.md`.

## Manual smoke safety

The manual harness:

- is restricted to the existing local CPGW base URL:
  `https://localhost:5000/v1/api`;
- requires explicit runtime opt-in:
  `TRADEOPS_IBKR_PAPER_SMOKE_CONFIRM=RUN_PAPER_READ_ONLY_SMOKE`;
- uses the production adapter's `GET /iserver/accounts` path;
- succeeds only when the adapter confirms `isPaper == true`;
- uses runtime-only account selection;
- performs account summary, positions, known-conid resolution, symbol/currency resolution, open-order read and optional order lookup;
- performs no order placement or cancellation;
- prints no account ID, username, credential, cookie, token, balance or authentication material.

No account-specific value or authentication material is committed.

## Future local smoke command

After CPGW is running locally and the user has manually authenticated to a real **Paper** session, from the TradeOps repository root on Windows PowerShell:

```powershell
$env:TRADEOPS_IBKR_PAPER_SMOKE_CONFIRM="RUN_PAPER_READ_ONLY_SMOKE"
$env:TRADEOPS_IBKR_PAPER_ACCOUNT_ID="<paper-account-id>" # optional/runtime only
$env:TRADEOPS_IBKR_PAPER_ACCOUNT_CURRENCY="USD"
$env:TRADEOPS_IBKR_PAPER_SMOKE_SYMBOL="AAPL"
$env:TRADEOPS_IBKR_PAPER_SMOKE_CURRENCY="USD"
$env:TRADEOPS_IBKR_PAPER_SMOKE_CONID="265598"
# $env:TRADEOPS_IBKR_PAPER_SMOKE_EXCHANGE="SMART" # optional
# $env:TRADEOPS_IBKR_PAPER_SMOKE_ORDER_ID="<paper-order-id>" # optional

dotnet run --project tools/TradeOps.IbkrPaperSmoke --configuration Release
```

Actual account/order identifiers must remain local runtime values.

## Expected successful shape

```text
Provider: IbkrPaper
Paper session confirmed: PASS
Account selected: PASS
Account summary retrieved: PASS
Positions count: <number>
Instrument resolution (known conid): PASS <symbol>/<currency> conid=<public-conid>
Instrument resolution (symbol+currency): PASS <symbol>/<currency> conid=<public-conid>
Open orders count: <number>
Order lookup: SKIPPED (no runtime order id supplied)
REAL IBKR PAPER READ-PATH SMOKE: PASS
```

If a runtime Paper order ID is supplied, successful lookup must instead report:

```text
Order lookup: PASS status=<TradeOps-order-status>
```

## Fail-closed conditions

Validation fails if any of these occur:

- explicit opt-in is missing/incorrect;
- local CPGW is unreachable;
- browser/brokerage authentication is missing or expired;
- the session is not Paper / `isPaper != true`;
- requested Paper account selection cannot be resolved;
- account summary or positions cannot be retrieved;
- known conid does not match the requested stock identity;
- symbol/currency/exchange resolution is missing or ambiguous;
- open orders cannot be retrieved;
- supplied order ID cannot be found;
- IBKR placement/cancellation capability is enabled.

## Shared contracts changed

None.

Frozen shared contracts remain unchanged.

## Tests

Implementation CI covers:

- solution restore/build, including `TradeOps.IbkrPaperSmoke`;
- IBKR adapter unit tests;
- explicit read-only capability test;
- existing mutation fail-closed test;
- full existing project smoke/E2E/deployment suite.

## CI

Implementation HEAD:

```text
b420b327726ec7365fa06da3643a1a733afde8a3
```

GitHub Actions:

```text
build #634
run id: 37522571102
conclusion: success
```

## Manual smoke status

```text
real authenticated IBKR Paper/CPGW smoke: NOT RUN
reason: no IBKR/Paper account is currently available
REAL_PAPER_READ_VALIDATED: false
```

No simulated success is recorded.

## Blocker

External dependency only:

```text
A real IBKR account with an available Paper Trading account
+ local Client Portal Gateway
+ manual browser authentication to the Paper session
```

This is not a code/CI blocker.

## Next integration action

Development Orchestrator may integrate the manual-smoke harness/documentation slice if desired.

WS-01 must then remain:

```text
WAITING_FOR_REAL_PAPER_ACCOUNT
```

until a real authenticated Paper smoke actually passes.

Do not begin the mutation slice before that gate. Order placement/cancellation and live IBKR execution remain disabled.
