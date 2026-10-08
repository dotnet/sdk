# Publishing dotnetup Preview Builds

The `dotnetup` bootstrap scripts install from the `preview` quality by default. Publishing a
build to that quality updates links under:

```text
https://aka.ms/dotnet/dotnetup/preview/
```

Publishing an older build through the same process rolls those links back without rebuilding.

Each official run prebuilds and signs both `daily` and `preview` candidates from the same
commit. They have distinct embedded versions and isolated artifacts. Only the daily candidate
is published automatically; publishing to BAR does not promote the preview candidate.

## Select a build

Use a successful
[`dotnet-dnup-official-ci`](https://dev.azure.com/dnceng/internal/_build?definitionId=1544)
run from the `release/dnup` branch. The build's tags include:

```text
BAR ID - <build-id>
```

If the build tags do not include a BAR ID, find it in the `Record candidate BAR ID` step
(`RecordCandidateBarId`) or the `ReleaseConfigs` artifact.

Use that numeric Build Asset Registry (BAR) ID when promoting the build. Normal non-test runs
of the official pipeline are PME-signed and have a `PME Signed` tag. Use the tag to confirm
signing completed; do not promote a test build or a build without the tag.

One BAR record contains both candidates. The `DotnetupCandidateManifest` artifact retains
the complete registration manifest. `AssetManifests` contains separate
`daily/MergedManifest.xml` and `preview/MergedManifest.xml` publishing manifests. Verify
the selected run has these artifacts before using the promotion instructions below.
Older runs without them require the previous release procedure.

Candidate registration and manifest selection are owned by this repository in
[`eng/dotnetup-build`](../../../eng/dotnetup-build) and
[`dotnetup-publish.yml`](../../../eng/pipelines/templates/jobs/dotnetup-publish.yml).
They reuse the pinned Arcade tasks without modifying `eng/common`. Registration validates
that both candidates are complete and have matching release/build versions before creating
the BAR record.

The publishing job delegates to separate step templates for
[registration](../../../eng/pipelines/templates/steps/dotnetup-register-candidates.yml),
[manifest uploads](../../../eng/pipelines/templates/steps/dotnetup-upload-manifests.yml), and
[daily promotion](../../../eng/pipelines/templates/steps/dotnetup-publish-daily.yml).
The .NET 8 SDK installed during promotion is a Darc prerequisite inherited from the
[Arcade publishing job](../../../eng/common/core-templates/job/publish-build-assets.yml),
not the SDK used to compile dotnetup.

## Promote the build

Run the
[`Maestro Build Promotion`](https://dev.azure.com/dnceng/internal/_build?definitionId=750)
pipeline with these parameters:

| Parameter | Value |
| --- | --- |
| `BARBuildId` | The BAR ID from the selected build |
| `PromoteToChannelIds` | `10506` (`dotnetup Daily`) |
| `ArtifactsPublishingAdditionalParameters` ( NOT `symbol` parameters ) | `/p:BuildQuality=preview /p:BlobBasePath="$(Build.ArtifactStagingDirectory)/BlobArtifacts/preview/"` |

Leave the remaining parameters at their defaults. The Maestro channel retains its historical
`dotnetup Daily` name; `BuildQuality=preview` controls the quality segment in the generated
aka.ms links.

Both properties are required: `BuildQuality` selects the links to update, while
`BlobBasePath` selects the prebuilt candidate's manifest. Leave the
`$(Build.ArtifactStagingDirectory)` macro as written: the promotion pipeline resolves it
on its own agent. This operation does not compile, change versions, or sign again.
For a manual daily promotion, use `BuildQuality=daily` and the `BlobArtifacts/daily/` path.

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
