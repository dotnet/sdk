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

## Publish to blob storage (initial rollout)

The manually triggered pipeline defined in
[`.vsts-dotnetup-release.yml`](../../../.vsts-dotnetup-release.yml)
publishes an existing signed build directly to:

```text
https://dotnetcli.blob.core.windows.net/dotnet/dotnetup/<version>/<filename>
```

It does not use ESRP, publish to `dotnetorigin`, or change aka.ms links. Direct uploads
to `dotnetcli` do not establish availability at `builds.dotnet.microsoft.com`; the
[dotnet-release production configuration](https://dev.azure.com/dnceng/internal/_git/dotnet-release?path=/src/release-pipeline/ReleasePipeline.Configs/Configuration.Prod.yaml&version=GBmain)
distinguishes the direct-access account from the CDN-origin account.

### One-time pipeline setup

Reuse [dotnetup-release, definition 1710](https://dev.azure.com/dnceng/internal/_build?definitionId=1710)
in `dnceng/internal`, which already uses this YAML filename. Select the branch containing
this implementation when queuing it; renaming the file does not change the pipeline's
configured default branch. Authorize the `dotnetup` pipeline resource, the `MicroBuildTemplates` repository,
and the internal build pool. No Maestro service connection or BAR lookup is required.
The pipeline's build identity must be able to read official pipeline 1544's runs and
build artifacts.

Create or authorize an Azure Resource Manager service connection named `dotnetup-release`,
preferably using workload identity federation. The `blobServiceConnection` pipeline
parameter defaults to this name and can select another approved connection. Its identity
needs blob data-plane read/write access to the existing `dotnet` container in `dotnetcli`,
for example a container-scoped Storage Blob Data Contributor role. Management-plane
Contributor access alone is insufficient. The pipeline does not create containers,
change public-access settings, or delete blobs. Confirm that the published URLs support
anonymous reads; authenticated upload success alone does not complete a release.

No ESRP owners, approvers, or `dnup-release-variables` variable group are required.
MicroBuild publishing authorization is disabled. `AzureCLI@2` authenticates through the
selected service connection and acquires an Azure Storage bearer token. The token is
masked, passed only to the publishing executable through `BLOB_ACCESS_TOKEN`, and removed
from the task environment afterward; it is not retained in release metadata.

### Run and inspect the release

In the **Run pipeline** pane, select **Resources**, then select `dotnetup` and choose
a run of `dotnet-dnup-official-ci` (pipeline 1544) from `release/dnup`. Without a manual
selection, the resource defaults to the latest successful run on that branch with the
`PME Signed` tag. The release pipeline remains manually triggered; completing an official
build does not start a CDN release.

The source build must contain `DotnetupReleaseTools` as well as all eight candidate
artifacts introduced by [#56864](https://github.com/dotnet/sdk/pull/56864).
The `build_release_tools` stage in [`.vsts-dnup-ci.yml`](../../../.vsts-dnup-ci.yml)
publishes the [file-based C# app](../../../eng/dotnetup-release/dotnetup-release.cs)
and its Windows x64 runtime as an unsigned, self-contained bundle. Native AOT is
explicitly disabled. The tool is separate from signed product candidates and BAR assets.
Older builds, including layout example 3126346, do not contain this tooling artifact.

The `dryRun` parameter defaults to `true`. Both modes validate build eligibility,
candidate manifests and checksums, stage files, and take read-only snapshots of public
aka.ms redirects. A dry run skips Azure authentication, blob uploads, and public downloads;
it makes no external writes. Set `dryRun` to `false` to publish the staged release to
blob storage and verify anonymous downloads. **Neither mode writes aka.ms links.**

The `variant` parameter selects `preview` (the default) or `daily`. Each selection consumes
that variant's already-built, signed candidates; it does not rebrand daily binaries.
GA candidates are placeholders and cannot be selected. Selecting a variant does not change
aka.ms links.

Both modes log the source build URL, branch, variant, version, commit, eligibility result, and
staged relative paths and sizes, including total bytes. Dry runs explicitly report that blob
uploads and public download verification are skipped. Set the pipeline variable `System.Debug`
to `true` for detailed eligibility, manifest, staging-path, per-file SHA-512, and download
verification diagnostics. Authentication headers and full API responses are not logged.

The pipeline resource fixes the source pipeline and project. The selected run must be
successful, non-test, and PME-signed. The artifact download selects that exact run;
the release job declares the tooling bundle and eight `Dotnetup_Dotnetup_*` candidate
pipeline artifacts as inputs, and the tool selects `<variant>/<version>/` locally.
These candidate artifacts are separate
from the V4 publishing artifacts and `AssetManifests`, which describe only daily assets.
The pipeline uses each candidate's `release.json` to validate its schema, source build ID,
commit, variant, version, RID, and non-placeholder status.
It does not require a BAR ID or BAR tag. Manual resource selection can override
the default branch and tag filters, so these checks also reject selections from other
branches and unsigned or failed builds. Exactly one candidate for each of the eight RIDs
must agree on a single version with the selected variant's prerelease label. Both bootstrap
scripts come from the Windows x64 candidate. All binaries, scripts, and SHA-512 sidecars
must be present and nonempty, and each sidecar must match its payload.
The [staging implementation](../../../eng/dotnetup-release/ReleasePreparation.cs) combines the selected
files into `dotnetup/<version>/<filename>` under the local staging root:
`dotnetup/<version>/dotnetup-<rid>` (with `.exe` on Windows). The bootstrap scripts and
all SHA-512 sidecars share the same version directory. Only the 20 expected release files
are staged, not candidate manifests, other variants, or artifact signing metadata.

One formal 1ES release job prepares the files and, unless dry-running, publishes them
with the downloaded executable using the Azure Blob REST API. The upload implementation
uses the BCL rather than introducing Azure NuGet dependencies; Azure CLI handles service-
connection authentication only. The job does not check out source, bootstrap an SDK,
compile, rebuild, or re-sign anything. Its inputs follow the
[1ES release-job dependency requirements](https://aka.ms/1espt/release-deps).
Unlike the planning-only prototype in [#56865](https://github.com/dotnet/sdk/pull/56865),
this implementation retains the source-build API lookup and eligibility checks. The
read uses `System.AccessToken`; the .NET tool checks the returned build record against
the selected pipeline resource's build ID and commit.
The [blob publisher](../../../eng/dotnetup-release/BlobPublishing.cs) checks every staged
hash and reads all existing destination blobs before writing any missing files. Conflicting
content fails the release without overwriting it. Missing files are uploaded as block
blobs using `If-None-Match: *`; a concurrent upload or lost successful response is accepted
only after the stored bytes match the source SHA-512. Staged bytes are rechecked while
opening each upload stream, including retries. Existing checksum sidecars are uploaded
verbatim, not regenerated or reformatted.

Uploads set `Content-Disposition: attachment`, `text/plain` for checksums, and
`application/octet-stream` for other files. Cache lifetimes are 900 seconds for scripts
and checksums, 864000 seconds for Windows executables, and 86400 seconds for extensionless
binaries. Transient request failures use up to five retries with exponential backoff
capped at ten seconds; requests and download-body reads have 300-second timeouts.

After authenticated storage verification, the
[download verifier](../../../eng/dotnetup-release/CdnVerification.cs) requires an exact
inventory of expected `dotnetcli/dotnet` URLs, downloads all 20 files anonymously without
following redirects, and checks their SHA-512 hashes, including checksum sidecars.
Storage verification does not substitute for public verification.

The final step attaches top-level metadata files and `dotnetup-release-metadata.zip`
to its task logs, including on failure. The archive preserves per-RID manifest paths
without colliding with the release inventory's filename. There are no release-job
pipeline-artifact outputs. Inspect:

- `source-build.json`: the selected Azure DevOps build record.
- `candidates/<rid>/release.json`: each validated source candidate manifest.
- `release.json`: source run, commit, variant, version, source and staged file paths, and hashes.
- `blob-publication.json`: source identity, URLs, hashes, and incremental per-file
  publication progress; `storage-verified` means every stored blob matched the source.
- `cdn-release.json`: the retained metadata filename for verified public download URLs
  and hashes. These are direct blob URLs, not ESRP or CDN-origin URLs. It is written only
  after every anonymous download passes verification.
- `aka-ms-plan.json`: source build/commit/variant/version, each shortlink, file path/hash,
  observed previous redirect when available, proposed verified blob target, and disabled
  operation status.

A failed release can leave some versioned blobs published. Inspect the retained publication
journal and task errors before retrying. An `uploading` entry can represent an upload whose
result was not verified. Retrying the same signed build reuses byte-identical blobs and
uploads missing files; different bytes at the same versioned path are rejected, not replaced.
No automatic rollback deletes published blobs. Neither failure nor success changes aliases.

### Before switching aka.ms links

The [link planner](../../../eng/dotnetup-release/AkaLinkPlanning.cs) records all 20 proposed
links under `https://aka.ms/dotnet/dotnetup/<variant>/`, including checksum sidecars.
Before public download verification, every proposed target is explicitly unresolved. Only
after all anonymous blob downloads pass verification are their actual URLs bound into the
plan. There is no alias-write switch.

The planner makes public HEAD requests without following redirects. A usable redirect
is a timestamped observation, not an authoritative aka.ms management record or a
guaranteed rollback target. HTTP failures, timeouts, missing links, and Bing fallback
redirects are recorded explicitly as unavailable with an error; they do not prevent
blob-only publishing. A future implementation must obtain approved ownership and
authentication, re-read management-service state, update only existing links on 200,
create only on 404, and fail on other statuses. All uploads and verification must finish
before any alias changes. Link updates are not atomic, so retain previous targets and
record individual outcomes for rollback.

Do not point the existing download links at the new blob URLs during this initial
rollout. [Self-update resolution](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DailyChannelResolver.cs)
currently extracts the version from the final URL's path, and the
[downloader](../../../src/Installer/Microsoft.Dotnet.Installation/Internal/DotnetDownloader.cs)
fetches checksums from a version-specific `ci.dot.net/public-checksums` location.
The [PowerShell](../../../scripts/get-dotnetup.ps1) and
[shell](../../../scripts/get-dotnetup.sh) bootstrap scripts also use the existing blob
layout to pin binary and checksum downloads to the same build. Validate the new blob
URL layout and establish a compatible transition for already-installed clients before
enabling alias updates.

`ReleaseToolTests` in [dotnetup.Tests](../../../test/dotnetup.Tests/ReleaseToolTests.cs)
uses synthetic candidates and mocked HTTP responses for build eligibility, variant
selection, provenance, asset completeness, staging hashes, blob headers and create-only
uploads, idempotent retries, conflict rejection, anonymous download verification, and
disabled aka.ms plans. It does not publish or contact external services.
Use the [focused test entry point](../../../scripts/RunTests.cs):

```pwsh
.\.dotnet\dotnet.exe scripts\RunTests.cs -- `
  --project test\dotnetup.Tests\dotnetup.Tests.csproj `
  --filter "FullyQualifiedName~ReleaseToolTests" --skip-redist-check
```

For local executable validation on multiple RIDs, give each RID a separate `ArtifactsDir`
when publishing the tooling bundle. This prevents incremental builds from reusing an
assembly compiled for another architecture. `dotnet publish` creates a local executable
bundle; only the release tool's authenticated `publish` command uploads blobs.
