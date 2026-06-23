# Eval: `54779-endpoints-manifest-collision`

- **Source:** dotnet/sdk#54779 (regression). **Fix that LANDED:** dotnet/aspnetcore#67375 (MERGED) —
  the `Microsoft.AspNetCore.Components.WebView` package stops shipping `blazor.modules.json` as an
  unconditional static-web-asset and instead **materializes the empty (`[]`) fallback as the consumer's own
  asset only when the app contributes no JS modules of its own**, so exactly one asset ever lands on
  `_framework/blazor.modules.json` and the collision cannot form. Backport: dotnet/aspnetcore#67401
  (`release/11.0-preview6`). **Superseded:** dotnet/sdk#54941 (apply deferred asset-group resolution at
  publish) was a real, working, *empirically verified* SDK fix but was **not merged** — the owner chose the
  simpler package fix that removes the duplicate at its source rather than de-duplicating it downstream.
- **Fixture provenance:** the asset table in the Prompt is transcribed from the real failing binlog
  (AzDO dnceng-public build `1463118`, job *Build Windows (Release)*). Asset kinds (`All` / `Build` /
  `Publish`) are reproduced verbatim — do **not** paraphrase or collapse `AssetMode` into `AssetKind`; an
  inaccurate kind changes which collider looks "foreign" and invalidates the eval.
- **Tests guidance:** `AGENTS.md` "Triage Heuristic", `Architecture.md` §"Deferred Groups and `SkipDeferred`
  Filtering". ⚠️ **Guidance recalibration pending:** as of this v3 rewrite those companion docs and the two
  code comments still teach the **superseded** SDK-carry-forward thesis (they predate the merge of
  dotnet/aspnetcore#67375). They must be reworked to match this eval — until then the eval is the
  authoritative target, and a v3 treatment A/B cannot be run.
- **Failure mode under test:** mis-localizing the fix anywhere along a deceptively reasonable gradient.
  Five reasoning destinations exist for this one crash, only one of which is the durable fix:
  1. **Symptom-site anchoring** — "fix the SDK task that threw" (soften `SingleOrDefault`). *Wrong.*
  2. **Wrong package fix** — retype the fallback as a `Framework` asset. *Still leaves two assets on the
     route; rejected in review.*
  3. **Consumer-scoped SDK patch** — a `Remove`/`SourceId != $(PackageId)` guard in the publish targets.
     *Structurally incomplete.*
  4. **SDK build→publish carry-forward** (dotnet/sdk#54941) — de-duplicate the two assets at publish.
     *Real, verified, defensible — but a downstream compensation, and superseded.*
  5. **Package conditional fallback** (dotnet/aspnetcore#67375) — never put the second asset on the route
     unless it's actually needed. *The landed fix.*
  The discriminator that separates #5 from all others: **does exactly one asset land on the route (cause
  removed), or do two land and get de-duplicated (symptom compensated)?**

## Prompt

> You are investigating a build failure in the .NET SDK. A .NET MAUI Blazor Hybrid app (a project using
> `Microsoft.NET.Sdk.Razor` that references the `Microsoft.AspNetCore.Components.WebView` package **and** a
> Razor Class Library that contributes JS module assets) fails to **publish** on SDK
> `11.0.100-preview.6` with the error below. The same app published cleanly on `preview.5`. **The project
> `build`s without error on `preview.6`; the failure occurs only during `publish`.**
>
> ```
> error : InvalidOperationException: Sequence contains more than one element
>    at System.Linq.Enumerable.SingleOrDefault[TSource](IEnumerable`1 source)
>    at Microsoft.AspNetCore.StaticWebAssets.Tasks.GenerateStaticWebAssetEndpointsManifest
>        .ComputeManifestAssets(IEnumerable`1 assets, String kind)+MoveNext()
>    at Microsoft.AspNetCore.StaticWebAssets.Tasks.GenerateStaticWebAssetEndpointsManifest.Execute()
> ```
>
> From the failing build's binlog, three `StaticWebAsset`s share the same target path
> `_framework/blazor.modules.json` going into the publish endpoints manifest:
>
> | SourceId | SourceType | AssetKind | AssetMode | Identity (abbreviated) |
> |----------|-----------|-----------|-----------|------------------------|
> | `Microsoft.AspNetCore.Components.WebView` | `Package` | `All` | `All` | `…/microsoft.aspnetcore.components.webview/…/staticwebassets/blazor.modules.json` |
> | `MyApp` | `Computed` | `Build` | `All` | `…/obj/.../jsmodules/jsmodules.build.manifest.json` (promoted to `blazor.modules.json`) |
> | `MyApp` | `Computed` | `Publish` | `All` | `…/obj/.../jsmodules/jsmodules.publish.manifest.json` |
>
> The `Publish`-kind publish manifest (row 3) is **new in `preview.6`**; `preview.5` produced only the
> `Build`-kind build manifest. At publish, the `Build`-kind asset is filtered out, so the endpoints manifest
> group for that target path contains the package's `All` asset and the app's `Publish` asset.
>
> Determine the **root cause** and recommend **where the fix belongs**. State the specific code or
> project/package you would change, and why.

## Rubric

Score the agent's **final conclusion/recommendation** on a three-tier scale. The crux of this eval is
distinguishing *removing the cause* (one asset ever lands on the route) from *compensating for the symptom*
(two assets land and something de-duplicates them).

### The root cause (what the agent must ultimately localize)

The `Microsoft.AspNetCore.Components.WebView` package **unconditionally** contributes a fallback
`blazor.modules.json` onto the shared target path `_framework/blazor.modules.json` (historically via a
deferred asset group plus an `AssetKind=All` promotion of the app's own manifest). That guarantees **two
primary assets on one route** whenever the app *also* has its own modules manifest — a collision that
*something* downstream must then de-duplicate. Pre-`preview.6` the de-duplication happened to survive into
publish; `preview.6` added a third collider (a real `Publish`-kind app manifest) and the de-duplication was
never re-applied at publish, so the fallback survived and `ChooseNearestAssetKind(...).SingleOrDefault()`
threw. **The durable fix is to stop creating the second asset unless it is actually needed** — i.e. make the
package's fallback **conditional** on the consumer having no JS modules of its own. Then exactly one asset is
ever on the route, in every phase, and no group / carry-forward / de-dup machinery is required anywhere.

### PASS (full credit) — removes the cause

Requires ALL of:

1. **Correct cause.** Identifies that the **package** is the source of the second asset on the route: it
   ships/materializes a fallback `blazor.modules.json` *unconditionally*, so two primary assets occupy one
   target path whenever the app supplies its own — and that surplus asset is the thing that has to be
   de-duplicated (and isn't, at publish).
2. **Correct remedy — eliminate, don't de-duplicate.** Recommends making the package's fallback
   **conditional**: materialize the empty (`[]`) fallback as the consumer's own static web asset **only when
   the app contributes no JS modules of its own** (decided during input resolution, before the manifest
   conflict check). Net effect named or clearly implied: **exactly one asset ever lands on the route**, so
   there is nothing to disambiguate and **no SDK change is needed**. (This is dotnet/aspnetcore#67375.)
3. **Uses the build-vs-publish asymmetry correctly** — recognizes the project builds but only publish throws,
   and explains it as the surplus fallback surviving into publish, *without* concluding that the remedy is to
   teach publish to drop it. (Bonus for noting that any downstream de-dup — at the throw site, via a
   consumer-scoped `Remove`, or via build→publish carry-forward — is compensating for a duplicate the package
   should never have emitted.)
4. **Rejects all four non-durable destinations** (see PARTIAL/FAIL): does not soften the throwing task, does
   not retype the fallback as a `Framework` asset (that still leaves two assets + a redundant endpoint), does
   not add a consumer-scoped publish-targets guard, and does not stop at the SDK build→publish carry-forward
   as *the* fix.

### PARTIAL (acceptable but superseded) — fixes a real defect, but compensates downstream

Award PARTIAL when the agent lands on the **SDK build→publish carry-forward** fix (dotnet/sdk#54941): persist
the resolved (no-longer-`Deferred`) groups into the build manifest and re-apply the **unscoped** group filter
when the manifest is reloaded at publish, dropping the excluded variant before the endpoints manifest is
computed, preserving the target-path uniqueness invariant.

This is **not a failure**: it correctly localizes a genuine SDK resolution gap, is at a defensible layer, was
**empirically verified to fix the bug** (see Baseline), and explicitly rejects the symptom-site, package-retype,
and consumer-scoped traps. It falls short of full credit only because it **de-duplicates the two assets at
publish instead of preventing the second asset** — a downstream compensation the owner ultimately superseded
with the simpler package fix. An agent that proposes #54941 *and* notes that the cleaner fix is to make the
package fallback conditional should be scored **PASS**.

### FAIL — wrong layer or wrong mechanism

Any of:

- **Symptom-site softening.** Recommends changing `GenerateStaticWebAssetEndpointsManifest` /
  `ChooseNearestAssetKind` / `GenerateStaticWebAssetsDevelopmentManifest` to disambiguate, pick one, de-dup,
  or swap `SingleOrDefault` → `First()` **as the fix**. (A clearer *diagnostic* at the throw site is fine only
  if explicitly framed as not being the fix.)
- **Wrong package fix (unconditional retype).** Recommends remodeling the package's `blazor.modules.json` as a
  **`Framework`** asset (or any retyping that keeps the fallback present unconditionally). This still leaves
  **two assets on the route** and a redundant endpoint — it relocates the disambiguation rather than removing
  the duplicate — and it is the approach that was **tried and rejected in review**. *Note the sharp line: a
  package fix is correct only if it makes the fallback **conditional** (one asset). An **unconditional**
  package change is FAIL.*
- **Consumer-scoped SDK patch.** A `Remove` / `SourceId != $(PackageId)` guard in the publish targets —
  structurally incomplete, because the publish group filter cannot scope to a referenced project's or
  package's group.
- **No localization.** Concludes the regression is "just" some other SDK PR/commit to revert, or proposes only
  a consumer-project workaround (remove the ProjectReference, disable the feature), without identifying the
  package's unconditional-fallback as the source of the surplus asset.

## Baseline

> **v3 recalibration (2026-06-24) — the correct answer moved a THIRD time, and it landed in the package.**
> Ground truth for this bug has now shifted three times: **(v1)** model `blazor.modules.json` as a `Framework`
> asset (package) — *rejected in review*; **(v2)** carry deferred-group resolution into publish
> (dotnet/sdk#54941, SDK) — *built and empirically verified, but not merged → superseded*; **(v3)** make the
> package fallback **conditional** so exactly one asset ever lands on the route (dotnet/aspnetcore#67375,
> package) — **MERGED**. The Rubric above is **v3**, calibrated to that landed fix.
>
> **Why this is not a contradiction of v2's "don't fix the package" rule.** v1 and v3 are *both* package
> changes but are opposites in mechanism: v1 keeps the fallback **unconditional** (just retyped) so two assets
> still collide and must be disambiguated — which is exactly why it was rejected; v3 makes the fallback
> **conditional** so the second asset never exists. The v2 rubric's hard-FAIL clause ("any package change =
> FAIL") was over-broad: it correctly caught the v1 *unconditional* retype but would have wrongly failed the
> v3 *conditional* fix that actually shipped. v3 narrows the FAIL clause to *unconditional* package changes
> only.
>
> **Methodology lesson (now fully realized).** Never freeze an eval's "correct answer" from an unsettled
> investigation. The obvious producer fix (v1) was a later-rejected intermediate; the elegant SDK fix (v2) was
> a verified-but-superseded intermediate; only the merged fix (v3) is authoritative. We deliberately never
> pushed the guidance branch, never opened an SDK PR, and never recalibrated to v2 as final — which is the
> only reason this rewrite is a clean re-aim rather than a shipped mistake.

### Empirical verification of the landed fix (2026-06-24)

A reproduction harness (Razor-SDK app → real public WebView `preview.6` package + an RCL with a JS module) was
run on a genuinely pre-fix SDK, swapping **only the package** between runs:

| Scenario | Public package (pre-fix) | Patched package (dotnet/aspnetcore#67375) |
|----------|--------------------------|-------------------------------------------|
| App + RCL-with-JS-module, `publish` | **crash** (`Sequence contains more than one element`, `GenerateStaticWebAssetEndpointsManifest.cs:229`) | **PASS** — 1 route, content = app's `_content/rcl/…lib.module.js` |
| App with no JS modules, `build`+`publish` | n/a | **PASS** — 1 route, content `[]` |
| Incremental no-module → add-module (no clean) | n/a | **PASS** — stale `[]` Discovered fallback fully evicted; no double-add |
| App **and** RCL both *directly* reference the package, neither has a JS module | n/a | **FAIL (genuine uncovered edge)** — two `[]` fallbacks collide → `Conflicting assets…` *build* error (loud/early, not the silent publish crash); the new target has no class-library guard. Not covered by the merged tests. |

**Conclusion:** the package-only conditional fallback resolves the targeted regression with **no SDK change**,
confirming v3. One non-blocking edge (dual *direct* package reference with no modules) was reported upstream as
a follow-up. dotnet/sdk#54941 remains a valid *alternative* fix (PARTIAL) but was superseded.

#### Real-MAUI confirmation (2026-06-24) — `dotnet new maui-blazor` on the genuine preview.6 toolchain

The synthetic harness above was independently confirmed on a **real MAUI Blazor Hybrid app** built and
published against the **official `11.0.100-preview.6.26323.106` SDK** (genuinely pre-fix, no dotnet/sdk#54941),
with the base `Microsoft.AspNetCore.Components.WebView` pinned to the matching `preview.6` value and referenced
exactly as `…WebView.Maui` references it (`ExcludeAssets="Build;Analyzers"`), plus an RCL contributing
`rcl.lib.module.js`:

- **Baseline (real MAUI, pre-fix package):** same crash —
  `InvalidOperationException: Sequence contains more than one element` at
  `GenerateStaticWebAssetEndpointsManifest.ComputeManifestAssets` (`Publish.targets(55,5)`); the binlog shows
  **two** assets on `_framework/blazor.modules.json` (the package's `[]` fallback + the SDK-generated RCL
  manifest). **MAUI preview.6 is genuinely affected — not immune.**
- **Fix (swap *only* the base package to the #67375-shaped build, same SDK, same app):**
  `GenerateStaticWebAssetEndpointsManifest` **succeeds**; exactly **one** endpoint on the route, content =
  the real `_content/rcl/…lib.module.js` module list (not `[]`). Publish then proceeds past SWA (later hitting
  an unrelated AOT/IL1032 native-build issue, orthogonal to this pipeline).
- **Mechanism correction worth keeping:** `…WebView.Maui`'s `ExcludeAssets="Build"` does **not** prevent the
  conditional-fallback target from running. The package's targets reach the consumer via
  `buildTransitive → buildMultiTargeting → build/…targets` (an explicit path-based `<Import>`); `Exclude="Build"`
  only suppresses the *automatic* `build/` import, not the path-based one. So `_AddBlazorWebViewModulesFallback`
  **does** run inside real MAUI apps — skipped when the RCL contributes a module (RCL wins), materializing the
  `[]` fallback when none exists. This refutes the pre-merge worry that the package fix might never fire under
  MAUI's `Exclude="Build"`, and the symmetric worry that a no-modules app would lose its manifest: it gets a
  valid `[]` manifest either way. **The package's targets see the local signal (`_ExistingBuildJSModules`)
  they need, inside the consumer's build — which is exactly why a *conditional* package fix is possible.**
- **Flow note:** as of WebView `…26324.102` the fix had not yet shipped in any preview.6 package; MAUI
  preview.6 needs the backport (dotnet/aspnetcore#67401) to ship to pick it up. Scenario D (dual direct
  reference, neither owner owns a module) was not re-run in MAUI shape and remains the one open edge.

### v1 runs (superseded rubric — retained for the methodology trail)

> Scored against the original v1 rubric, which scored the `Framework`-asset package remodel as PASS. Under v3
> every row below is **FAIL** (the v1 treatment landed on the *unconditional* package retype that was rejected
> in review).

| Date | Model / setup | v1 score | v3 re-score | Evidence |
|------|---------------|----------|-------------|----------|
| 2026-06-22 | Copilot agent, preview.6 era, **before** any guidance | FAIL | **FAIL** | Drafted an SDK issue whose "Expected behavior" was that the publish manifest task should *disambiguate* Build/Publish/All assets "not throwing", and opened two SDK-task PRs (softening `SingleOrDefault`; adding a diagnostic). Symptom-site anchoring. |
| 2026-06-23 | Sonnet-4.6 · **control** (clean `main`) · repo-confined · no GitHub/web/git-history | FAIL | **FAIL** | Found the publish-side `_ExistingPublishJSModules` filter lacks the `SourceId != $(PackageId)` guard its build-side sibling has, and proposed adding it. Consumer-scoped SDK patch; no producer/deferred-group framing. |
| 2026-06-23 | Sonnet-4.6 · **treatment** (guidance, spoilers redacted) · repo-confined · no GitHub/web/git-history | FAIL (near-miss) | **FAIL** | Reconstructed the deferred-group mechanism and named the `Framework`-asset remedy, but chose an SDK-side compensating `Remove` in `JSModules.targets` as the concrete change. |
| 2026-06-23 | Sonnet-4.6 · **control**, un-confined · no GitHub/web/git-history | FAIL | **FAIL** | Located the fix in SDK `StaticWebAsset.ChooseNearestAssetKind` (suppress the `All` candidate once a specific kind is seen). Symptom-site softening. |
| 2026-06-23 | Sonnet-4.6 · **treatment** (guidance, spoilers redacted), un-confined · no GitHub/web/git-history | **PASS (v1)** | **FAIL** | Concluded the fix belongs in the WebView package by changing `blazor.modules.json` from an `All`-kind deferred-group asset to a `SourceType="Framework"` asset. Right *layer*, but the **unconditional** retype that was rejected in review — it leaves a redundant endpoint and still puts two assets on the route. This is precisely the v3 "wrong package fix" FAIL clause. |

### v2 runs (superseded rubric — SDK carry-forward scored as PASS)

> The v2 rubric scored the SDK build→publish carry-forward as PASS and **any** package change as FAIL. Under
> v3 the SDK carry-forward is **PARTIAL** (acceptable but superseded), and the FAIL-on-package clause is
> narrowed to *unconditional* package changes only.

| Date | Model / setup | v2 score | v3 re-score | Evidence |
|------|---------------|----------|-------------|----------|
| 2026-06-23 | Sonnet-4.6 · **control** (clean `main`, no guidance), un-confined · no GitHub/web/git-history | FAIL | **FAIL** | Rejected both the task-softening and the package remodel, but landed on a **consumer-scoped `Remove`** in `JSModules.targets` `ResolveJSModuleManifestPublishStaticWebAssets`. Structurally incomplete (consumer-scoped). |
| 2026-06-23 | Sonnet-4.6 · **treatment** (recalibrated v2 guidance, spoilers redacted), un-confined · no GitHub/web/git-history | PASS | **PARTIAL** | Concluded the SDK resolves deferred groups at build but never carries that into publish, and the publish filter is consumer-scoped so it structurally can't filter a package-owned group; fix in SDK = persist resolved groups + re-apply the unscoped filter at publish. Independently matches dotnet/sdk#54941 — a real, verified fix at a defensible layer, **but a downstream de-dup that was superseded** by the package conditional fallback. Did not reach "stop creating the second asset." |

### v3 runs (current rubric — authoritative)

| Date | Model / setup | Result | Evidence |
|------|---------------|--------|----------|
| _pending_ | — | — | No v3 treatment run yet: the companion guidance (AGENTS.md / Architecture.md / code comments) still teaches the v2 SDK-carry-forward thesis and must be recalibrated to the landed package conditional-fallback before a fair treatment A/B can be run. A v3 **control** (no guidance) is expected to FAIL on the symptom-site or consumer-scoped traps as before; the open question v3 measures is whether recalibrated guidance moves a fresh agent past the *defensible-but-superseded* SDK carry-forward (PARTIAL) to "make the package fallback conditional" (PASS). |

> Update this table when re-running. A guidance change "works" when a fresh agent session, given only the
> **Prompt** above, produces a conclusion that scores **PASS** against the v3 **Rubric** (full credit =
> conditional package fallback; the SDK carry-forward is honest PARTIAL credit, not a failure).
