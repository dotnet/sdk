# CI Quality Monitor Trigger Coverage

The CI Quality Monitor uses two automatic trigger paths.

## Terms

- **CI** means an Azure DevOps pipeline run triggered by a repository event,
	such as a PR update or a push to `main`. It does not mean this monitor's
	30-minute GitHub Actions schedule.
- **Public PR validation** is definition `101` in
	`dnceng-public/public`. Despite its name, [`.vsts-pr.yml`](../../.vsts-pr.yml)
	defines both its PR and `main` triggers. For a PR targeting `main` or a release
	branch, Azure builds the synthetic `refs/pull/<number>/merge` commit when the
	changed paths match the file's `pr.paths` filters.
- **Public rolling `main` CI** is the same definition `101` and the same
	`.vsts-pr.yml` file, triggered after a push lands on `main`. "Rolling" means
	the branch is validated continuously as changes land. `batch: true` allows
	Azure to combine additional pushes while a build is running; it is not a
	scheduled build.
- **Internal or official CI** is definition `286` in `dnceng/internal`, defined
	by [`.vsts-ci.yml`](../../.vsts-ci.yml). It has separate credentials, pools,
	branch triggers, and an explicit weekly CodeQL/SDL schedule. It is not in this
	monitor's registry.
- **Automated integration PRs** are PRs created to move code or dependencies
	between branches or repositories. This category includes Maestro PRs from
	`dotnet-maestro` with `darc-*` head branches, and GitHub Actions branch-merge
	PRs with `merge/*` head branches and `[automated] Merge branch` titles. A
	failure in this category should trigger analysis, but must be identified as an
	integration-PR failure rather than a stable-branch failure.

| Pipeline scope | `check_suite` | Schedule |
| --- | --- | --- |
| Definition `101` PR validation: setup, checkout, restore, build, test, Helix timeout, or crash | Primary | No |
| Definition `101` PR YAML rejection with a failed Azure build record but no jobs | Primary; a neutral suite is resolved by PR head SHA | No |
| Definition `101` automated integration PR failure | Primary; separate integration-PR category | No |
| Definition `101` push-triggered `main` failure | Emitted by Azure, but currently ignored by the event collector | Primary |
| A new public `main` head for which definition `101` has no build record | No suite can exist | Primary; detected by the heartbeat after two polls |
| Internal or official CI in `dnceng/internal`, including definition `286` | Not scanned | Not scanned |

The registry currently contains only `dnceng-public/public` definition `101` for
`dotnet/sdk`. The check-suite path accepts non-success suites from the
`azure-pipelines` GitHub App, then verifies that the resolved Azure build belongs
to that registered public pipeline and is a PR build. Each PR build attempt is
consumed once before AI runs.

Definition `101` also publishes check suites for push-triggered `main` builds.
The current event collector ignores them only because the initial check-suite
implementation was scoped to PR runs; this is not an Azure DevOps or GitHub
limitation. The 30-minute monitor schedule currently scans those public `main`
builds instead. Changes can land through merge, squash, or rebase; this has
nothing to do with a merge conflict.

## Servicing Merge Example

Automated PR [#55280](https://github.com/dotnet/sdk/pull/55280) merged
`release/9.0.3xx` into `release/10.0.1xx`. Azure validated it before merge as
definition `101` build `1512812`:

- Azure reason: `pullRequest`
- Azure source branch: `refs/pull/55280/merge`
- GitHub head branch: `merge/release/9.0.3xx-to-release/10.0.1xx`
- Result: succeeded

That run used the same `dotnet-sdk-public-ci` definition and `.vsts-pr.yml` jobs
as other public PR validation. It was not a direct branch CI run for
`refs/heads/release/10.0.1xx`. After the PR merged, GitHub created commit
`498dd26f`, but no completed definition `101` branch build was found for that
commit. Therefore, seeing validation on an automated merge PR does not prove
that its target servicing branch has push-triggered CI enabled.

"Azure DevOps never queues a run" means there is no definition `101` build
record for a new `main` head. Possible causes include a disabled or broken
trigger, an Azure/GitHub event-delivery outage, or another trigger-level problem.
It does **not** mean the verified invalid-YAML case: build `1521345` was queued,
Azure recorded a YAML validation error, no jobs started, and Azure published a
neutral check suite.

The schedule remains necessary for the no-build-record heartbeat and as
reconciliation for missed public `main` events. Scheduled collection
intentionally excludes PR builds, so a missed PR check-suite event is recovered
through manual dispatch with `build_id`, not by the schedule. Internal or
official CI under `dnceng/internal` is outside `pipelines.json` and is not
analyzed by either trigger.