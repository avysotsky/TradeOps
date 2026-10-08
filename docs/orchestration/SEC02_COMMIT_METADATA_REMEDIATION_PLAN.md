# SEC-02 — TradeOps Commit Metadata Remediation Plan

## State

READY_FOR_ORCHESTRATOR_REVIEW

Planning date: 2026-10-08

NO HISTORY REWRITE EXECUTED

## Repository / worker branch

    repository: avysotsky/TradeOps
    branch: TradeOps/sec02-commit-metadata-remediation-plan
    initial branch HEAD: 7403983f2d03d06645a4507ef08c7d74a1771a25
    baseline: 8afc2528eb672a424bbee3635afff2484a7a8fc4
    latest code-bearing integrated baseline: 54e981d51bc7de5746371106509cc92030767916
    VS-15 post-merge CI: 37669861416 — SUCCESS
    full suite: 519 / 519 tests passed

SEC-02 is planning-only. This document prepares remediation commands and sequencing but does not execute any rewrite, force-push, branch/tag deletion, ref mutation, merge, repository-visibility change, source/test/config/workflow change, or security-setting change.

## Live GitHub snapshot used for this plan

Observed immediately before drafting this plan:

    TradeOps main: e2a23f455a2ab02d1e14e9520596665edf8ba856
    DocFlow main: be957f139cae0eafff3cd47241a5d7dd6beca855
    SEC-02 branch HEAD: 7403983f2d03d06645a4507ef08c7d74a1771a25
    SEC-02 vs main: diverged; ahead 1; behind 3; merge base 8afc2528eb672a424bbee3635afff2484a7a8fc4
    public branch refs: 103
    tags: 0
    open PRs: 0
    main build: 37757996258 — SUCCESS
    main CodeQL/default-setup signal: 37757996388 — SUCCESS
    SEC-02 initial-head build: 37674290287 — SUCCESS
    main protected flag: false
    repository rulesets observed: none

The ref set changed while SEC-02 was running: VS-16 was created and main advanced. Therefore every execution command below that names refs or counts commits is a planning snapshot, not authority to execute later. Re-enumeration immediately before remediation is mandatory.

## Finding being remediated

SEC-01 found one unresolved MUST FIX finding:

    SEC-001 — personal/custom author/committer email metadata in three public commits

Never place the actual personal/custom email value in this repository, chat, PR, issue, shell transcript, or remediation artifact. Use only:

    <OLD_PRIVATE_EMAIL>

The replacement identity must be the owner's GitHub noreply email and is represented here as:

    <GITHUB_NOREPLY_EMAIL>

Known affected commits:

    22555f59291bb4d2df87d55cf71ce227ffd0fad4
    a4b06637b33b7c3ee1158352b5db920d65aff948
    eb1be147114c9ba7d75d26ecb771b5668c735eff

## Exact reachability / ref matrix

| Affected commit | Current main | Public branches containing it | Tags | Direct commit→PR association | Branch-only removal |
|---|---|---|---|---|---|
| 22555f59291bb4d2df87d55cf71ce227ffd0fad4 | YES | main; TradeOps/sec01-public-repo-security-audit; TradeOps/sec02-commit-metadata-remediation-plan; TradeOps/vs13-provider-transcript-rebalance-e2e; TradeOps/vs14-dotnet-child-process-hardening; TradeOps/vs15-provider-transcript-rebalance-wiring; TradeOps/vs16-rebalance-risk-preview | none | none returned by GitHub | NO — at least main must be rewritten; every retained containing branch must also be rewritten or removed/recreated |
| a4b06637b33b7c3ee1158352b5db920d65aff948 | NO | TradeOps/v_1.1.1.2 only; commit is the branch tip | none | none | YES — delete the stale branch, or recreate/move it to unaffected parent 43774a3ad6a4705c818c09f0732eb2d452be99e0 |
| eb1be147114c9ba7d75d26ecb771b5668c735eff | NO | TradeOps/vs12-provider-selectable-transcript-demo only; commit is the branch tip | none | none | YES — delete the stale branch, or recreate/move it to unaffected parent bee9c880e388e4a219e32edceca4d2578fbfb39e |

Reachability was tested against all 103 public branch refs visible in the snapshot. A branch was classified as containing a commit only when GitHub compare reported the affected commit as an ancestor or identical head.

### Branch-only cases

a4b06637b33b7c3ee1158352b5db920d65aff948 has no other branch/tag/PR reachability observed. Removing or recreating TradeOps/v_1.1.1.2 at its unaffected parent removes ordinary public ref reachability for this commit.

eb1be147114c9ba7d75d26ecb771b5668c735eff was created after VS-12 PR #64 had already merged. PR #64's recorded head is bee9c880e388e4a219e32edceca4d2578fbfb39e, which is exactly the affected commit's parent. Recreating TradeOps/vs12-provider-selectable-transcript-demo at that parent removes the post-merge temporary commit while preserving the PR #64 head lineage.

Deleting/recreating a branch removes repository-ref reachability; it does not guarantee immediate erasure from GitHub caches, dangling-object storage, forks, clones, external mirrors, or previously copied commit URLs.

## Main rewrite blast radius

At the planning snapshot, GitHub compare reports 35 commits ahead of 22555f59291bb4d2df87d55cf71ce227ffd0fad4 on main. Therefore a metadata rewrite of that commit changes the SHA of:

    36 commits reachable in the current main rewrite cone

That count is the affected commit itself plus 35 descendants. It increased during SEC-02 because main advanced while analysis was in progress. It must be recalculated after the merge freeze and immediately before execution.

### Current public branches affected by the main-reachable rewrite

If no stale branches are removed first, every currently retained branch containing 22555f59291bb4d2df87d55cf71ce227ffd0fad4 must be rewritten together:

    refs/heads/main
    refs/heads/TradeOps/sec01-public-repo-security-audit
    refs/heads/TradeOps/sec02-commit-metadata-remediation-plan
    refs/heads/TradeOps/vs13-provider-transcript-rebalance-e2e
    refs/heads/TradeOps/vs14-dotnet-child-process-hardening
    refs/heads/TradeOps/vs15-provider-transcript-rebalance-wiring
    refs/heads/TradeOps/vs16-rebalance-risk-preview

SEC-01, VS-13, VS-14 and VS-15 are integrated/stale workstream branches. SEC-02 is the current planning branch. VS-16 was active while this plan was produced. The safer execution path is to finish active workers, merge/close their work under orchestrator control, then delete stale integrated branches where retention is unnecessary. After that cleanup, recompute reachability. If only main remains, the controlled history rewrite can be reduced to main only.

### Merged PR history impact

The affected commit itself has no direct commit→PR association. However the main rewrite cone includes merge commits for:

    PR #65 — VS-14
    PR #66 — VS-13
    PR #67 — VS-15
    PR #68 — SEC-01

The rewritten repository will have new merge commit SHAs for those historical integrations. GitHub PR conversations, reviews and PR numbers remain, but their historical merge SHA metadata and old commit links may continue to show pre-rewrite objects. GitHub-managed PR refs are not ordinary refs that should be force-pushed by this procedure.

PR #64 is not in the main rewrite cone caused by SEC-001. Its current VS-12 affected commit is a later branch-only temporary commit; recreating the branch at PR #64's recorded head preserves that merged PR history.

### GitHub Actions/history impact

Each affected commit has an existing historical failed build run:

    22555f59291bb4d2df87d55cf71ce227ffd0fad4 -> run 37642309259
    a4b06637b33b7c3ee1158352b5db920d65aff948 -> run 37641825996
    eb1be147114c9ba7d75d26ecb771b5668c735eff -> run 37642062985

History rewrite does not rewrite GitHub Actions run records. Existing run pages, artifacts/log metadata and head_sha fields remain historical references to pre-rewrite SHAs. The remediation must create fresh post-rewrite CI runs on rewritten heads and treat old runs as pre-rewrite evidence, not as validation of rewritten commits.

### Local clone impact

Every local clone that has old main or affected branch ancestry will diverge from rewritten remote history. Normal git pull is not an acceptable recovery method. Preferred recovery is a fresh clone after saving any uncommitted work outside the repository. If a clone must be retained, fetch/prune and hard-reset each rewritten local branch to its rewritten remote branch; delete stale local branches that no longer exist.

### Orchestration SHA references

Current-main orchestration documents containing SHAs in the observed rewrite cone include at least:

    docs/orchestration/OP01_REAL_GROQ_REBALANCE_VALIDATION.md
    docs/orchestration/ORCHESTRATION.md
    docs/orchestration/SEC01_PUBLIC_REPO_SECURITY_AUDIT.md
    docs/orchestration/VS13_PROVIDER_TRANSCRIPT_REBALANCE_E2E.md
    docs/orchestration/VS14_DOTNET_CHILD_PROCESS_HARDENING.md
    docs/orchestration/VS15_PROVIDER_TRANSCRIPT_REBALANCE_WIRING.md

This SEC-02 plan also records pre-rewrite SHAs and will become stale after execution. Do not edit those other files in SEC-02. After the rewrite, use the filter-repo commit map to perform a separate docs-only reconciliation of stale SHA references.

## Strategy evaluation

### Option A — leave existing history

Actions:

1. Configure local/global Git author email to <GITHUB_NOREPLY_EMAIL>.
2. Use the noreply identity for every future commit.
3. Explicitly accept the three historical personal/custom metadata entries as public history.

Pros:

- zero rewrite blast radius;
- no force-push;
- no disruption to clones, PR merge SHAs, CI history or documentation.

Cons:

- the three existing metadata exposures remain public/reachable;
- SEC-001 remains unresolved;
- this option does NOT satisfy strict SEC-001 remediation.

Verdict: acceptable only if the repository owner explicitly accepts the historical privacy exposure and downgrades/waives the finding. It is not a remediation of SEC-001.

### Option B — branch-only cleanup + controlled main rewrite

Actions:

1. Finish active feature workers, especially VS-16, and finish the SEC-02 review lifecycle.
2. Freeze merges and branch creation.
3. Capture exact refs and backup the repository.
4. Remove/recreate the two branch-only affected refs at their unaffected parents.
5. Delete stale integrated branches that no longer need to remain public, if explicitly owner-approved.
6. Re-enumerate every remaining branch/tag containing 22555f59291bb4d2df87d55cf71ce227ffd0fad4.
7. Rewrite the remaining containing refs using a mailmap from <OLD_PRIVATE_EMAIL> to <GITHUB_NOREPLY_EMAIL>.
8. Verify tree/content equivalence and absence of the old metadata.
9. Force-push rewritten refs only after explicit owner approval.
10. Re-clone/reset local clones, reconcile stale SHA docs, rerun CI, then rerun SEC-01.

Pros:

- removes the two branch-only exposures without rewriting unrelated history;
- confines history rewrite to refs that actually retain the main-reachable affected commit;
- can become main-only if integrated/stale containing branches are removed first;
- lower operational blast radius than an all-ref rewrite.

Cons:

- main and every retained descendant ref still receive new SHAs;
- old PR/Actions/cache/fork/clone references may survive outside rewritten live refs;
- requires a coordinated freeze and local clone reset/reclone.

Verdict: RECOMMENDED, subject to owner approval and a fresh frozen-state reachability calculation.

### Option C — repository-wide all-ref rewrite

Actions:

Run git filter-repo with the email mapping across all fetched refs, then force-update every changed public head/tag after verification.

Pros:

- conceptually simple global mailmap rule;
- catches any additional occurrence of the same old email that might exist outside the currently known three commits.

Cons:

- all 103 current public branch refs enter the operational rewrite review surface even though only nine current branch refs are known to retain one of the three affected commits;
- larger force-push/recovery burden;
- greater chance of breaking stale local clones, automation references and historical tooling assumptions;
- GitHub-managed PR history/caches/forks are still outside normal force-push control, so the broader rewrite does not provide perfect erasure.

Verdict: disproportionate for one privacy metadata finding with a precisely mapped three-commit exposure. Use only if the post-freeze audit discovers broader same-email contamination or the owner deliberately chooses a repository-wide identity rewrite.

## Recommended execution runbook — commands prepared, NOT executed

The following is a future operator runbook. Replace placeholders only in the operator's private shell/session. Never commit the actual old email.

### 0. Configure future commit identity

    git config --global user.email "<GITHUB_NOREPLY_EMAIL>"
    git config --global user.name "Oleksandr Vysotskyi"

Verify before creating any future commit:

    git config --global --get user.email

### 1. Fresh isolated mirror and immutable backup

Run only after feature workers are finished and merges are frozen:

    mkdir sec001-remediation
    cd sec001-remediation
    git clone --mirror https://github.com/avysotsky/TradeOps.git TradeOps-rewrite.git
    cd TradeOps-rewrite.git

Capture exact refs before mutation:

    git show-ref --head > ../pre-rewrite-refs.txt
    git for-each-ref --format='%(refname) %(objectname)' refs/heads refs/tags > ../pre-rewrite-public-refs.txt

Create the required recovery bundle:

    git bundle create ../TradeOps-pre-sec001.bundle --all
    git bundle verify ../TradeOps-pre-sec001.bundle

Create a second read-only verification clone from the backup bundle:

    cd ..
    git clone --mirror TradeOps-pre-sec001.bundle TradeOps-before.git
    cd TradeOps-rewrite.git

### 2. Private mailmap input

Create a local file outside the repository history:

    printf '%s\n' '<GITHUB_NOREPLY_EMAIL> <OLD_PRIVATE_EMAIL>' > ../sec001.mailmap

Do not add this file to Git. Do not echo the real old email into CI logs or committed documentation.

### 3. Recompute exact affected refs in the frozen snapshot

Main-reachable root:

    ROOT_MAIN=22555f59291bb4d2df87d55cf71ce227ffd0fad4
    git for-each-ref --format='%(refname)' refs/heads refs/tags | while read ref; do
      if git merge-base --is-ancestor "$ROOT_MAIN" "$ref"; then
        echo "$ref"
      fi
    done | sort > ../main-root-containing-refs.txt
    cat ../main-root-containing-refs.txt

Branch-only roots:

    git for-each-ref --format='%(refname)' refs/heads refs/tags | while read ref; do
      git merge-base --is-ancestor a4b06637b33b7c3ee1158352b5db920d65aff948 "$ref" && echo "a4b06637 $ref"
      git merge-base --is-ancestor eb1be147114c9ba7d75d26ecb771b5668c735eff "$ref" && echo "eb1be147 $ref"
    done > ../branch-only-containing-refs.txt
    cat ../branch-only-containing-refs.txt

The output must be reviewed against the frozen GitHub ref snapshot before proceeding. Any unexpected ref is a STOP condition.

### 4. Branch-only cleanup candidate

Current known unaffected parents:

    A4_PARENT=43774a3ad6a4705c818c09f0732eb2d452be99e0
    EB_PARENT=bee9c880e388e4a219e32edceca4d2578fbfb39e

In the isolated candidate mirror only, model recreation of the stale branches at those parents:

    git update-ref refs/heads/TradeOps/v_1.1.1.2 "$A4_PARENT"
    git update-ref refs/heads/TradeOps/vs12-provider-selectable-transcript-demo "$EB_PARENT"

These local candidate updates are not a GitHub mutation. Actual GitHub branch deletion/recreation remains forbidden until explicit owner approval.

### 5. Build the selected-ref rewrite list

After any owner-approved stale branch cleanup decision, regenerate the retained ref list. Every retained ref that still contains ROOT_MAIN must be included. The current snapshot list is seven refs, but the execution list must come from the frozen repository, not from this document.

Example dynamic preparation:

    git for-each-ref --format='%(refname)' refs/heads refs/tags | while read ref; do
      if git merge-base --is-ancestor "$ROOT_MAIN" "$ref"; then
        echo "$ref"
      fi
    done | sort > ../rewrite-refs.txt
    cat ../rewrite-refs.txt

### 6. Metadata-only rewrite in the isolated mirror

Read the reviewed ref list into the shell and run filter-repo only there:

    mapfile -t REWRITE_REFS < ../rewrite-refs.txt
    git filter-repo --force --mailmap ../sec001.mailmap --refs "${REWRITE_REFS[@]}"

Do not use an unscoped all-ref rewrite for Option B.

### 7. Verification before any push

Verify the old private email no longer exists in reachable commit metadata. The operator substitutes the actual old value only in the private shell:

    OLD_PRIVATE_EMAIL='<OLD_PRIVATE_EMAIL>'
    test -z "$(git log --all --format='%ae%n%ce' | grep -F "$OLD_PRIVATE_EMAIL" || true)"

Verify affected branch-only commits are no longer reachable from candidate public heads/tags:

    test -z "$(git branch --contains a4b06637b33b7c3ee1158352b5db920d65aff948 2>/dev/null || true)"
    test -z "$(git branch --contains eb1be147114c9ba7d75d26ecb771b5668c735eff 2>/dev/null || true)"

Verify exact tree/content equivalence for every rewritten commit using filter-repo's commit map and the backup clone:

    while read old new; do
      [ "$old" = "old" ] && continue
      [ "$new" = "0000000000000000000000000000000000000000" ] && continue
      old_tree=$(git --git-dir=../TradeOps-before.git show -s --format=%T "$old")
      new_tree=$(git show -s --format=%T "$new")
      test "$old_tree" = "$new_tree" || { echo "TREE MISMATCH: $old -> $new"; exit 1; }
    done < filter-repo/commit-map

Verify final head trees against their mapped predecessors where applicable, inspect commit subjects/parents, and review:

    git fsck --full
    git show-ref --head
    git log --all --format='%H %T %an <%ae> %cn <%ce> %s'

The last command must be run only in the private operator session; do not paste output containing the old address into chat/issues/PRs.

### 8. Branch protection / rulesets gate

Current observation is main protected=false and no repository rulesets were returned. This is not authorization to force-push. Immediately before execution, re-read branch protection/rulesets. If force-push is blocked, the owner must explicitly decide whether to temporarily allow it. Restore any temporarily changed protection immediately after the push.

### 9. Owner approval gate and force-push sequence

STOP here until the repository owner explicitly approves the verified candidate and the exact ref list.

For the two branch-only refs, if deletion/recreation is the approved method:

    git push origin --delete TradeOps/v_1.1.1.2
    git push origin --delete TradeOps/vs12-provider-selectable-transcript-demo
    git push origin 43774a3ad6a4705c818c09f0732eb2d452be99e0:refs/heads/TradeOps/v_1.1.1.2
    git push origin bee9c880e388e4a219e32edceca4d2578fbfb39e:refs/heads/TradeOps/vs12-provider-selectable-transcript-demo

For rewritten retained refs, use one force-with-lease push per ref, where OLD_HEAD is taken from pre-rewrite-public-refs.txt:

    git push --force-with-lease=refs/heads/<BRANCH>:<OLD_HEAD> origin refs/heads/<BRANCH>:refs/heads/<BRANCH>

Do not use a blind git push --force --mirror. A lease failure is a STOP condition proving the remote changed after the freeze snapshot.

### 10. Post-push verification

Re-read all public heads/tags from GitHub and verify:

1. neither branch-only affected SHA is reachable from a public branch/tag;
2. 22555f59291bb4d2df87d55cf71ce227ffd0fad4 is not reachable from any retained public branch/tag;
3. rewritten commits use <GITHUB_NOREPLY_EMAIL>;
4. repository trees/content match the verified candidate;
5. no unexpected ref disappeared or moved;
6. old PR/Actions references are classified as historical pre-rewrite references, not live-ref failures.

### 11. Local clone recovery

Preferred:

    move the old local clone aside after saving any uncommitted work
    git clone https://github.com/avysotsky/TradeOps.git

For a clean clone that must be retained:

    git fetch --prune origin
    git switch main
    git reset --hard origin/main

Repeat reset/recreation for every rewritten local branch. Do not merge old local branches into rewritten history.

### 12. Stale SHA reconciliation, CI and re-audit

Using filter-repo/commit-map, update stale SHA references in orchestration documentation in a separate post-rewrite docs commit. At minimum recheck the files listed above and any new orchestration files created after this plan.

Then:

1. rerun the full build/test suite on rewritten main;
2. rerun CodeQL/current security workflows;
3. rerun SEC-01 against all current public branch tips/history surfaces;
4. confirm no unresolved BLOCKER/MUST FIX finding remains;
5. only then mark SEC-001 RESOLVED and reconsider SAFE TO KEEP PUBLIC.

## Repository-wide Option C command shape — NOT recommended / NOT executed

Only if the owner explicitly chooses an all-ref identity rewrite after a new audit:

    git clone --mirror https://github.com/avysotsky/TradeOps.git TradeOps-allref-rewrite.git
    cd TradeOps-allref-rewrite.git
    git bundle create ../TradeOps-pre-allref.bundle --all
    printf '%s\n' '<GITHUB_NOREPLY_EMAIL> <OLD_PRIVATE_EMAIL>' > ../sec001.mailmap
    git filter-repo --force --mailmap ../sec001.mailmap

After verification, push only refs whose object IDs changed, each with an explicit force-with-lease based on the frozen pre-rewrite ref snapshot. Do not blind-force all refs.

## Safety sequence — mandatory

1. Complete active feature workers.
2. Freeze merges and new branch creation.
3. Record exact branch/tag refs and current main.
4. Create and verify a backup bundle.
5. Perform rewrite only in an isolated fresh mirror.
6. Verify tree/content equivalence.
7. Verify removal of <OLD_PRIVATE_EMAIL> from reachable commit metadata.
8. Force-push only after explicit owner approval.
9. Re-clone/reset local repositories.
10. Update stale SHA references in orchestration docs.
11. Rerun CI/security workflows.
12. Rerun the repository security audit.
13. Only after successful re-audit mark SEC-001 resolved.

## Preflight checklist

- [ ] Active feature workers completed; VS-16 no longer moving refs.
- [ ] SEC-02 reviewed by orchestrator.
- [ ] Merge/branch creation freeze announced and observed.
- [ ] TradeOps main, all branch heads, tags, open PRs and rulesets re-fetched live.
- [ ] No unexpected new ref contains any affected commit.
- [ ] Exact main rewrite count recalculated.
- [ ] Backup bundle created and verified.
- [ ] Private mailmap uses <OLD_PRIVATE_EMAIL> -> <GITHUB_NOREPLY_EMAIL>.
- [ ] Actual old email has not been copied to docs/chat/PR/issues.
- [ ] Owner has chosen which stale integrated branches to delete versus retain/rewrite.

## Verification checklist

- [ ] Old metadata absent from all rewritten reachable commits.
- [ ] a4b06637b33b7c3ee1158352b5db920d65aff948 unreachable from public heads/tags.
- [ ] eb1be147114c9ba7d75d26ecb771b5668c735eff unreachable from public heads/tags.
- [ ] 22555f59291bb4d2df87d55cf71ce227ffd0fad4 unreachable from all retained public heads/tags.
- [ ] Commit-map tree equivalence passes for every rewritten commit.
- [ ] Public branch/tag inventory matches intended post-rewrite state.
- [ ] Fresh CI passes on rewritten main.
- [ ] Local clones have been recloned/reset.
- [ ] Stale orchestration SHA references reconciled.
- [ ] SEC-01 rerun reports SEC-001 resolved.

## Rollback / recovery

If verification fails before push: discard TradeOps-rewrite.git and start again from the verified backup bundle/fresh GitHub mirror. No public state has changed.

If a push partially succeeds after approval:

1. keep the merge freeze in force;
2. stop all further pushes;
3. compare remote refs against pre-rewrite-public-refs.txt;
4. restore only the intended refs from TradeOps-pre-sec001.bundle using explicit force-with-lease against the partially rewritten remote heads;
5. verify the restored ref set and CI;
6. investigate the failed candidate before another attempt.

Do not delete the backup bundle until post-rewrite CI, local-clone recovery, SHA-document reconciliation and the SEC-01 rerun are complete.

## Blockers / residual exposure

- Execution is blocked on explicit owner approval; SEC-02 does not execute remediation.
- Active/ref-moving work must stop before any rewrite; VS-16 was active during analysis.
- GitHub PR metadata, Actions history, caches, forks, clones and third-party mirrors may preserve pre-rewrite SHAs or metadata even after live refs are remediated.
- GitHub-native secret/security-control verification remains independent of this metadata remediation.

## Recommendation / next orchestrator decision

Choose Option B.

First finish active work and freeze the repository. Preserve a verified backup. Remove/recreate the two branch-only stale refs. Delete stale integrated branches that do not need retention. Recalculate the main-root containing refs. Rewrite only the retained refs that still contain the main-reachable affected commit. Verify tree equivalence and metadata removal. Obtain explicit owner approval before any force-push. Then recover local clones, reconcile SHA documents, rerun CI and rerun SEC-01.

SEC-02 stops at the plan.

NO HISTORY REWRITE EXECUTED
