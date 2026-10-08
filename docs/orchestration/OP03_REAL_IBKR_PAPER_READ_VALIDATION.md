# OP-03 — Real IBKR Paper Read-Path Validation

## State

WAITING_EXTERNAL

## Date opened

2026-10-08

## Repository baseline

```text
TradeOps main: 4a1f6b4448c1af96db8a05008c1f071425b3a0b6
post-rewrite build: 37767816902 — SUCCESS
post-rewrite CodeQL: 37767816589 — SUCCESS
full suite: 537 / 537 tests passed
warnings: 0
```

## Purpose

Validate the already integrated Interactive Brokers Paper read-only adapter against a real authenticated Client Portal Gateway Paper session when a real IBKR Paper account becomes available.

This is an operator validation only.

No order placement, cancellation, persistence or broker mutation is permitted.

## Existing harness

```text
tools/TradeOps.IbkrPaperSmoke
```

The harness:

- requires an explicit runtime opt-in;
- is restricted to the local Client Portal Gateway base URL:
  `https://localhost:5000/v1/api`;
- confirms the session is Paper before continuing;
- reads account summary;
- reads positions;
- resolves a known public stock conid;
- resolves symbol/currency through IBKR;
- reads open orders;
- optionally reads one Paper order by runtime-only ID;
- refuses to run if TradeOps IBKR placement/cancellation capability is enabled.

## External prerequisite

A real IBKR account with Paper Trading access is required.

Interactive Brokers Client Portal Gateway must be running locally and manually authenticated in a browser on the same machine.

Authentication must not be automated or committed.

## Local operator setup

1. Verify Java:

```powershell
java -version
```

2. Download and unzip the current Client Portal Gateway from the official Interactive Brokers documentation.

3. From the unzipped `clientportal.gw` directory run:

```powershell
bin\run.bat root\conf.yaml
```

4. Open in a browser:

```text
https://localhost:5000
```

5. Accept the localhost certificate warning if shown.

6. Log in manually to the IBKR **Paper** session.

Do not paste IBKR credentials, cookies, tokens, account IDs, usernames or session payloads into ChatGPT or repository files.

## TradeOps operator command

From the TradeOps repository root, in the same Windows user session:

```powershell
$env:TRADEOPS_IBKR_PAPER_SMOKE_CONFIRM="RUN_PAPER_READ_ONLY_SMOKE"
$env:TRADEOPS_IBKR_PAPER_ACCOUNT_CURRENCY="USD"
$env:TRADEOPS_IBKR_PAPER_SMOKE_SYMBOL="AAPL"
$env:TRADEOPS_IBKR_PAPER_SMOKE_CURRENCY="USD"
$env:TRADEOPS_IBKR_PAPER_SMOKE_CONID="265598"

# Optional, runtime-only:
# $env:TRADEOPS_IBKR_PAPER_ACCOUNT_ID="<paper-account-id>"
# $env:TRADEOPS_IBKR_PAPER_SMOKE_EXCHANGE="SMART"
# $env:TRADEOPS_IBKR_PAPER_SMOKE_ORDER_ID="<paper-order-id>"

dotnet run --project tools/TradeOps.IbkrPaperSmoke --configuration Release
```

## Expected successful output shape

```text
Provider: IbkrPaper
Paper session confirmed: PASS
Account selected: PASS
Account summary retrieved: PASS
Positions count: <number>
Instrument resolution (known conid): PASS AAPL/USD conid=265598
Instrument resolution (symbol+currency): PASS AAPL/USD conid=<public-conid>
Open orders count: <number>
Order lookup: SKIPPED (no runtime order id supplied)
REAL IBKR PAPER READ-PATH SMOKE: PASS
```

If a runtime-only Paper order ID is explicitly supplied, the optional lookup may instead print:

```text
Order lookup: PASS status=<TradeOps-order-status>
```

## Safety boundary

The following remain disabled:

```text
IBKR order placement
IBKR order cancellation
IBKR private event stream
broker mutation from RebalanceOrderIntent
```

A successful OP-03 does not itself authorize order placement.

It only removes the external read-path validation blocker.

## Privacy

The operator must not share:

- IBKR username;
- account ID;
- cookies;
- tokens;
- balances/equity if personally sensitive;
- live order IDs;
- authentication/session payloads.

For handoff, only the bounded PASS/FAIL console lines are required.

## Pass criteria

All of the following must be true:

- Client Portal Gateway is reachable locally;
- browser authentication is active;
- session is confirmed Paper;
- account selection succeeds;
- account summary succeeds;
- position read succeeds;
- known-conid resolution succeeds;
- symbol/currency resolution succeeds;
- open-order read succeeds;
- mutation capability remains disabled;
- final line is:
  `REAL IBKR PAPER READ-PATH SMOKE: PASS`.

## Failure handling

Do not weaken the adapter or enable mutation to make the smoke pass.

Classify failures as one of:

- gateway not installed/running;
- Java/runtime problem;
- browser authentication missing/expired;
- non-Paper session;
- competing brokerage session;
- account selection mismatch;
- account endpoint failure;
- instrument resolution failure;
- open-order endpoint failure;
- network/localhost TLS issue;
- TradeOps adapter defect.

## Completion

After operator handoff:

1. record only bounded non-sensitive PASS/FAIL output;
2. update this file to DONE only after a real Paper session passes;
3. update orchestration;
4. only then consider a separate mutation-enablement workstream.

Do not enable or test real order placement in OP-03.


## External blocker status

As of 2026-10-08, the operator does not have an available IBKR Paper account/session.

Installing Java or Client Portal Gateway alone would not satisfy this gate, because meaningful account/read-path validation requires an authenticated brokerage Paper session.

Status remains:

```text
WAITING_EXTERNAL
```

Development may continue through non-mutating execution dry-run/audit slices that do not depend on IBKR.
