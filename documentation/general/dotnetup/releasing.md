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

## Publish to blob storage (initial rollout)

The manually triggered pipeline defined in
[`.vsts-dotnetup-release.yml`](../../../.vsts-dotnetup-release.yml)
publishes an existing signed build to the `dotnetup` container in `dotnetorigin`:

```text
https://dotnetorigin.blob.core.windows.net/dotnetup/<version>/<filename>
```

The configured CDN exposes those blobs at:

```text
https://builds.dotnet.microsoft.com/dotnetup/<version>/<filename>
```

Blob names inside the container are `<version>/<filename>`.
The pipeline verifies every CDN download after authenticated origin verification.
It does not publish change aka.ms links.

### One-time pipeline setup

Reuse [dotnetup-release, definition 1710](https://dev.azure.com/dnceng/internal/_build?definitionId=1710)
in `dnceng/internal`, which already uses this YAML filename. Select the branch containing
this implementation when queuing it; renaming the file does not change the pipeline's
configured default branch. Authorize the `dotnetup` pipeline resource, the `MicroBuildTemplates` repository,
and the internal build pool. No Maestro service connection or BAR lookup is required.
The pipeline's build identity must be able to read official pipeline 1544's runs and
build artifacts.

Authorize the Azure Resource Manager service connection named `DotNetUp-PME`,
preferably using workload identity federation. The `blobServiceConnection` pipeline
parameter defaults to this name and can select another approved connection. Its identity
needs blob data-plane read/write access to the existing `dotnetup` container in `dotnetorigin`,
for example a container-scoped Storage Blob Data Contributor role. Management-plane
Contributor access alone is insufficient. The pipeline does not create containers,
change public-access settings, or delete blobs. Public reads use the CDN, not the
authenticated origin endpoint; authenticated upload success alone does not complete a release.

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
publishes the [dotnet-release project](../../../eng/dotnetup-release/dotnetup-release.csproj)
and its Windows x64 runtime as an unsigned, self-contained bundle. Native AOT is
explicitly disabled. The tool is separate from signed product candidates and BAR assets.
Older builds, including layout example 3126346, do not contain this tooling artifact.
The project is included in [sdk.slnx](../../../sdk.slnx); its
[entry point](../../../eng/dotnetup-release/Program.cs) dispatches the `prepare`, `publish`,
and `verify` phases. The executable remains named `dotnetup-release`.

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
the release job declares the tooling bundle and eight `Build_Dotnetup_*` candidate
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
inventory of expected `dotnetorigin/dotnetup` URLs in the storage journal, maps them to
`builds.dotnet.microsoft.com/dotnetup/<version>/<filename>`, and downloads all 20 CDN files anonymously without
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
  and hashes. These are `builds.dotnet.microsoft.com` URLs, not authenticated origin URLs. It is written only
  after every anonymous download passes verification.
- `aka-ms-plan.json`: source build/commit/variant/version, each shortlink, file path/hash,
  observed previous redirect when available, proposed verified CDN target, and disabled
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
after all anonymous CDN downloads pass verification are their actual URLs bound into the
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

Do not point the existing download links at the new CDN URLs during this initial
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
