# Signal audit query

TradeOps exposes a bounded read-only audit query for externally supplied trading signals:

`GET /api/signals`

This endpoint lists persisted `TradingSignals` records. It does not reconstruct transition history and it does not change execution behavior.

## Query parameters

- `symbol` — optional symbol filter; trimmed and normalized to uppercase; maximum 50 characters.
- `outcome` — optional current persisted outcome: `Received`, `Accepted`, or `Rejected`.
- `from` — optional inclusive lower bound on `TradingSignals.CreatedAt`.
- `to` — optional exclusive upper bound on `TradingSignals.CreatedAt`.
- `limit` — result bound; default 50; valid range 1..200.

When both `from` and `to` are supplied, `from` must be earlier than `to`.

Date/time inputs are normalized to UTC before querying PostgreSQL.

Results are ordered deterministically by:

1. `CreatedAt DESC`
2. `Id DESC`

## Time semantics

The audit query uses signal creation/submission time:

`TradingSignals.CreatedAt`

This answers questions such as:

- Which external instructions were received during a period?
- Which BTCUSDT signals were submitted during a period?
- Which submitted signals currently have a rejected outcome?

It is intentionally different from signal-transition metrics, which use:

`TradingSignalOutcomeEvents.OccurredAt`

Do not use this audit endpoint as a substitute for transition-event history or transition-time metrics.

## Historical limitation

`outcome` is the current persisted `TradingSignals.Outcome` projection. A signal that was created in the requested interval and later changed outcome is returned according to its current outcome.

For exact transition timing, use:

`GET /api/signals/{id}/history`

or the separate signal-transition metrics endpoints.

Pre-v1.1.2.12 signals may legitimately have no persisted transition history. TradeOps does not fabricate legacy transition timestamps.

## Examples

Recent signals with the default bound:

`GET /api/signals`

Recent BTCUSDT signals:

`GET /api/signals?symbol=BTCUSDT&limit=50`

Rejected BTCUSDT signals created in a UTC interval:

`GET /api/signals?symbol=BTCUSDT&outcome=Rejected&from=2026-10-01T10:00:00Z&to=2026-10-01T11:00:00Z&limit=100`


## Current execution issue filter

`executionIssueCode` filters the current signal projection, not outcome history.

Example:

`GET /api/signals?executionIssueCode=ClientOrderIdConflict&outcome=Received&limit=50`

This is intended for operator diagnosis of unresolved signals carrying the durable current execution issue projection introduced in v1.1.2.25.

No dedicated index is added in this version; indexing remains evidence-driven.
