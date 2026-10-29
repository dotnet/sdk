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

## Maintenance

Change the workflow source, then regenerate its lock file:

```shell
gh aw compile llm-detection-audit --schedule-seed dotnet/sdk
```

Commit both files. Do not edit `llm-detection-audit.lock.yml` directly.
The harness inventory comes from current CLI code, not a separate list.
Add vendor domains to the network and safe-output allowlists only when needed.
Do not broaden network access or add credentials to resolve missing evidence.
