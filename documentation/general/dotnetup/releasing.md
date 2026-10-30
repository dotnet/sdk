# Building and releasing dotnetup

The official `dotnet-dnup-official-ci` pipeline invokes the build command three times
on each RID agent, selecting preview, GA, then daily branding with
`DotnetupBuildVariant`. Intermediate and output directories are isolated; the
channel identity is compiled into dotnetup and its libraries, not changed at promotion.

| Variant | Example version | Default self-update channel | BAR |
| --- | --- | --- | --- |
| daily | `0.3.0-daily.26530.1` | daily | Yes |
| preview | `0.3.0-preview.2.26530.1` | preview | No |
| ga | `0.3.0` | stable | No |

GA variants are **placeholders**, not approved releases. They have stable version
branding so the build/release handoff can be exercised before GA support is enabled.

## Artifact contract

Each RID job uploads a `Build_<job-name>` pipeline artifact containing
`<variant>/<version>/`. Each directory contains the RID-suffixed executable, its
SHA-512 checksum, and `release.json` with the source Azure DevOps build ID, commit,
version, variant, RID, schema version, and placeholder flag. The win-x64 artifact
also contains signed bootstrap scripts and their checksums, copied identically into
each variant after signing. Checksums describe the final signed bytes.

All native variants are signed in the normal Arcade signing phase. Preview and GA
use Arcade's `Artifact.SkipPublish` sign-only contract, which also excludes their
checksums from the publishing manifest. Only daily assets are registered in BAR.

Daily publication uses Arcade V4 publishing. Each RID job additionally uploads a
`<job-name>_Artifacts` pipeline artifact containing staged daily assets and a V4
manifest. The BAR job merges these manifests into the `AssetManifests` pipeline
artifact. Preview and GA remain exclusively in the `Build_<job-name>` candidate
artifacts; the release pipeline's input contract is unchanged. The shared SDK build
job templates use V4 publishing without a V3 compatibility option.

To produce variants locally:

```powershell
foreach ($variant in @('preview', 'ga', 'daily')) {
    $artifacts = Join-Path $PWD 'artifacts'
    if ($variant -ne 'daily') { $artifacts = Join-Path $artifacts $variant }
    .\eng\common\build.ps1 -restore -build -pack -configuration Release `
        "/p:Projects=$PWD\dotnetup.slnf" "/p:DotnetupBuildVariant=$variant" `
        "/p:ArtifactsDir=$artifacts\"
    if ($LASTEXITCODE -ne 0) { throw "The $variant build failed." }
}
```

Daily uses the normal `artifacts` root;
preview and GA use `artifacts\preview` and `artifacts\ga` for their isolated outputs.
The final daily invocation additionally signs all three variants, stages the
candidates, and publishes only daily assets. Local nonofficial
builds retain Arcade's development versions; an `OfficialBuildId` produces the
date-stamped identities shown above.

## Releasing

Daily publication uses the build's configured Maestro default channels. Preview and
GA promotion consume the corresponding pipeline artifacts, without rebuilding or
restamping the binaries.

1. Select the input run from
   [dotnet-dnup-official-ci](https://dev.azure.com/dnceng/internal/_build?definitionId=1544).
2. Run [dotnetup-release](https://dev.azure.com/dnceng/internal/_build?definitionId=1710).
   Select that run for the `candidate` pipeline resource and choose `preview` or `ga`
   for `quality`. The source run is an Azure DevOps build, not a BAR build ID.
3. Inspect `release-plan.json` attached to the release execution task's logs.
   The planner validates the eight RID manifests against the selected build and final-byte
   checksums before generating any publication operations.

The release pipeline is currently **dry-run only**. It prints the versioned uploads
to both CDN origin accounts and the conditional aka.ms link updates; it does not call
their write APIs. Aka.ms ownership parameters remain `UNCONFIGURED` until approved.

Preview links use `https://aka.ms/dotnet/dotnetup/preview/`; GA links use
`https://aka.ms/dotnet/dotnetup/stable/`. GA plans remain explicitly marked as
placeholders. Each plan contains 40 blob uploads and 20 link operations, including
the bootstrap scripts and checksums.

## Publish to the CDN through ESRP (initial rollout)

The manually triggered pipeline defined in
[`eng/pipelines/dotnetup-release.yml`](../../../eng/pipelines/dotnetup-release.yml)
publishes an existing signed build to `download.visualstudio.microsoft.com` through ESRP.

### One-time pipeline setup

Create an Azure DevOps pipeline in `dnceng/internal` using this YAML on the release
branch. Authorize the `dotnetup` pipeline resource, the `MicroBuildTemplates` repository,
and the internal build pool. No Maestro service connection or BAR lookup is required.
The pipeline's build identity must be able to read official pipeline 1544's runs and
build artifacts. Confirm with Engineering that the new pipeline is authorized for
MicroBuild/ESRP publishing and that its owners and approvers have the required PME accounts.

Create the `dnup-release-variables` variable group in `dnceng/internal` and authorize
the release pipeline to use it. Define `ESRPOwners` and `ESRPApprovers` in that group
as semicolon-separated lists of full email addresses for the required PME accounts.
Keep these values in the variable group, not in source control.

### Run and inspect the release

In the **Run pipeline** pane, select **Resources**, then select `dotnetup` and choose
a run of `dotnet-dnup-official-ci` (pipeline 1544) from `release/dnup`. Without a manual
selection, the resource defaults to the latest successful run on that branch with the
`PME Signed` tag. The release pipeline remains manually triggered; completing an official
build does not start a CDN release.

The `dryRun` parameter defaults to `true`. A dry run executes `Prepare`, including build
eligibility checks, artifact download, manifest validation, staging, and hash generation,
but skips the entire `Publish` job. Inspect the `DotnetupCdnAssets` artifact for staged files
and `DotnetupReleaseInputs` for the build record, manifest, and release metadata.
No ESRP submission or CDN verification occurs. Set `dryRun` to `false` in the
**Run pipeline** pane to publish the prepared release through ESRP.

Both modes log the source build URL, branch, version, commit, eligibility result, and
staged filenames and sizes, including total bytes. Dry runs explicitly report that ESRP
submission and CDN verification are skipped. Set the pipeline variable `System.Debug`
to `true` for detailed eligibility, manifest, staging-path, per-file SHA-512, and CDN
verification diagnostics. Authentication headers and full API responses are not logged.

The pipeline resource fixes the source pipeline and project. The selected run must be
successful, non-test, and PME-signed. The artifact download selects that exact run;
the pipeline uses its `MergedManifest.xml` to identify the version and expected asset inventory.
It does not require a BAR ID or BAR tag. Manual resource selection can override
the default branch and tag filters, so these checks also reject selections from other
branches and unsigned or failed builds. All eight RID binaries, both
bootstrap scripts, and their SHA-512 sidecars must be listed under one dotnetup
version and available in that run's `BlobArtifacts`.
Manifest entries use versioned paths, but `BlobArtifacts` stores files by their flat
filenames; the pipeline stages only the expected release files, not `MergedManifest.xml`.

The regular `Prepare` job stages the source assets and records their hashes for CDN
verification. That job publishes the staged files, release
metadata, and publishing helper as pipeline artifacts. The `Publish` release job
consumes those artifacts through declared job inputs; it does not check out source
or download build artifacts directly. This follows the
[1ES release-job dependency requirements](https://aka.ms/1espt/release-deps).
It publishes those existing files without rebuilding or re-signing.
After ESRP completes, it downloads every returned CDN URL and checks that its SHA-512
matches the corresponding source asset, including checksum files.

Inspect the `DotnetupCdnRelease` pipeline artifact:

- `source-build.json`: the selected Azure DevOps build record.
- `MergedManifest.xml`: the source build's validated asset manifest.
- `release.json`: source run, commit, version, file paths, and hashes.
- `esrp-release-details.json`: the raw ESRP publishing result, when returned.
- `cdn-release.json`: the verified per-file CDN URLs and hashes, written only after
  every download passes verification.

A failure after ESRP submission can leave files published even though URL verification
failed. Inspect the retained ESRP result before retrying; do not assume a retry reuses
the previous URLs. Neither a failure nor a successful run changes production aliases.

### Before switching aka.ms links

Do not point the existing download links at the new CDN URLs during this initial
rollout. [Self-update resolution](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DailyChannelResolver.cs)
currently extracts the version from the final URL's path, and the
[downloader](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetDownloader.cs)
fetches checksums from a version-specific `ci.dot.net/public-checksums` location.
The [PowerShell](../../../scripts/get-dotnetup.ps1) and
[shell](../../../scripts/get-dotnetup.sh) bootstrap scripts also use the existing blob
layout to pin binary and checksum downloads to the same build. Validate the actual ESRP
URL layout and establish a compatible transition for already-installed clients before
enabling alias updates.

To run offline fixtures for build eligibility, asset completeness, staging hashes,
and ESRP result and CDN download validation locally without publishing or downloading anything:

```pwsh
pwsh -NoProfile -File eng/pipelines/dotnetup-publishing.tests.ps1
```
