# Attribution and confidence

## Independent axes

| Attribution | Required support |
| --- | --- |
| PR-related / New-commit-related | A supported link from changed behavior to the observed mechanism. Executed comparable controls strengthen it. Intermittency does not exclude it. |
| Pre-existing | The same material mechanism in comparable evidence without the PR changes or before the stated standalone window. Note unresolved exacerbation by new changes. |
| External infrastructure | Evidence of an external CI service, agent, queue, or transport mechanism, after accounting for changed source/configuration that could produce similar symptoms. |
| Inconclusive | Missing controls, incompatible runs, conflicting signals, incomplete identity/coverage, or no causal link. State the next discriminating check. |

Behavior is **Intermittent**, **Deterministic**, or **Unknown**. Use actual execution
history or a specific deterministic diagnostic. Repeated failures alone do not
prove determinism. Mixed outcomes in candidate controls alone do not establish
equivalent conditions or prove a flake is unrelated.

`Pre-existing` is relative to the stated comparison window. If build B already
failed before new commits in build C, that mechanism predates B-to-C. An older
passing A may bound an A-to-B regression; it does not implicate the C batch.

## Qualitative confidence

Use **High/Medium/Low only**, accompanied by evidence and limitations. Do not
produce percentages, numeric scores, or invented probabilities.

- **High:** specific diagnostics/artifacts or strongly comparable controls support
  the assessment, and material alternatives have been checked.
- **Medium:** supporting evidence forms a coherent assessment, but an important
  causal link, control, or alternative remains unresolved.
- **Low:** sparse, conflicting, or weakly comparable evidence. Use Inconclusive
  when it cannot support attribution; a low-confidence guess is not a finding.

Confidence in the proximate symptom can exceed confidence in its underlying
cause. For example, the watchdog clearly terminated a hung host, but its reason
for hanging may remain unknown. State both instead of attaching High confidence
to an unsupported product/infrastructure diagnosis.

Evidence coverage is a separate concern. Complete collection is not proof, and
one well-understood failure does not excuse other unexplained failures.

## Comparability and change windows

Verify repository, organization/project, pipeline and actual job/scenario, OS/RID,
configuration, queue, test selection/shard, resolved dependencies/toolchain,
pipeline parameters/resources, and tested revisions. Different build reasons may
select different jobs/test modes even in the same pipeline; see the SDK
[CI definition](../../../../.vsts-ci.yml).

For PRs, establish the tested PR head, merge tree, and historical target SHA.
Compare against the actual tested baseline, not today's branch head. If source
and target moved together, neither is automatically responsible.

For standalone builds, verify the preceding candidate scenario execution and the
last actual passing control separately. Honor explicit baselines only after
comparability and ancestry checks. Do not use build finish order to infer ancestry.
The collector's candidate limitations require investigation, not automatic trust.

Rolling builds may contain many commits. Diff the tested range and trace a specific
change to the failure before naming an introducing commit. Otherwise retain the
range and acknowledge uncertainty.

For same-SHA scheduled/manual/repeated runs, report no new source commits.
Investigate resolved dependencies, external resources, parameter changes, machine
state, and latent source defects. An empty source diff does not prove infrastructure.

## Evidence traps

- Known-issue matches are evidence of matching history, not immunity for the PR.
  Inspect mechanism, chronology, branch/configuration, and potential exacerbation.
- Build Analysis must cover the exact build. An overridden green check can retain
  unmatched failures. A partial report cannot establish zero matches.
- An absent branch Build Analysis report is a coverage gap/not-applicable state,
  not proof the branch has no known problems.
- Never infer a pass from missing failed results, test-run summary counts, a skip,
  or overall build success without verified scenario execution.
- Synthetic "Work Item" results, exit codes, and assembly names are not named
  assertion failures or ownership evidence. Recover logs/results when available.
- Restore/network-looking failures can follow PR changes to URLs, credentials,
  dependency versions, or configuration. Inspect those changes before assigning
  external infrastructure.
- Distinguish CI infrastructure from external dependency and configuration
  failures. A feed HTTP 401/403, DNS failure for a package source, expired
  credential, package vulnerability rejection, or invalid external service
  configuration is not by itself evidence that Azure/Helix infrastructure failed.
  Attribute it to the PR only when changed source/configuration causally explains
  the request or credential; call it pre-existing only when comparable evidence
  shows the same failure before the stated window. Otherwise use Inconclusive and
  name the next check (for example, verify feed availability and credential scope).
- Use External infrastructure for provider-side service outages, agent/queue
  failures, or transport incidents only when logs, service status, or comparable
  executions support that mechanism and changed configuration has been considered.
- Normalize volatile identifiers only. Preserve diagnostic numbers, meaningful
  paths/versions, endpoints, expected/actual values, and distinct stack mechanisms.
  Stable GUIDs such as COM CLSIDs and assertion timestamps are meaningful values,
  not automatically volatile identifiers.
- Cascades and generic parent jobs remain context, not extra independent causes.
- Authentication failures, truncation, incompatible schemas, and missing artifacts
  must remain explicit. Do not convert them to successful empty results.

The existing monitor also separates phase, mechanism, provenance, and causal
confidence; see its [reasoning rules](../../../workflows/ci-quality-monitor.md)
and [failure model](../../../ci-quality-monitor/DESIGN.md). Its automatic
issue-delivery and recurrence gates are not attribution rules for interactive CI.

## Examples

**PR-introduced intermittency:** a new unguarded environment mutation is reachable
by a parallel test; source and traces establish that race. Relevant controls ran
without the mutation. Verdict: PR-related, Intermittent; confidence follows the
causal evidence, not merely a retry's outcome.

**Latest batch after a pre-existing failure:** A passes the scenario, B fails
mechanism M, and C fails M after three new commits. Comparable B evidence proves
M predates the B-to-C window. Verdict: Pre-existing relative to that window;
the A-to-B introduction remains a separate question. Do not blame C's final commit.

**Scheduled unchanged source:** two runs share a tested SHA, but the failing run
uses a newly resolved resource version. Report the empty source window and the
resource difference. Without a mechanism-level explanation, attribution remains
Inconclusive rather than automatically flaky or infrastructure-owned.

**Mixed failures:** one test matches a verified existing race; a separate compile
diagnostic traces to a changed target. Report both assessments. A third result-less
timeout remains Inconclusive if its logs are unavailable. No all-unrelated verdict.
