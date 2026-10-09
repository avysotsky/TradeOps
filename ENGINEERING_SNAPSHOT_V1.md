# TradeOps v1 — Public Engineering Snapshot

## Status
**PUBLIC / APPLICATION DEVELOPMENT FROZEN.** Archive/read-only status is controlled by GitHub repository settings, not this static report.

This is a historical **engineering portfolio snapshot**, not an actively maintained product or a live trading service. This repository will remain at `https://github.com/avysotsky/TradeOps`; the repository name, path, individual file/commit URLs and existing issue/PR URLs are not intended to change. Future proprietary development is private. The active private code and deployment credentials are not published here.

- **Frozen production source commit:** [`77229d7f4f835b8cf43eb0aeae427d2df509bb11`](https://github.com/avysotsky/TradeOps/tree/77229d7f4f835b8cf43eb0aeae427d2df509bb11)
- **Validated public GitHub Actions:** [build `37810268475`](https://github.com/avysotsky/TradeOps/actions/runs/37810268475), **SUCCESS**.
- **Public unit test result:** **576 / 576 passed**, 0 build warnings, 0 errors in that CI run.
- GitHub Actions log and artifact retention follows platform policies; these recorded results are a dated snapshot, not a continuous green-build claim.

## What reviewers can inspect
- C#/.NET 8 application/domain/infrastructure boundaries, typed APIs, dependency injection, persistence and integration tests.
- External-signal input validation and signed webhook audit boundary; isolated testnet/mock adapters.
- ResearchDecision → backtest/rebalance → risk preview → execution dry-run boundary. Operator-preview results derived from caller-supplied snapshots are **not live risk authorization or order placement**.
- DocFlow HTTP extraction application port, bounded client, opt-in service registration and offline negative tests.
- PostgreSQL-backed API smoke, CI and Docker smoke tests; historical PRs/commits documenting change control and review.
- Public mock/example data, not private accounts or commercial strategy alpha.

## Important limitations
- No profitability, real-exchange execution, credentials, or investment-performance assurances.
- Archived/read-only source code should not be deployed to real money trading without your own security/compliance review.
- A source snapshot is not a license grant; any permitted reuse is governed by existing applicable license terms and law. Do not assume all visible code is public-domain.
- Runtime services should not expose unauthenticated internal DocFlow extraction over public networks.
- For live operational claims or current implementation status, this frozen v1 must not be treated as the latest commercial version.

## Permanent-link guidance
Existing GitHub links point to this repository and remain meaningful after archival:
- [Repository](https://github.com/avysotsky/TradeOps)
- [Frozen source tree](https://github.com/avysotsky/TradeOps/tree/77229d7f4f835b8cf43eb0aeae427d2df509bb11)
- [Historical PRs](https://github.com/avysotsky/TradeOps/pulls)
- [CI evidence](https://github.com/avysotsky/TradeOps/actions/runs/37810268475)

GitHub repository metadata is authoritative for its archived/read-only status. Read-only archival does not require renaming, deleting or privatizing this repository.
