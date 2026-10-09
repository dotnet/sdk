# Publishing dotnetup Preview Builds

The `dotnetup` bootstrap scripts install from the `preview` quality by default. Publishing a
build to that quality updates links under:

```text
https://aka.ms/dotnet/dotnetup/preview/
```

Publishing an older build through the same process rolls those links back without rebuilding.

Daily builds are published automatically. Preview builds require the manual promotion
described below.

Daily candidate versions use `-daily.<BuildNumber>` (for example,
`0.2.0-daily.26508.2`). Preview candidates retain the preview iteration, for example
`0.2.0-preview.1.26508.2`. Both candidates share the release prefix and build number.

The promotion pipeline must include the `BlobAssetIdManifests` support proposed in
[dotnet/arcade#17684](https://github.com/dotnet/arcade/pull/17684). Do not promote these
candidates with an older publishing toolset.

## Select a build

Use a successful
[`dotnet-dnup-official-ci`](https://dev.azure.com/dnceng/internal/_build?definitionId=1544)
run from the `release/dnup` branch. The build's tags include:

```text
BAR ID - <build-id>
```

If the build tags do not include a BAR ID, find it in the `📋 Record candidate BAR ID` step
(`RecordCandidateBarId`) or the `ReleaseConfigs` artifact.

Use that numeric Build Asset Registry (BAR) ID when promoting the build. Normal non-test runs
of the official pipeline are PME-signed and have a `PME Signed` tag. Use the tag to confirm
signing completed; do not promote a test build or a build without the tag.

Verify that the selected run's `AssetManifests` artifact contains `MergedManifest.xml` and `preview.blobids`
before promoting it.

## Promote the build

Run the
[`Maestro Build Promotion`](https://dev.azure.com/dnceng/internal/_build?definitionId=750)
pipeline with these parameters:

| Parameter | Value |
| --- | --- |
| `BARBuildId` | The BAR ID from the selected build |
| `PromoteToChannelIds` | `10506` (`dotnetup Daily`) |
| `ArtifactsPublishingAdditionalParameters` ( NOT `symbol` parameters ) | `/p:BuildQuality=preview /p:BlobAssetIdManifests=preview.blobids` |

Leave the remaining parameters at their defaults. The Maestro channel retains its historical
`dotnetup Daily` name; `BuildQuality=preview` controls the quality segment in the generated
aka.ms links.

Both properties are required: `BuildQuality` selects the links to update, while
`BlobAssetIdManifests` selects the prebuilt candidate's blob IDs from the complete manifest.
The allowlist path is relative to the promotion agent's blob staging directory.
This operation does not compile, change versions, or sign again.
For a manual daily promotion, use `/p:BuildQuality=daily /p:BlobAssetIdManifests=daily.blobids`.

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
