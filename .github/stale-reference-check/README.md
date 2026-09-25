# Potentially stale reference discovery

This maintenance workflow finds ignored tests, actionable TODOs, and temporary
workarounds whose referenced GitHub blockers may have been resolved. It creates
revalidation tasks, not claims that tests pass or code can safely be removed.

The [driver](../workflows/stale-reference-check.yml) runs on its configured
target branch. It never creates more than **five** total open tracking issues
(labeled `stale-issue-detection`); once that many are open, filing halts until
a human closes some of them. It never changes source,
opens a pull request, comments on an existing issue, or removes an Ignore.
Generated tracking issues and interpretation caches use the workflow's target
branch rather than assuming `main`.

## Deterministic processing and the agent boundary

1. [`collect.mjs`](collect.mjs) uses tracked-source grep to locate relevant
   constructs and collect numbered context, including URLs on adjacent lines.
2. [`interpretations.mjs`](interpretations.mjs) restores compatible cached
   classifications and selects a bounded batch of new or changed source.
3. The [reusable interpreter](../workflows/stale-reference-interpret.md) identifies
   the actual action, owning declaration/test, and relevant blocking URLs. It
   does not query GitHub or write issues. A host-side submission tool validates
   results against private source snapshots while the agent can still correct
   errors. Recording revalidates against a separate trusted checkout.
4. [`github.mjs`](github.mjs) deduplicates reference lookups and obtains current
   issue/PR states. [`finalize.mjs`](finalize.mjs) checks live tracking issues and
   creates fixed-template tasks. [`workflow.mjs`](workflow.mjs) connects these
   stages, including the route that skips the agent completely.

The compiler's standard threat-detection stage remains enabled. It checks agent
output rather than performing a second semantic investigation of references.
It retains the runtime's `detection` model alias rather than inheriting the
interpreter's pinned model.
Recording requires successful detection and source validation. The compiler's
conclusion job is disabled because this compiler version can otherwise create
diagnostic issues outside the filing limit and preview guard. Native job results,
logs, and the deterministic decision report provide diagnostics instead.

## Scope and bounds

Discovery scans repository-owned source, real tests, scripts, and build files.
Documentation and prompt text, test-input fixtures, snapshots, localization,
generated files, `eng/common`, and manifest-declared vendored files are excluded.
The checker's own synthetic test cases are also excluded from discovery.
The collector is deliberately not a general-purpose parser of every language.

Files without any GitHub issue/PR URL are eliminated before interpretation.
Initial context is 20 lines on either side of a hit. Overlapping ranges are
expanded to include adjacent hits, but each candidate retains its own complete
context; there is no shared-context representation. Each batch contains at most
25 windows and 64 KiB of initial context, including repeated context.

The reader presents that batch as consecutive, labeled text pages rather than
one large batch object. Every text response is at most 12 KiB including the
[pinned MCP adapter's JSON string encoding](https://github.com/github/gh-aw/blob/v0.89.17/actions/setup/js/mcp_handler_process.cjs#L143-L160),
with an explicit next-page number. The adapter can expose escaped newlines even
for string results; native calls and bounded pages eliminate the need for shell
extraction, not the runtime's JSON encoding.
Read all pages: a candidate, or an unusually long source line, can span pages.
Paging does not remove candidates, truncate source, or change the batch artifacts.
Declaration lookup hints give line numbers for nearby syntactic matches, not
proof of ownership or additional source evidence. Use them to target a necessary
context expansion, not to guess a fully qualified name. Non-Ignore anchors do not
require a fully qualified namespace when a stable declaration is already visible.

The interpreter may request up to two additional windows of at most 80 lines
and 10 KiB of source per candidate. A response also reports the remaining
expansion allowance. Cases that cannot be identified confidently within those
bounds are explicitly deferred, not guessed.

Historical references and retained compatibility behavior are not cleanup tasks.
Distinct nearby comments can depend on different URLs. A class-level Ignore
requires complete identification of the affected tests; ambiguous coverage is
deferred. Data rows do not create separate tracking issues.

The interpreter uses native MCP calls, not shell-based CLI proxies. Bash and
file editing are disabled; it has no source checkout, general-purpose Node
execution, or GitHub tools. Native tool schemas define the input arguments,
including the object-valued `payload` for `prepare_interpretations`. The agent
passes the object directly; trusted MCP script code serializes it once for the
private host service. This avoids generating escaped JSON inside another JSON
document, without repairing malformed submissions or weakening validation.
The agent never needs temporary files or shell pipelines to submit its results.
That tool validates the entire batch synchronously, using the same
[`interpretations.mjs`](interpretations.mjs) checks as the recording job. Invalid
shapes, missing candidates, unsupported URLs, and unsupported source claims return
errors while the agent is still running. The host permits three submission
attempts and a 512 KiB serialized-payload limit, with no partial acceptance.
The native schema declares an object; the host validator, not that type
declaration alone, enforces the complete nested result contract.

An accepted payload is frozen in the private host process. The tool returns a
SHA-256 receipt; repeating the identical accepted submission returns the same
receipt, while replacing it is rejected. A trusted post-step inserts the full
validated payload and receipt into `agent_output.json` before the agent artifact
is uploaded, so threat detection inspects the actual results, not just a hash.
It also exports `submission.json` with the context evidence. After successful
threat detection, a trusted recording job downloads the exact agent artifact
that detection inspected and verifies its payload and receipt against
`submission.json` before rerunning validation against committed blobs. Preflight
acceptance and threat detection alone never authorize filing.

The source-specific recorder uses the compiler's native
`safe-outputs.jobs.record-interpretations` custom job. The job preserves the
safe-output boundary by requiring both successful threat detection and a
matching trusted payload; the agent cannot invoke the job directly or write
its result. Because the interpreter workflow does not need a repository-write
safe output such as `create-issue`, the compiler's internal `safe_outputs` and
`conclusion` jobs need no elevated permissions and the disabled `conclusion`
job declares `issues: none`; the interpreter call requires no `issues: write`
grant from the driver.

`gh aw compile` must be run on Linux or macOS for this workflow. On Windows,
a host-path-separator bug in the compiler's secret-redaction validation
(`filepath.Join` instead of `path.Join`) produces backslash artifact paths
that fail the "artifact paths not covered by secret redaction" check even
though the workflow is valid; see
[github/gh-aw#62458](https://github.com/github/gh-aw/issues/62458) and the fix
in [github/gh-aw#62484](https://github.com/github/gh-aw/pull/62484). Always
regenerate `stale-reference-interpret.lock.yml` from a Linux/macOS shell (e.g.
WSL on Windows).

The gh-aw v0.89.17 compiler defaults to MCP gateway v0.4.25, which the protocol
smoke test validates for native clients' stateful fallback initialization. Strict
mode, sandboxing, and permissions remain unchanged. The generated lock pins that
compiler-selected image to an immutable digest.

The interpreter does not pin a model (uses the Copilot CLI default) and permits at most 64 agent turns and
150 AI credits, retaining the 20-minute agent-execution timeout. The generated
job has a separate 60-minute ceiling for setup, execution, and post-processing.
The credit limit is enforced
by the runtime API proxy, not a dollar-cost estimate. These are initial operational
guardrails, not measured performance targets; a hosted run is still needed to
calibrate them after the transport changes.
The bounded reader in
[`source-tools.mjs`](source-tools.mjs) runs on the host; its private input artifact
is outside the agent's filesystem mounts. Only selected batch/context results
cross that boundary. The host enforces the expansion and response-size limits.
Because the pinned
[gh-aw runtime](https://github.com/github/gh-aw/blob/v0.89.17/actions/setup/js/mcp_server_core.cjs)
launches a fresh process per MCP script call, a private loopback reader retains the shared
budget and accepted submission. Source calls only read source; submission calls
validate and retain one payload without GitHub access or issue/cache writes.
A trusted post-agent step exports the served window receipts. Recording verifies
them against source, and the cache retains the verified expansion evidence for
later runners. The private artifact packages both validator and collector modules;
preflight reads the snapshot and does not invoke Git or need a source checkout.

## Fresh runners and interpretation caching

GitHub Actions cache persists validated interpretations between runners. Each
entry is associated with its source identity, entire file blob, and collector/
validation/prompt rule hash. Changing a declaration elsewhere in the file
invalidates the interpretation even when its initial snippet is unchanged.

The cache contains **interpretations, not authoritative GitHub states**. Every
retained actionable reference is checked again on later runs, even when there is
no new context for the agent. Deleted and changed entries are pruned. Deliberately
irrelevant classifications are reusable; incomplete or invalid outputs are not.
Batch continuation prevents one unresolved context from monopolizing discovery.

Cache eviction is safe: the workflow interprets a bounded batch again and still
checks live tracking issues before filing. Corrupt cache data is reported and
discarded. Only the trusted main-branch workflow saves production cache entries;
preview runs do not.

## Eligibility and duplicate protection

Supported references are public GitHub issue and pull-request URLs, including
cross-repository blockers. Fragments and query strings do not produce duplicate
lookups. References through `/issues/` that identify a PR are checked as PRs.
Code links (`/blob/`, `/tree/`), commits, repository homepages, and documentation
may explain a workaround but cannot be submitted as blockers, even alongside a
valid issue/PR. Both synchronous submission validation and final recording reject
the entire batch if any unsupported URL slips through.

An issue qualifies only when closed **as completed**. A PR qualifies only when
**merged**. All identified blockers for an action must qualify. Open/reopened
issues, not-planned or unknown closure reasons, closed-unmerged PRs, unavailable
references, and incomplete duplicate listings cannot authorize filing.

Additional source conditions, such as consuming a fixed dependency version,
remain explicit unverified prerequisites. A tracking task asks the assignee to
check them before changing code.

Durable identity is separate from interpretation-cache identity:

- Ignored test: repository path plus fully qualified test declaration, independent
  of line number, source commit, data rows, and original reference.
- TODO/workaround: repository path, owning declaration/structural anchor, and
  normalized actionable source text, not the surrounding window.

Code creates versioned body markers and visible identity fields. Open issues are
fully paginated and compared locally, without relying on hidden-marker search
indexing, mutable titles, or labels. Conservative checks also recognize existing
unmarked tasks with the same exact test/source identity. A shared upstream URL
alone is not a duplicate.

The workflow serializes its runs, checks again before creation, and reconciles
ambiguous creation errors before any further action. It does not repeatedly file
unchanged tasks already closed by maintainers when matching workflow history is
available. Closed-history lookup uses the existing `agentic-workflows` label;
removing that label from a closed issue can remove this suppression. Open
duplicate detection is not label-dependent.

GitHub does not enforce unique issue-body keys atomically. These checks protect
against this workflow's repeated and concurrent runs, but cannot prevent an
unrelated human or automation from creating the same task at the same instant.

## Filed tasks and Issue Monster

Each issue includes the stable identifier, exact source excerpt, commit-pinned
source link, original references, verified resolution information, and explicit
unknowns. Titles and bodies are generated by code, not by the model.

Issues receive `cookie`, `agentic-workflows`, and `stale-issue-detection`. The
last one marks issues that count toward the total-open-issue filing cap
described above; removing it from an open issue frees a filing slot even
though the issue itself is still open. The existing
[Issue Monster](../workflows/issue-monster.md) scheduled queue handles
assignment, so creation does not depend on an issue event from `GITHUB_TOKEN`
triggering another workflow.

For ignored tests, the task starts by removing the relevant Ignore and following
the [`run-tests` skill](../skills/run-tests/SKILL.md) for the smallest appropriate
selection on the required platform. The test must actually execute. A passing
test can be re-enabled; a newly exposed failure belongs in the tracking task.

## Preview, refresh, and diagnostics

Manually dispatch **Check potentially stale references** from `main`:

- `dry_run: true` is the default. It produces proposed issue payloads and decisions
  without changing issues or the production cache.
- `refresh_cache: true` ignores cached interpretations for collection. Batch
  limits still apply.
- Scheduled runs are live and retain validated interpretations.

The run's `stale-reference-report-*` artifact records created/proposed/skipped
decisions, deferred context, remaining interpretations, and per-batch counts
computed from source-validated results. The recording job also writes
`summary.json` and `runtime.json` beside `interpretations.json`; the agent's
narrative is not a source of counts.
An always-run agent post-step also publishes `runtime.json` in
`stale-reference-runtime-<input-artifact-id>` and the agent job summary, so runtime
failures do not depend on successful output recording for diagnostics. The
recording job recomputes metrics from the complete downloaded agent artifact.

These `runtime.json` artifacts and the decision report's `runtime` field cover
the **interpreter only**, not threat detection.
A separate read-only `runtime_diagnostics` job waits for the interpreter and
detector, including failures, without depending on successful recording. It
downloads their current-run artifacts into separate directories and publishes
`workflow-runtime.json` as `stale-reference-workflow-runtime-<input-artifact-id>`.
It runs no model and changes no detection or filing gates. Successful
finalization includes this report in `workflowRuntime` and the job summary.

[`diagnostics.mjs`](diagnostics.mjs) summarizes the downloaded traces:
request count, models, summed request duration, token/cache usage, AI credits,
permission-denial occurrences, context-budget failures, and rejected submissions.
The workflow usage table separates interpreter and detector requests and reports
their combined usage. It does not add `agent_usage.json` totals to request
totals a second time. Both stages must have usable request logs for a combined
total; missing or skipped detector traces are not assumed to cost zero.
Detector metrics require only its token-usage logs, not interpreter-only MCP or
CLI artifacts. Token fields retain each provider's accounting semantics, and
summed model-request duration is not workflow wall time.
Runtime warnings appear in the decision report and job summaries. Missing,
malformed, or oversized traces produce explicit unavailable metrics, not zeroes.
Model prompt-cache tokens are unrelated to the persistent interpretation cache.
These metrics are observational and never authorize filing; cached-only runs do
not reuse a previous batch's runtime diagnostics.

To summarize separately downloaded interpreter and detection artifacts locally:

```powershell
node .github\stale-reference-check\diagnostics.mjs --workflow .\agent .\detection .\workflow-runtime.json
```

A failure is explicit,
not a successful empty result. API failures do not establish that a blocker was
resolved. Failed/cancelled interpretation jobs cannot authorize the filing job.
Artifact consumers use the successful collection job's artifact ID, rather than
the current attempt number. Partial reruns can therefore reuse successful
producers without searching for an unrelated latest artifact.

For local, deterministic source collection without GitHub access or inference:

```powershell
node .github\stale-reference-check\cli.mjs collect
node .github\stale-reference-check\cli.mjs read-batch
node .github\stale-reference-check\cli.mjs read-batch 2
```

Temporary input, cache, and report files live in the git-ignored
`.stale-reference-check` directory. Collection alone never files issues.

## Development

The helpers use Node built-ins and the Octokit instance from
`actions/github-script`; there is no package installation or SDK build.
Run fixture and mocked-API tests with:

```powershell
node --test .github\stale-reference-check\test\*.test.mjs
```

The [helper test workflow](../workflows/stale-reference-check-tests.yml) runs
these tests on relevant pull requests without invoking the interpreter or
granting issue-write permissions.
It also runs an independent Docker-backed
[`gateway-smoke.mjs`](test/gateway-smoke.mjs) protocol check against the pinned
image. With Docker running (Linux containers), run it locally using:

```powershell
node .github\stale-reference-check\test\gateway-smoke.mjs
```

This check downloads the public pinned gateway image if needed, uses an
authenticated synthetic MCP fixture, and verifies stateless-probe rejection,
legacy session initialization, tool discovery, and source/output-shaped tool
calls. It uses no Copilot PAT, GitHub API, or model inference and removes its
uniquely named container on completion. An optional digest-pinned gateway image
argument allows checking a previous version against the same assertions.
The fast fixture tests remain independent of Docker. The protocol check does not
replace an end-to-end hosted preview of the interpreter.

Edit the interpreter Markdown, never its generated lock file. The checked-in
workflow is compiled with gh-aw v0.89.17 and its matching immutable runtime:

```powershell
gh aw compile stale-reference-interpret --action-mode action --action-tag v0.89.17
```

When changing the compiler/runtime together, regenerate only this workflow and
inspect its job dependencies, artifact handoff, action pins, and permissions.
The caller's Actions-write ceiling is required by the compiler's disabled
conclusion job; every executing interpreter job explicitly uses read-only
Actions permissions, and none can write issues.
The source interpreter uses the repository's existing Copilot PAT pool only for
Copilot authentication; deterministic GitHub operations use the job token.
No new secrets are required.
