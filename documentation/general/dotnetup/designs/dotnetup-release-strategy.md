# dotnetup `Preview` Release Strategy

`dotnetup` ships today as a small internal preview built daily from the tip of the  internal CI pipeline. SDK `main` consumes that daily build.

The problem: every daily change reaches every CI consumer, so we cannot let an SDK servicing/release branch depend on a *stable* dotnetup without it being broken by an unrelated daily change once we remove the `.NET Install Script` fallback.

---

## 1. Requirements

### Decisions: preview / phase 1

**Must have**
- Allow others to download prior releases, even when unsupported (the intent is to let customers roll back without our intervention). Prior versions are downloadable today as immutable per-version `ci.dot.net` URLs; the underlying `dotnetbuilds` blob retention window has no formal retention policy for these artifacts, however.
- Easy rollback or re-release when something goes wrong.
- Develop features without risk of them reaching a stable release.
- Preview consumption / telemetry on a dev branch to confirm stability before promoting to stable.
- Only support 'latest' preview to reduce maintenance burden and move fast.

**Nice to have**
- No PRs required to bump minor versions.
- Automated changelog notes.
- Only maintain one branch at a time.

**Not necessary**
- Truly supporting multiple dotnetup versions simultaneously.
- Backporting.

### Decisions: public / stable preview

**Additional must have**
- CDN routing for high-scale / geolocation downloads (`builds.dotnet.microsoft.com`).
  (`ci.dot.net` likely cannot serve a large public download swath.)

**Additional nice to have**
- `dotnetup` inclusion in a `json` manifest (likely required for automatic updates).

---

## 2. Assumptions

- `ci.dot.net` could not scale to the full potential dotnetup customer base, even though it is fronted by Azure Front Door over blob storage.
- `stable` needs to scale beyond `ci.dot.net`; `preview` may not (external customers should not build production CI on top of a preview product).
- GitHub Releases may provide scalable / CDN-like downloads (works for dotnet diagnostics).
- `aka.ms` has no Azure Front Door / `x-cache` / Akamai layer of its own. It is a Kestrel app serving a `301` redirect, which is sufficient for `preview` and `daily`.
- `ci.dot.net` per-version build URLs persist long enough for `dotnetup` rollback needs. This is **assumed, not verified.**

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

- dotnetup's Arcade channel, with `targetFeeds: DotNetToolsFeeds`) publishes installers/checksums to `dotnetbuilds/public` and
  `dotnetbuilds/public-checksums`. i.e. we are **already in staging**.

- The versioned blob layout `dotnetup/$(DotnetupVersion)/dotnetup-$(TargetRid)$(_DotnetupNativeExt)`
  is set in [eng/Publishing.props](../../../../eng/Publishing.props) (`SetDotnetupBlobPaths`).

- The install scripts default to `Quality=daily` and resolve `https://aka.ms/dotnet/dotnetup/<quality>`
  ([scripts/get-dotnetup.sh](../../../../scripts/get-dotnetup.sh),
  [scripts/get-dotnetup.ps1](../../../../scripts/get-dotnetup.ps1)).

---

## 3. Comparisons

- **aspire** — owns its own download site (`aspire.dev`) plus npm, WinGet, other packages, and GitHub Releases. Includes tich channel identity (stable / staging / daily / local / pr), release branches, and SHA-derived staging feeds. Promotion is *manual*: the release pipeline is queued against a selected build, extracts its BAR ID, and runs `darc add-build-to-channel`. Product SemVer is hand-authored in `eng/Versions.props`, not auto-bumped.
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
   - Bumps the patch version using global `msbuild` parameters and determines a preview tag.
   - Pushes that tag onto the commit from the selected daily pipeline run.
   - Sets `preview` version metadata property as a global property override using the same methodology as the .NET SDK.
   - Runs tests on that branch.
   - On pass: repins the `/preview/` `aka.ms` url (only if `test` run is not enabled), and creates the GitHub Release with change notes.

**Recovery — if only a simple revert is needed:**
1. Re-run the `release` pipeline against an existing tag and repoint the `/preview/` URL.
   Archival CI URLs exist per version and are confirmed live today, e.g.
   `https://ci.dot.net/public/dotnetup/0.1.4-preview.4.26303.1/dotnetup-win-x64.exe` and its
   `.../public-checksums/dotnetup/0.1.4-preview.4.26303.1/dotnetup-win-x64.exe.sha512` sidecar.
   **Open:** the blob retention/TTL on `dotnetbuilds` is not yet confirmed — if it is finite,
   rollback past that window would require re-publishing rather than just repointing.

**Recovery — fix needed (e.g. security patch on an old branch):**
1. Check out the tagged commit.
2. Create `release/dnup/<tag-version>/hotfix-x.y.z` off the tag.
3. Open an internal PR with the fix into that branch and get approval.
4. Run the release pipeline (which runs tests) and repoint the selector.

This phase allows a public preview.

In this phase, before public preview we'd remove the fallback to the .NET Install Script, but keep the fallback on the `daily` dotnetup builds, and keep `release/dnup` using the `daily` dotnetup builds to build. (This prevents a broken `dotnetup` from preventing us from shipping a new `dotnetup`.)

### Phase 2 — Stable on `builds.dotnet.microsoft.com` (tentative)

Host a `stable` URL on `builds.dotnet.microsoft.com` by coordinating with the release team to use their promotion pipeline — we already push to `dotnetbuilds`, so this is a copy/promote from
`dotnetbuilds` → `dotnetcli` plus storage write permissions granted by the release/dnceng team.

Similar release process, through coordination. Possibly automate `preview` releases at this point.

### Phase 3 — Package-manager acquisition (tentative)

Linux Package Manager Feed / WinGet / Homebrew / etc. Needs discussion with partners.

### Phase 4 — Inclusion in the .NET SDK (unplanned at this time)

If dotnetup ever ships inside the SDK, consider shelling it and adding tests to validate it across all in-support SDK versions.
