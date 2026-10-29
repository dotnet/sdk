# CLI agent detection audit

[`llm-detection-audit.md`](llm-detection-audit.md) checks the CLI's agent detection
against public upstream source and vendor documentation. It also researches up to
five noteworthy harnesses missing from the detector.

The workflow runs weekly and supports manual dispatch. It runs only in `dotnet/sdk`
on `main`, through the existing
[`Copilot PAT pool`](shared/pat_pool.README.md). Merging the source and generated
lock file makes the workflow available. No additional harness credentials are required.
The model defaults to `gpt-5.6-sol`. The repository's `GH_AW_MODEL_AGENT_COPILOT`
and `GH_AW_DEFAULT_MODEL_COPILOT` variables override that default.

## What the report establishes

Each run inventories the
[`current detection rules`](../../src/Cli/dotnet/Telemetry/LLMEnvironmentDetectorForTelemetry.cs)
and their
[`matching semantics`](../../src/Cli/Microsoft.DotNet.Cli.Definitions/Telemetry/EnvironmentDetectionRule.cs).
It checks each signal's name, value, scope, and propagation to agent-executed commands.
For covered harnesses with public source, it independently inventories exported agent identity signals
and compares them with the SDK rules. This can identify missing alternatives even when
an existing rule is verified.
Investigation follows environment construction, initialization, hooks, and command launch
rather than stopping at an unsuccessful search for the SDK's variable name.
Reports list these gaps separately from the SDK inventory and missing-harness candidates.
Tracker lookup reads identity checks without exposing the previous report's narrative.
The audit compares prior findings only after current research, to reduce anchoring on old source paths and conclusions.
The report distinguishes stable releases from unreleased upstream changes.
It also identifies false-positive risks, unsupported assumptions, and gaps in test coverage.

This is a source-and-documentation audit, not a runtime test of installed harnesses.
Mocked tests prove SDK matching behavior, not that a harness exports a signal.
Closed-source behavior without a public contract remains **Unverifiable**.
The report is **Incomplete** when evidence or discovery coverage is incomplete.
An incomplete report is not evidence that the CLI detector is correct.

## Publication and safety

The workflow creates one `[CLI agent detection] Evidence report` issue with the
`agentic-workflows` label. Later runs replace that issue's body with the full report.
Issue edit history retains earlier reports. Each report identifies its SDK commit,
workflow run, upstream versions, evidence, and limitations.

The workflow locates its issue through the generated workflow marker, bot author,
label, and exact title. Closing the tracker suspends publication until a maintainer
reopens it. Duplicate trackers require maintainer resolution.
Reports also retain a visible workflow marker because issue updates can remove HTML comments.

The agent has read-only GitHub permissions. Separate safe-output jobs create or
update the report. Update permission is restricted by title prefix and label.
The workflow does not install harnesses, run their code, expose environment values,
change CLI behavior, or create pull requests.
The separate [manual fixer](llm-detection-fix.md) can propose a bounded draft PR after maintainer dispatch.

Upstream lookup failures remain visible in the report. Checkout, inventory, and
issue lookup failures stop publication instead of replacing the report with a
success-shaped fallback. The workflow fails visibly when required data or tools
prevent publication, or the agent emits no terminal safe output.
The guard fails the run when the report lacks the visible marker or exceeds the size limit.
Threat detection uses the Copilot engine and blocks publication on failure.
Research is limited to 150 remote reads, ten code
searches, six discovery searches, 160 agent turns, and 45 minutes.

## Pre-merge validation

Validate the source without emitting generated files:

```shell
gh aw compile llm-detection-audit --no-emit --strict --validate
```

For hosted validation, use a separate private repository and a trial workflow copy.
Remove the weekly trigger and production PAT-pool references from that copy.
Restrict the repository guard to the trial host.
Configure its own `COPILOT_GITHUB_TOKEN` secret and pin the SDK checkout to the revision under review.
If the checkout uses a subdirectory, adapt source paths in the trial prompt.
Keep issue reads and safe outputs directed at the trial host.

Run the local copy with `gh aw trial <local-copy.md> --host-repo <owner/test-repo> --yes`.
Do not use the SDK repository as the trial host.
In gh-aw v0.89.21, `--clone-repo` and `--logical-repo` are mutually exclusive.

Check the report issue, not only the workflow conclusion.
A passing run must publish the expected report or emit an intentional no-op.
Check all inventoried signal rows, upstream evidence, incomplete coverage, and the issue update behavior.
For investigation changes, do not supply the expected missing variable or its source path to the trial.
Inspect the execution trace and report to confirm independent discovery, assignment, and propagation evidence.

## Proposing a fix

The [fixer workflow](llm-detection-fix.md) does not run automatically after an audit.
After both workflows reach `main`, a maintainer can dispatch it with a successful audit run ID and one existing harness label:

```shell
gh workflow run llm-detection-fix.lock.yml --repo dotnet/sdk --ref main \
  -f audit_run_id=<successful-run-id> -f harness=opencode
```

The selected run must belong to this repository's main-branch audit workflow.
Its report must still appear in the unique, open, bot-authored tracking issue.
An open detection-fix PR blocks another proposal, regardless of which harness it addresses.
Closing the tracker prevents a new fix and stops publication when validation rechecks it.
This workflow never approves or merges PRs.

The agent rechecks stable upstream source before proposing one harness-prefixed alias.
It must provide distinct assignment and propagation permalinks at the same commit.
The agent's editing tool is disabled, and its prompt prohibits builds and tests in the agent job.
It invokes a [trusted generator](../llm-detection/fix.mjs) that changes only an existing presence-based rule and its regression matrix.
Setup exposes only its `prepare` entry point as `prepare-llm-detection` in the agent's command path.
The wrapper resides in [gh-aw's mounted CLI directory](https://github.com/github/gh-aw/blob/v0.89.21/actions/setup/js/mount_mcp_as_cli.cjs), which the sandbox can read but cannot modify.
The shell allowlist names this command rather than granting general Node execution.
A missing or denied generator is an execution failure, not a successful no-op.
Git access permits local commit preparation, but agent GitHub permissions remain read-only.

An independent job checks the AM patch against a clean checkout of the workflow revision.
It selects an immutable artifact ID from the current attempt and rejects stale artifacts from earlier attempts.
It accepts exactly one added variable and four generated test rows.
All existing rules, labels, matching semantics, and tests must remain unchanged.
It rejects bundles, multiple commits, extra files, mode changes, and transport overrides.
The publisher also enforces two exact allowed paths, a two-file limit, and a 16 KiB patch limit.

The validation job has only read permissions, no protected environment, no supplied secrets, and no persisted checkout credentials.
It builds the patched redist SDK and runs the existing telemetry detection matrix through `scripts/RunTests.cs`.
The generated cases exercise a positive value, an empty value, a non-empty false value, and simultaneous legacy/new signals.
Existing cases retain legacy coverage.
Validation can take up to 90 minutes because the hosted runner needs a complete redist build.
Missing prerequisites, build failures, test failures, or threat-detection failures block PR publication.
Binlogs and test diagnostics remain in the validation artifact for seven days.

The separate publisher requires successful validation and rechecks the artifact without executing its C# code.
It verifies a SHA-256 digest of both the patch and PR metadata against the validated payload.
It rechecks duplicate PRs and refuses publication if `main` advanced during validation.
It can open at most one draft PR, using the restricted safe output.
Source references and an agent's semantic conclusions still require human review.
Structural checks prove the patch's scope, not the truth of upstream claims.
Tool restrictions and prompt instructions do not replace the sandbox or the separate publisher's enforced checks.
Rule removal, exact-value changes, new harness labels, and ambiguous identity signals require a separate human-directed change.

Run the deterministic guard tests locally with:

```shell
node --test .github/llm-detection/fix.test.mjs
gh aw compile llm-detection-fix --no-emit --strict --validate
```

For pre-merge hosted testing, adapt the fixer only in a private trial repository.
Use that repository's successful audit and tracker, its own credential, and a pinned SDK revision with the trusted fixer files.
Do not grant write credentials to the validation job or dispatch the production fixer from a feature branch.
Local guard and compilation checks do not prove a hosted build or draft-PR publication succeeded.
For a known-gap trial, require a proposal instead of accepting a no-op.
Confirm the SDK build and telemetry matrix ran successfully.
Inspect the resulting private draft PR and its two-file diff.

## Maintenance

Change the workflow source, then regenerate its lock file:

```shell
gh aw compile llm-detection-audit --schedule-seed dotnet/sdk
gh aw compile llm-detection-fix
```

Commit each changed source with its generated lock file. Do not edit lock files directly.
The harness inventory comes from current CLI code, not a separate list.
Add vendor domains to the network and safe-output allowlists only when needed.
Do not broaden network access or add credentials to resolve missing evidence.
