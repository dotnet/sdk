# Publishing dotnetup Preview Builds

The `dotnetup` bootstrap scripts install from the `preview` quality by default. Publishing a
build to that quality updates links under:

```text
https://aka.ms/dotnet/dotnetup/preview/
```

Publishing an older build through the same process rolls those links back without rebuilding.

## Select a build

Use a successful
[`dotnet-dnup-official-ci`](https://dev.azure.com/dnceng/internal/_build?definitionId=1544)
run from the `release/dnup` branch. The build's tags include:

```text
BAR ID - <build-id>
```

If the build tags do not include a BAR ID, you can find it at the end of the job `📤 Publish to BAR`, under the step `Publish Using Darc`.

Use that numeric Build Asset Registry (BAR) ID when promoting the build. Normal non-test runs
of the official pipeline are PME-signed and have a `PME Signed` tag. Use the tag to confirm
signing completed; do not promote a test build or a build without the tag.

## Promote the build

Run the
[`Maestro Build Promotion`](https://dev.azure.com/dnceng/internal/_build?definitionId=750)
pipeline with these parameters:

| Parameter | Value |
| --- | --- |
| `BARBuildId` | The BAR ID from the selected build |
| `PromoteToChannelIds` | `10506` (`dotnetup Daily`) |
| `ArtifactsPublishingAdditionalParameters` ( NOT `symbol` parameters ) | `/p:BuildQuality=preview` |

Leave the remaining parameters at their defaults. The Maestro channel retains its historical
`dotnetup Daily` name; `BuildQuality=preview` controls the quality segment in the generated
aka.ms links.

The overall pipeline can report `PartiallySucceeded` because optional artifacts are downloaded
with `continueOnError`. The `Publish packages, blobs and symbols` step must succeed.

## Verify the promotion

Check that the executable link resolves to the selected build's version:

```pwsh
curl.exe -sI https://aka.ms/dotnet/dotnetup/preview/dotnetup-win-x64.exe |
  Select-String Location
```

Then install into an isolated directory and check the running executable:

```pwsh
$script = Join-Path $env:TEMP 'get-dotnetup-preview.ps1'
$installDir = Join-Path $env:TEMP 'dotnetup-preview-test'

Invoke-WebRequest https://aka.ms/dotnet/dotnetup/preview/get-dotnetup.ps1 -OutFile $script
& $script -InstallDir $installDir
& (Join-Path $installDir 'dotnetup.exe') --info
```

## Publish to the CDN through ESRP (initial rollout)

The manually triggered pipeline defined in
[`eng/pipelines/dotnetup-release.yml`](../../../eng/pipelines/dotnetup-release.yml)
publishes an existing signed build to `download.visualstudio.microsoft.com` through ESRP.

### One-time pipeline setup

Create an Azure DevOps pipeline in `dnceng/internal` using this YAML on the release
branch. Authorize the `MicroBuildTemplates` repository, the `Darc: Maestro Production`
service connection, and the internal build pool. The pipeline's build identity must be
able to read official pipeline 1544's runs and build artifacts. Confirm with Engineering
that the new pipeline is authorized for MicroBuild/ESRP publishing and that its owners
and approvers have the required PME accounts.

The initial ESRP owners and approvers are the same:
`noahgilson@microsoft.com`, `daplaist@microsoft.com`, `marcpop@microsoft.com`, and
`jorobich@microsoft.com`. They are configured in the YAML.

### Run and inspect the release

Run the pipeline with `BARBuildId` set to the numeric BAR ID selected above, not the
Azure DevOps run ID. It requires a successful, non-test, PME-signed run of pipeline 1544
on `release/dnup`, with a matching commit and BAR tag. All eight RID binaries, both
bootstrap scripts, and their SHA-512 sidecars must be registered under one dotnetup
version and available in that run's `BlobArtifacts`.
BAR records use versioned paths, but `BlobArtifacts` stores files by their flat
filenames; the pipeline stages only the expected release files, not `MergedManifest.xml`.

The pipeline verifies the original checksums and Windows executable/PowerShell script
signatures in a regular `Prepare` job. That job publishes the staged files, release
metadata, and publishing helper as pipeline artifacts. The `Publish` release job
consumes those artifacts through declared job inputs; it does not check out source
or download build artifacts directly. This follows the
[1ES release-job dependency requirements](https://aka.ms/1espt/release-deps).
It publishes those existing files without rebuilding or re-signing.
After ESRP completes, it downloads every returned CDN URL and checks that its SHA-512
matches the corresponding source asset, including checksum files.

Inspect the `DotnetupCdnRelease` pipeline artifact:

- `bar-build.json`: the selected BAR build record.
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

The pipeline runs offline fixtures for build eligibility, asset completeness, hashes,
signatures, and ESRP result validation before accessing release inputs. To run these
checks locally without publishing or downloading anything:

```pwsh
pwsh -NoProfile -File eng/pipelines/dotnetup-publishing.tests.ps1
```
