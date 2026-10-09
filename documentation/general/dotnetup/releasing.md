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

The promotion pipeline must include the `BlobAssetIdPattern` support proposed in
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

Verify that the selected run's `AssetManifests` artifact contains `MergedManifest.xml`
with both daily and preview candidates before promoting it.

## Promote the build

Run the
[`Maestro Build Promotion`](https://dev.azure.com/dnceng/internal/_build?definitionId=750)
pipeline with these parameters:

| Parameter | Value |
| --- | --- |
| `BARBuildId` | The BAR ID from the selected build |
| `PromoteToChannelIds` | `10506` (`dotnetup Daily`) |
| `ArtifactsPublishingAdditionalParameters` ( NOT `symbol` parameters ) | `/p:BuildQuality=preview /p:BlobAssetIdPattern=^dotnetup/[^/]+-preview[.]%7C^assets/manifests/` |

Leave the remaining parameters at their defaults. The Maestro channel retains its historical
`dotnetup Daily` name; `BuildQuality=preview` controls the quality segment in the generated
aka.ms links.

Both properties are required: `BuildQuality` selects the links to update, while
`BlobAssetIdPattern` filters blob IDs in the complete manifest before publication and
link creation. The expression includes the selected quality's binaries, bootstrap scripts,
and checksums, plus the shared archived manifest. `%7C` is MSBuild's escaped form of the
regex alternation operator `|`; keep it escaped when passing these parameters through
the promotion pipeline. This selection does not modify the BAR inventory or make channel
membership candidate-specific.
This operation does not compile, change versions, or sign again.
For a manual daily promotion, use
`/p:BuildQuality=daily /p:BlobAssetIdPattern=^dotnetup/[^/]+-daily[.]%7C^assets/manifests/`.

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
