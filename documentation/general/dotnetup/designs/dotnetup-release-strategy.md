# dotnetup `Preview` Release Strategy

`dotnetup` ships today as a small internal preview built daily from the tip of the internal CI pipeline. SDK `main` consumes that daily build.

The problem: every daily change reaches every CI consumer, so we cannot let an SDK servicing/release branch depend on a *stable* dotnetup without it being broken by an unrelated daily change once we remove the `.NET Install Script` fallback.

---

## 1. Requirements

### Decisions: preview / phase 1

**Must have**
- Allow others to download prior releases, even when unsupported (the intent is to let customers roll back without our intervention). Prior versions are downloadable today as immutable per-version `ci.dot.net` URLs; the underlying `dotnetbuilds` blob retention window has no formal retention policy for these artifacts, however.
- Fast (within 15 minutes of detection) rollback or re-release when something goes wrong.
- Develop features without risk of them reaching a stable release.
- Preview consumption / telemetry on a dev branch to confirm stability before promoting to stable.
- Only support 'latest' preview to reduce maintenance burden and move fast.
- Compliance with MSFT policy.

**Nice to have**
- No PRs required to bump minor versions.
- Automated changelog notes.
- Only maintain one branch at a time.
- Minimal manual maintenance effort burdens to release.
- Clear UX with `dotnetup --version` (existing today) that helps report and track issues tied to a specific version

**Not necessary**
- Truly supporting multiple dotnetup versions simultaneously.
- Backporting.

### Decisions: public / stable preview

**Additional must have**
- CDN routing for high-scale / geolocation downloads (`builds.dotnet.microsoft.com`).
  (`ci.dot.net` likely cannot serve a large public download swath.)
- Vendor testing plan to release a stable release.

**Additional nice to have**
- `dotnetup` inclusion in a `json` manifest (likely required for automatic updates).

---

## 2. Assumptions

- `ci.dot.net` could not scale to the full potential dotnetup customer base, even though it is fronted by Azure Front Door over blob storage.
- `stable` needs to scale beyond `ci.dot.net`; `preview` may not (external customers should not build production CI on top of a preview product).
- GitHub Releases may provide scalable / CDN-like downloads (works for dotnet diagnostics).
- `aka.ms` has no Azure Front Door / `x-cache` / Akamai layer of its own. It is a Kestrel app serving a `301` redirect, which is sufficient for `preview` and `daily`.
- `ci.dot.net` per-version build URLs persist long enough for `dotnetup` rollback needs. See [Retention investigation](#appendix-a-retention-investigation-dotnetbuildscidotnet) below.

### Verified infrastructure facts

- The aka.ms daily links are `301` redirects served by **Kestrel** (no CDN of their own):

 `https://aka.ms/dotnet/dotnetup/daily/dotnetup-win-x64.exe` →

  ```
    https://ci.dot.net/public/dotnetup/0.1.4-preview.4.26303.1/dotnetup-win-x64.exe
  ```

 `https://aka.ms/dotnet/dotnetup/daily/dotnetup-win-x64.exe.sha512` →


  ```
  https://ci.dot.net/public-checksums/dotnetup/0.1.4-preview.4.26303.1/dotnetup-win-x64.exe.sha512
  ```

- Both resolved explicit-version URLs return `200` (binary = 16,143,696 bytes; checksum = 128 bytes).
- `ci.dot.net` fronts `dotnetbuilds.blob.core.windows.net` via **Azure Front Door**
  (`x-azure-ref` present, `X-Cache: TCP_MISS`, `Cache-Control: public, max-age=604800` = 7 days,
  `x-ms-blob-type: BlockBlob`). dotnetup daily already lands here today.
- `builds.dotnet.microsoft.com` fronts `dotnetcli.blob.core.windows.net` via **Akamai** (`Akamai-GRN` present, no `x-azure-ref`). Reaching it requires a promotion (copy) from `dotnetbuilds` → `dotnetcli`, gated by the release team's staging process. The account→CDN mapping is encoded in Arcade's `PublishingConstants.cs`.

- dotnetup's Arcade channel, with `targetFeeds: DotNetToolsFeeds` publishes installers/checksums to `dotnetbuilds/public` and
  `dotnetbuilds/public-checksums`. i.e. we are **already in staging**.

- The versioned blob layout `dotnetup/$(DotnetupVersion)/dotnetup-$(TargetRid)$(_DotnetupNativeExt)`
  is set in [eng/Publishing.props](../../../../eng/Publishing.props) (`SetDotnetupBlobPaths`).

- The install scripts default to `Quality=daily` and resolve `https://aka.ms/dotnet/dotnetup/<quality>`
  ([scripts/get-dotnetup.sh](../../../../scripts/get-dotnetup.sh),
  [scripts/get-dotnetup.ps1](../../../../scripts/get-dotnetup.ps1)).

---

## 3. Comparisons

- **aspire** — owns its own download site (`aspire.dev`) plus npm, WinGet, other packages, and GitHub Releases. Includes rich channel identity (stable / staging / daily / local / pr), release branches, and SHA-derived staging feeds. Promotion is *manual*: the release pipeline is queued against a selected build, extracts its BAR ID, and runs `darc add-build-to-channel`. Product SemVer is hand-authored in `eng/Versions.props`, not auto-bumped.
  ([release-process.md](https://github.com/microsoft/aspire/blob/main/docs/release-process.md))
- **az cli** — `dev` branch is an `edge` build via `aka.ms`. Official uses WinGet, MSI, and ZIP (served from Microsoft-hosted blob storage behind `aka.ms` links) plus Microsoft package feeds.
  ([docs](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?pivots=zip))
- **gh cli** — WinGet, Homebrew, custom Linux package feed. **Stable releases only**; no daily/edge channel. "Build from source" is for contributors, not a distribution channel.

Takeaway: dotnetup's ideal is the *lighter half* of Aspire's model. i.e. Separate daily from a blessed selector with immutable versioned artifacts.

---

## 4. Versions Policy

| Channel  | Version shape | Meaning |
|----------|---------------|---------|
| daily    | `0.x.y`       | Moving, as-is, built from active development tip. No stability promise. |
| preview  | `0.x.y`       | Blessed internal preview. The latest supported version. |
| stable   | `1.0.0`       | Public, CDN-scaled release (future). |

Explicit immutable version selectors remain downloadable for historical acquisition and rollback.
These are real, working URLs today — verified `200` on 2026-06-11, e.g.:

- `https://ci.dot.net/public/dotnetup/0.1.4-preview.4.26303.1/dotnetup-win-x64.exe`
- `https://ci.dot.net/public-checksums/dotnetup/0.1.4-preview.4.26303.1/dotnetup-win-x64.exe.sha512`


---

## 5. Plan (phased)

### Phase 0 — Current

Daily channel available. `aka.ms/dotnet/dotnetup/daily/...` points at the latest published daily
build on `ci.dot.net`. SDK `main` consumes daily. No tags, no GitHub Release, no blessed selector.
The dotnetup CI pipeline ([.vsts-dnup-ci.yml](../../../../.vsts-dnup-ci.yml)) triggers on the
`dnup`, `release/dnup`, and `release/dotnetup` branches today.

### Phase 1 — Blessed `preview` selector

1. **Maintain one branch: `release/dnup`.**
2. **Expose both `/daily/` and `/preview/` URLs.** Daily keeps tracking the moving tip; preview resolves to a 'blessed' build. (But still not 'blessed' to actually tell external customers to use in production.)
3. **Change which `/daily/` build `/preview/` points at via an explicit process**
   Implement a `release` pipeline that lets an operator select a set of dotnetup release artifacts from a prior daily pipeline run.
4. **The release pipeline:**
  - Takes a previously produced daily build/artifact set and promotes it as-is. **No rebuild.**
  - Does **not** inject version properties (`PreReleaseVersionLabel`, patch bump, etc.) at release time.
  - Validates release preconditions before promotion:
    - Selected artifact version is different from the version in the latest preview tag.
    - Selected artifacts include matching checksum sidecars for all published installers.
  - Executes using the trusted pipeline/workflow and script content from `release/dnup` only (no PR ref checkout for executable content).
  - Creates/updates the preview tag on `release/dnup` that corresponds to the already-built artifact version.
  - Repoints `/preview/` `aka.ms` links to that immutable version.
  - Creates the GitHub Release notes for that version.

5. **Version bumping is decoupled from release promotion.**
  - Version increments are prepared ahead of time via bot-created PRs on `release/dnup` which must be merged manually.
  - Promotion only picks and blesses a prebuilt version; it does not author a new version.

**Recovery — if only a simple revert is needed:**
1. Re-run the `release` pipeline against an existing tag and repoint the `/preview/` URL.
   Archival CI URLs exist per version and are confirmed live today, e.g.
   `https://ci.dot.net/public/dotnetup/0.1.4-preview.4.26303.1/dotnetup-win-x64.exe` and its
   `.../public-checksums/dotnetup/0.1.4-preview.4.26303.1/dotnetup-win-x64.exe.sha512` sidecar.
   **Open (low risk):** No formal retention/TTL policy is documented for `dotnetbuilds` blobs — see
   [Retention investigation](#appendix-a-retention-investigation-dotnetbuildscidotnet). Empirical evidence shows
   non-promoted preview builds from 4+ years ago remain accessible, and dotnetup builds are never
   subject to promotion-cleanup. We consider the rollback-via-repoint strategy safe for practical
   timescales, though confirming definitively requires Azure portal access from dnceng.

**Recovery — fix needed (e.g. security patch on an old branch):**
1. Check out the tagged commit.
2. Create `release/dnup/<tag-version>/hotfix-x.y.z` off the tag.
3. Open an internal PR with the fix into that branch and get approval.
4. Validate the fix through normal branch CI/PR checks, then run the release pipeline to repoint the selector.

This phase allows a public preview. I view arcade rollout as valid around this time once we see stabilization of the public preview, but also potential arcade rollout with a fallback during `preview`.

In this phase, before public preview we'd remove the fallback to the .NET Install Script, but keep the fallback on the `daily` dotnetup builds, and keep `release/dnup` using the `daily` dotnetup builds to build. (This prevents a broken `dotnetup` from preventing us from shipping a new `dotnetup`.)

### Phase 1.1 — Concrete version-bump bot strategy (no new bot)

Goal: always have a bot-created "next version bump" PR ready on `release/dnup`, and automatically create the next one when the previous bump PR merges.

#### GitHub Actions PR loop (recommended)

Use a repo workflow with `github-actions[bot]` as the actor (no new custom bot service):
1. Trigger on merge of a PR labeled `dotnetup-version-bump` (or matching branch prefix).
2. Run a small script that computes next preview version from the canonical version file and updates only those version fields.
3. Open/update PR via standard PR action (single open PR invariant).
4. Reapply label and assign owners.

Guardrails:
- If an open bump PR already exists, update it instead of creating a second one.
- Require branch protection + required checks before merge.
- Include idempotency checks so reruns do not create duplicate bumps.
- Workflow execution must use the trusted workflow definition and scripts from `release/dnup` only. Do not execute workflow YAML, scripts, or other content from the PR branch/ref when performing bump automation, since PR-authored content could run malicious code.

Why this is the best fit:
- Directly models the required "always create next PR after merge" behavior.
- No extra source channel, no synthetic dependency graph, no new bot host.
- Uses existing GitHub native automation and can still coexist with Maestro for other flows.

#### Proposed decision

- Use **GitHub Actions** for the recurring version-bump PR loop.
- Keep **Maestro/Darc** for build/channel/subscription flow where it already excels.

### Phase 2 — Stable on `builds.dotnet.microsoft.com` (tentative)

Migrate to `stable` versioning as well. Confirm whether it is actually feasible and correct to only support a latest version if it becomes a 'released' product.

Host a `stable` URL on `builds.dotnet.microsoft.com` by coordinating with the release team to use their promotion pipeline — we already push to `dotnetbuilds`, so this is a copy/promote from
`dotnetbuilds` → `dotnetcli` plus storage write permissions granted by the release/dnceng team.

Similar release process, through coordination. Possibly automate `preview` releases at this point. Required to rollout to any azdo task.

### Phase 3 — Package-manager acquisition (tentative)

Linux Package Manager Feed / WinGet / Homebrew / etc. Needs discussion with partners.

---

## Appendix A: Retention Investigation (`dotnetbuilds`/`ci.dot.net`)

*Investigated 2026-06-12.*

### Summary

There is **no documented retention policy** for `dotnetbuilds.blob.core.windows.net` / `ci.dot.net`.
Arcade's `PublishingConstants.cs` `TargetChannelConfig` has no `retentionDays`, `expirationDays`, or
`lifecyclePolicy` field. No retention documentation exists in `dotnet/arcade` or `dotnet/dnceng`.
Any Azure Blob lifecycle policy would be configured in the Azure portal (not in public repos).

### Empirical HTTP probe results (2026-06-12)

| Build | Age | `ci.dot.net` | `builds.dotnet.microsoft.com` | Explanation |
|-------|-----|:---:|:---:|---|
| .NET 6.0 preview 1 (Mar 2021) | ~5 yr | ❌ 404 | ✅ 200 | **Promoted** to dotnetcli, cleaned from dotnetbuilds |
| .NET 6.0 preview 7 (Aug 2021) | ~5 yr | ❌ 404 | ✅ 200 | Promoted |
| .NET 7.0 preview 1 (Feb 2022) | ~4.3 yr | ✅ 200 | — | Never promoted; still on staging |
| .NET 7.0 preview 3–7 (Mar–Jul 2022) | ~4 yr | ✅ 200 | — | Never promoted |
| .NET 7.0 RC1/RC2 (Sep–Oct 2022) | ~4 yr | ❌ 404 | ✅ 200 | Promoted |
| .NET 7.0 GA (Nov 2022) | ~4 yr | ❌ 404 | ✅ 200 | Promoted |
| .NET 8.0 preview 1 (Feb 2023) | ~3.3 yr | ✅ 200 | — | Never promoted |
| .NET 9.0 preview 1 (Feb 2024) | ~2.3 yr | ✅ 200 | — | Never promoted |
| dotnetup 0.1.4-preview (Jun 2026) | current | ✅ 200 | — | DotNetToolsFeeds; never promoted |

### Interpretation

The 404s on `ci.dot.net` are **not** from age-based TTL deletion. They are the result of a
deliberate **promotion workflow**: once a build graduates from staging (`dotnetbuilds`) to production
(`dotnetcli` / `builds.dotnet.microsoft.com`), the staging copy is cleaned up.

Since dotnetup uses `DotNetToolsFeeds` (which only targets `dotnetbuilds/public`), its builds are
**never subject to promotion-cleanup**. Non-promoted preview/daily builds from as far back as
Feb 2022 (~4.3 years) remain accessible at this time.

### Conclusion for dotnetup rollback

We consider the rollback-via-repoint strategy (pointing `/preview/` at an older versioned URL) safe
for practical timescales. The risk of silent blob deletion is low but cannot be formally ruled out
without Azure portal access from the dnceng team.

---

## Appendix B: Automated GitHub Releases

Automated GitHub Release creation from CI is compliant with Microsoft policy and is established
practice across the dotnet org. Arcade does not provide a shared release stage; each repo
implements its own using one of the patterns below.

### Precedent in the dotnet org

| Repo | Pattern | Auth | Human gate |
|---|---|---|---|
| dotnet/aspire | AzDO dispatches GitHub Actions via `aspire-repo-bot` App | GitHub App token | AzDO approval stage |
| dotnet/dotnet-monitor | AzDO pipeline runs `gh release create` | KeyVault-backed `dotnet-bot` PAT | `ManualValidation@1` |
| dotnet/android-native-tools | AzDO `GitHubRelease@1` task | AzDO service connection | `ManualValidation@0` |
| cli/cli | GitHub Actions `workflow_dispatch` | `GITHUB_TOKEN` | `production` environment approval |
| microsoft/mcp | AzDO job runs `gh release create` + upload | GitHub App token | Pipeline stage gate |

### dotnetup approach

dotnetup uses the `dotnet-monitor` pattern: an AzDO pipeline stage runs `gh release create --draft`
with a KeyVault-backed PAT, gated by `ManualValidation@1`.

- No GitHub App or cross-system dispatch to set up — a single pipeline stage on existing 1ES/dnceng
  infrastructure.
- `--draft` ensures a release is never published without a manual confirmation on GitHub.
- `ManualValidation@1` runs on a `pool: server` job with no agent cost.
- For `preview`, the manual gate may be dropped since the channel is internal, reducing release to a
  single pipeline run.
