# Static Web Assets — Agent Reasoning Evals

These are **reasoning evals**, not unit tests. Each one captures a real investigation where an
AI agent (or a person) plausibly reaches the *wrong* conclusion, and pins down what the *right*
conclusion is. They exist so that changes to agent guidance (`AGENTS.md`, `Architecture.md`,
diagnostics, code comments) can be **measured**: does the new guidance actually move an agent from
the wrong conclusion to the right one?

## Why this exists

The Static Web Assets pipeline crashes tend to surface **far from their cause**. A duplicate asset
produced by a NuGet package throws inside an SDK MSBuild task, so the obvious-but-wrong instinct is
to "fix the task that threw." Getting this right requires reasoning from *invariants* and *provenance*
to the **producer**, not patching the symptom site. That reasoning step is exactly what a doc/diagnostic
change is supposed to install — and the only way to know if it worked is to re-run a fixed scenario and
check the conclusion.

## Format

Each eval is a Markdown file, `<id>.eval.md`, with three required sections:

- **`## Prompt`** — what the agent is given. The crash, minimal repro context, and the artifacts a real
  investigator would have (e.g. a binlog excerpt). It must **not** contain the answer or leading hints.
  **Ground every datum in the real artifact.** Asset facts (target path, `AssetKind`, `AssetMode`,
  `SourceType`, provenance) must be transcribed verbatim from the actual binlog and cited (build/job).
  A single wrong `AssetKind` can change which collider looks "foreign" and quietly invert the eval — a
  fabricated value here is worse than no value.
- **`## Rubric`** — objective PASS/FAIL criteria. Written as checkable assertions about the agent's final
  conclusion, so a human or a grader model can score a transcript without judgment calls.
- **`## Baseline`** — the last recorded result (date, model, PASS/FAIL, and the evidence). This is what a
  guidance change is trying to improve.

## How to run (until a harness exists)

1. Start a fresh agent session in a clean checkout of this repo (so it sees current `AGENTS.md` etc.).
2. Paste the eval's **Prompt** verbatim. Let the agent investigate to a final recommendation.
3. Score the transcript against the **Rubric**. Update **Baseline** with date, model, result, evidence.

**Do not floor out cross-repo attribution.** If the eval's correct answer is a fix in a *different* repo
(a referenced package, the consuming app), the run setup must let the agent recommend that. An agent
confined to read/write only this repo will put the fix where it can edit it — masking the very
producer-attribution behavior under test. State explicitly in the run setup: *"you may recommend a fix in
any repo — this SDK, the consuming app, or a referenced package's source — name the specific repo and
file."* Keep other constraints (no internet, no git history) as needed for a fair reasoning test. (This is
a sound general rule; note, though, that for `54779-endpoints-manifest-collision` the correct fix turned out
to be in *this* repo — the SDK — so confinement was not the decisive flaw there. Mis-calibration was; see the
next rule.)

**Do not calibrate against an unsettled investigation.** An eval's "correct answer" must be the *final*
resolution, not an in-flight hypothesis. `54779-endpoints-manifest-collision` was first calibrated to the
producer-side `Framework`-asset fix that dotnet/aspnetcore#67375 *originally* proposed; that approach was
**rejected in review** and the real fix landed in the SDK (dotnet/sdk#54941, with the package authoring
unchanged), so the PASS criterion had to be flipped. If the source PR/issue is still open or under active
review, mark the baseline **provisional** and re-confirm the rubric once it merges — the obvious "fix the
producer" answer can be a later-rejected intermediate.

A transcript-grading harness (feed Prompt → capture conclusion → grade against Rubric with a model) can
be added later; the file format is designed to be machine-readable for that purpose. The point of
committing these now is that the **target outcome and the rubric are version-controlled and reviewable**,
independent of any harness.

## Catalog

| Eval | Scenario | Right conclusion |
|------|----------|------------------|
| [`54779-endpoints-manifest-collision`](54779-endpoints-manifest-collision.eval.md) | `GenerateStaticWebAssetEndpointsManifest` throws `Sequence contains more than one element` for a MAUI Blazor Hybrid app (builds fine; only publish fails). | SDK build→publish carry-forward: a package's deferred-group fallback `blazor.modules.json` is resolved at build but never re-applied at publish, so it survives and collides. Fix the **SDK** (persist resolved groups; re-apply the unscoped group filter when the manifest is reloaded at publish); leave the package unchanged. NOT an SDK-task disambiguation, and NOT a package remodel to a `Framework` asset. |
