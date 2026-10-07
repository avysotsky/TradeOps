# SEC-02 — Commit Metadata Remediation Plan

## State

READY

## Repository / branch

```text
avysotsky/TradeOps
TradeOps/sec02-commit-metadata-remediation-plan
```

## Baseline

TradeOps main at slice creation:

```text
8afc2528eb672a424bbee3635afff2484a7a8fc4
```

Latest code-bearing integrated baseline:

```text
54e981d51bc7de5746371106509cc92030767916
```

VS-15 post-merge CI:

```text
37669861416 — SUCCESS
519 / 519 tests passed
```

Current-main build / CodeQL before SEC-02 creation:

```text
37670318847 — SUCCESS
37670319399 — SUCCESS
```

SEC-01 result:

```text
0 BLOCKER
SEC-002 RESOLVED
SEC-001 remains MUST FIX
```

## Purpose

Produce an exact, low-risk remediation plan for SEC-001: personal/custom author/committer email metadata in three public commits.

This is a **planning-only** workstream.

Do not rewrite Git history.
Do not force-push.
Do not delete branches/tags.
Do not change any production/test/config/workflow file.
Do not expose the email value.

## Known SEC-001 commits

Use live GitHub verification; do not assume this list is complete until checked.

Known from SEC-01:

```text
22555f59291bb4d2df87d55cf71ce227ffd0fad4
a4b06637b33b7c3ee1158352b5db920d65aff948
eb1be147114c9ba7d75d26ecb771b5668c735eff
```

Never reproduce the actual email address.

## Mandatory live verification

Before analysis fetch:

- current TradeOps `main`;
- current DocFlow `main` for context only;
- SEC-02 HEAD;
- compare SEC-02 vs current TradeOps `main`;
- current OP-01 state if recorded;
- all public branch refs;
- open PRs;
- tags;
- current number of public branches;
- current CI on TradeOps main.

Verify the branch has not advanced unexpectedly.

## Required analysis

### 1. Reachability map

For each affected commit determine exactly:

- reachable from current `main` or not;
- reachable from which public branches;
- reachable from which tags, if any;
- referenced by open/closed PR heads or bases where observable;
- whether deleting one stale branch would eliminate exposure for a branch-only commit;
- whether the commit remains reachable through another ref after such deletion.

Produce a per-commit ref matrix.

### 2. Blast radius

For the main-reachable commit, calculate/describe:

- how many current main commits would receive new SHAs after a rewrite;
- which active/recent branches would be invalidated/diverge;
- which merged PR commit links/history would change;
- CI/run references that would remain historical but point to pre-rewrite SHAs;
- expected impact on local clones;
- impact on existing handoff/orchestration documents containing SHAs;
- whether GitHub caches/forks/clones may retain old metadata.

Do not minimize this blast radius.

### 3. Minimal-remediation options

Evaluate at least these strategies:

#### Option A — no history rewrite

- configure GitHub noreply for all future commits;
- accept the three old metadata entries as historical public information;
- document explicit owner acceptance.

State clearly that this does **not** remove existing exposure and therefore does not satisfy strict SEC-001 remediation.

#### Option B — branch-only cleanup + main history rewrite

Determine whether the two branch-only commits can be eliminated by deleting/recreating stale refs while the main-reachable commit is handled by one controlled rewrite.

Specify exact refs affected.

#### Option C — repository-wide all-ref rewrite

Analyze rewriting all relevant public branches/tags with a mailmap/filter-repo strategy.

State whether this is proportionate.

### 4. Tooling recommendation

Prefer a well-established history-rewrite mechanism such as `git filter-repo`.

Design exact commands but DO NOT execute them.

The plan should include:

- fresh mirror clone;
- pre-rewrite backup bundle;
- mapping old email → GitHub noreply email;
- scope: all refs vs selected refs;
- verification commands;
- force-push strategy;
- branch protection considerations;
- local clone recovery instructions;
- post-rewrite GitHub verification.

Do not include the real old email in the committed plan. Use:

```text
<OLD_PRIVATE_EMAIL>
```

For the replacement, using the already-public GitHub noreply identity is acceptable, e.g. the noreply address already visible in current GitHub commit metadata, but avoid unnecessary reproduction if not needed.

### 5. Safety sequence

The final plan must explicitly require:

1. stop/finish active feature workers;
2. freeze merges;
3. record current main/branch/tag refs;
4. create backup bundle;
5. perform rewrite in isolated mirror;
6. verify no code/tree content changed unexpectedly;
7. verify affected email metadata removed from rewritten refs;
8. force-push only after owner approval;
9. re-clone/reset local working copies;
10. update orchestration SHA references where necessary;
11. rerun CI;
12. rerun security audit;
13. only then declare SEC-001 resolved.

### 6. No accidental content rewrite

The remediation must change commit metadata only.

Plan verification should include tree-content equivalence where possible, for example comparing final tree IDs or representative file trees between old and rewritten heads.

Do not alter source content to fix an author email.

## Deliverable

SEC-02 may modify only:

```text
docs/orchestration/SEC02_COMMIT_METADATA_REMEDIATION_PLAN.md
```

The final report must include:

- State;
- audit/planning date;
- main HEAD;
- SEC-02 HEAD;
- affected commits;
- reachability/ref matrix;
- branch/tag/PR impact;
- estimated rewrite blast radius;
- options A/B/C with pros/cons;
- recommended strategy;
- exact non-secret commands;
- preflight checklist;
- execution checklist;
- verification checklist;
- rollback/recovery procedure;
- explicit statement: `NO HISTORY REWRITE EXECUTED`;
- blockers;
- next orchestrator decision.

## Prohibitions

SEC-02 MUST NOT:

- modify source/tests/samples/workflows/configs;
- modify `.gitignore`;
- modify README;
- modify other orchestration files;
- delete branches;
- delete tags;
- rewrite refs;
- force-push;
- merge PRs;
- change repository visibility;
- change GitHub security settings;
- print the actual personal/custom email value.

## CI

This is documentation-only.

If no CI runs on final SEC-02 HEAD:

```text
CI: NOT TRIGGERED — remediation planning documentation only
```

Do not change workflows.

## Completion protocol

Before handoff:

1. refetch current TradeOps main;
2. refetch SEC-02 HEAD;
3. compare branch vs current main;
4. verify changed files are limited to the owned plan document;
5. verify no private email value appears in the document;
6. create a draft PR for orchestrator review only;
7. do not execute remediation.

Stop after the plan.
