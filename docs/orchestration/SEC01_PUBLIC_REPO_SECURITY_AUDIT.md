# SEC-01 — TradeOps Public Repository Security & Privacy Audit

## State

READY

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/sec01-public-repo-security-audit
```

## Baseline

TradeOps main at slice creation:

```text
7e91388ec81d4b9ae2c27f7cc2d321d56f867088
```

Latest code-bearing integrated baseline:

```text
1444a6ccb023d2545c0b1bf8ba6d52b71e6b6f48
```

Combined VS-13 + VS-14 post-merge CI:

```text
37660656844 — SUCCESS
516 / 516 tests passed
```

Parallel product workstream:

```text
VS-15
TradeOps/vs15-provider-transcript-rebalance-wiring
```

SEC-01 is independent and MUST NOT modify VS-15-owned files.

## Purpose

Perform a repository-wide public-exposure audit of TradeOps now that it is a public portfolio repository.

The audit must determine whether the public repository exposes:

- real credentials or credential-like material;
- private keys/certificates;
- broker/exchange/account identifiers that should remain private;
- client/customer/pilot data;
- personal/private documents or runtime artifacts;
- provider payloads or transcripts that were intended to remain local;
- environment/configuration files containing real values;
- sensitive data in historical branches/commits;
- sensitive data in GitHub Actions logs or artifacts;
- sensitive data in PR/issue bodies;
- personal email addresses in commit metadata;
- other privacy/security material unsuitable for a public portfolio repository.

The worker is an auditor, not a remediation worker.

## Mandatory safety rule

NEVER reproduce a discovered secret or private value in:

- chat;
- audit document;
- commit;
- PR;
- issue;
- console summary.

If a suspected secret is found, report only:

- finding ID;
- severity;
- provider/type;
- file/path or GitHub surface;
- branch/commit/run identifier;
- whether the value appears active-looking, test/synthetic, placeholder, or unknown;
- required remediation.

Mask/redact values completely.

Example:

```text
SEC-001
BLOCKER
Possible exchange API credential
historical commit <sha>, path <path>
value: REDACTED
action: rotate credential first, then rewrite/remove history
```

Do not display even a partial credential unless it is an obviously documented placeholder such as `<YOUR_API_KEY>`.

## Source of truth

GitHub is source of truth.

Use the GitHub connector for repository state, branches, commits, trees, files, PRs and Actions.

Do not substitute local assumptions for live GitHub state.

If a GitHub security endpoint such as secret-scanning alerts is not accessible through the connector, explicitly record that limitation. Do not claim that GitHub secret-scanning reports zero alerts unless the endpoint was actually read.

## Mandatory live verification before audit

Before inspecting content, fetch and record:

- current TradeOps `main` HEAD;
- current DocFlow `main` HEAD for cross-project context only;
- current SEC-01 branch HEAD;
- compare SEC-01 vs current TradeOps `main`;
- current VS-15 branch state and changed-file scope;
- open PR/workstreams;
- current repository visibility;
- total public branch count;
- relevant recent GitHub Actions state.

Verify that SEC-01 branch has not advanced unexpectedly.

## Audit scope

### 1. Current main

Inspect current `main` recursively.

High-risk filenames/extensions include, but are not limited to:

```text
.env
.env.*
secrets.json
appsettings*.json
config*.json
credentials*
secret*
token*
key*
*.pem
*.key
*.pfx
*.p12
*.crt
*.cer
*.db
*.sqlite
*.sqlite3
*.dump
*.sql
*.csv
*.parquet
*.ndjson
*.log
*.zip
*.tar
*.tar.gz
*.pdf
*.doc
*.docx
*.xls
*.xlsx
*.eml
*.mbox
```

Also inspect sensitive directories/terms:

```text
.tradeops/
pilot-evidence/
client
customer
pilot
account
broker
exchange
ibkr
bybit
deribit
kraken
api-key
apikey
authorization
bearer
oauth
token
password
secret
webhook
tls
certificate
transcript
provider
payload
runtime
snapshot
cache
storage
output
artifact
```

Do not classify a filename as a leak by name alone. Inspect whether values are placeholders/synthetic/real-looking.

### 2. Public branch tips

Audit all currently public branch tips, not only `main`.

At slice creation TradeOps had roughly 99 visible branches; fetch the live count.

For each branch tip:

- inspect recursive tree;
- flag risky filenames/extensions;
- inspect branch-specific config/examples/docs likely to contain credentials or private values;
- identify old client/pilot/runtime/prototype branches that materially increase public exposure.

The audit may group branches when they share identical blobs/history, but must not silently skip a distinct high-risk branch.

### 3. Git history / deleted files

A clean current tree is not sufficient.

Check whether sensitive paths were ever committed and later deleted.

At minimum investigate historical commits for:

```text
.env
.env.*
secrets.json
appsettings.Development.json
deploy/client-starter/client-pilot.env
pilot-evidence/
.tradeops/
deploy/tradingview-gateway/tls/
storage/
output/
artifacts/
logs/
transcripts/
credentials/
keys/
certificates/
```

Also search commit messages for clues such as:

```text
secret
credential
token
password
api key
apikey
private
remove secret
cleanup credential
user secrets
client
pilot
account
broker
exchange
```

A commit message is only a lead; inspect the actual changed paths/diff before classifying.

### 4. Secret-pattern scan

Search repository-visible text for common credential patterns and assignments, including:

- OpenAI;
- Groq;
- GitHub tokens;
- AWS access keys;
- JWTs;
- PEM/private-key headers;
- Authorization/Bearer strings;
- API-key/client-secret/access-token/refresh-token/password assignments;
- exchange/broker credential names.

Distinguish:

```text
real-looking / unknown
synthetic test fixture
documented placeholder
variable name only
redaction code/test
```

Do not report placeholder/test strings as real credentials.

### 5. Broker/exchange privacy boundary

TradeOps intentionally contains trading/exchange/broker integration code.

Verify public content does not expose real:

- account numbers/account IDs;
- exchange API keys/secrets;
- subaccount IDs;
- wallet addresses if private operational use;
- IBKR usernames/account identifiers;
- live order IDs tied to a real account;
- client-specific broker configuration;
- production webhook secrets;
- private endpoint URLs;
- personally attributable trading history.

Synthetic/public examples are acceptable when clearly artificial.

### 6. Client/pilot material

Inspect public client-facing and pilot-related branches/files.

Determine whether they expose:

- real client/company names not intended for publication;
- email addresses;
- contact information;
- contract/commercial terms intended to remain private;
- private deployment addresses;
- customer IDs;
- screenshots/logs/evidence from real customers;
- credentials or one-time setup values.

Generic portfolio templates and deliberately public product copy are acceptable.

### 7. Transcript/provider/runtime data

Verify that runtime outputs remain excluded and no real provider/transcript artifacts are committed.

Check especially:

```text
.tradeops/
provider transcript demo runtime outputs
normalized transcript artifacts
structured provider output
raw transcripts
provider request/response dumps
LLM logs
```

Synthetic sample transcripts explicitly designed for the repository are acceptable.

### 8. GitHub Actions

Inspect workflows and representative high-risk historical runs.

Prioritize runs involving:

- broker/exchange smoke;
- Bybit/testnet;
- provider/Groq/OpenAI;
- client/pilot deployment;
- webhook;
- Docker/deployment;
- integration tests using environment variables.

Check logs for:

- secret values;
- environment dumps;
- command-line credentials;
- Authorization/Bearer output;
- real account/client identifiers;
- private absolute paths;
- raw provider/transcript payloads.

Inspect workflow artifacts for sensitive uploaded files when artifacts exist.

Do not assume GitHub masking is sufficient; inspect actual public logs.

### 9. PRs / issues / discussions available through connector

Inspect public PR/issue titles and bodies for private values or client information.

Do not treat ordinary technical architecture/history as a privacy issue.

### 10. Commit metadata

Audit author/committer metadata for ordinary personal/custom email addresses instead of GitHub noreply addresses.

Report:

- number of affected commits;
- commit SHAs;
- dates;
- whether the email is already intentionally public elsewhere.

Do not reproduce the email address in the audit report.

Commit-email exposure is normally privacy severity `MUST FIX` or `ACCEPTABLE WITH CONSENT`, not a credential blocker.

### 11. GitHub-native security controls

Where observable, report whether the repository should enable/verify:

- Secret Protection / secret scanning;
- Push protection;
- Dependency graph;
- Dependabot alerts;
- Dependabot security updates;
- CodeQL default setup.

If settings cannot be read via connector, state `NOT VERIFIABLE VIA CONNECTOR` rather than guessing.

## Severity model

Use exactly these top-level classifications:

### BLOCKER

Public exposure that should trigger immediate containment.

Examples:

- real credential/private key;
- production webhook secret;
- real broker/exchange credential;
- private customer document;
- highly sensitive operational/account data.

Required response should normally include:

1. rotate/revoke credential first if applicable;
2. contain exposure;
3. remove/rewrite Git history if necessary;
4. only then consider repository public-safe.

### MUST FIX

Material public/privacy/security issue that is not an active credential blocker.

Examples:

- unintended personal email metadata;
- real client contact information;
- internal commercial/private client material;
- overly revealing runtime logs;
- unsafe tracked config likely to receive secrets.

### ACCEPTABLE

Potentially sensitive-looking material verified as:

- synthetic;
- placeholder;
- intentionally public;
- public-source data;
- variable names only;
- redaction/security test fixture.

Explain why it is acceptable.

### SAFE TO KEEP PUBLIC

Use only after the audit finds no unresolved BLOCKER or MUST FIX finding.

This conclusion must list limitations.

## Deliverable

SEC-01 must create/update only:

```text
docs/orchestration/SEC01_PUBLIC_REPO_SECURITY_AUDIT.md
```

The final document should contain:

- State;
- audit timestamp/date;
- repository/main HEAD;
- branch HEAD;
- branch count audited;
- audit surfaces covered;
- findings table;
- BLOCKER findings;
- MUST FIX findings;
- ACCEPTABLE findings;
- Actions audit summary;
- history/deleted-path audit summary;
- commit metadata summary;
- GitHub-native security-control visibility/limitations;
- final public-safety verdict;
- exact remediation actions, if any;
- explicit limitations;
- next orchestrator action.

Do not add a generic security framework, scripts, scanners or production dependencies unless the orchestrator explicitly authorizes it.

## File ownership / prohibition

SEC-01 owns only:

```text
docs/orchestration/SEC01_PUBLIC_REPO_SECURITY_AUDIT.md
```

It MUST NOT modify:

- source code;
- tests;
- samples;
- workflows;
- configs;
- README;
- `.gitignore`;
- VS-15 files;
- any other orchestration file;
- DocFlow.

It MUST NOT:

- rotate secrets;
- delete branches;
- rewrite history;
- change repository visibility;
- change GitHub security settings;
- merge PRs;
- push remediation commits outside its audit report.

Remediation is an orchestrator decision after review.

## CI

Because SEC-01 is audit-document-only, CI is not a required correctness gate unless repository path filters trigger it.

If no workflow runs on the final docs-only HEAD, record exactly:

```text
CI: NOT TRIGGERED — audit documentation only
```

Do not modify workflows merely to obtain CI.

## Completion protocol

Before handoff:

1. refetch current TradeOps `main`;
2. refetch current DocFlow `main`;
3. refetch SEC-01 HEAD;
4. refetch VS-15 state;
5. compare SEC-01 vs current TradeOps `main`;
6. verify SEC-01 changed only its owned audit document;
7. confirm no secret values were written to the report;
8. report exact unresolved findings and limitations;
9. do not merge independently.

Stop after the audit report is complete.
