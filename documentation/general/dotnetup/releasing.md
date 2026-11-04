# Building and releasing dotnetup

The official `dotnet-dnup-official-ci` pipeline invokes the build command three times
on each RID agent, selecting preview, GA, then daily branding with
`DotnetupBuildVariant`. Intermediate and output directories are isolated; the
channel identity is compiled into dotnetup and its libraries, not changed at promotion.

| Variant | Example version | Default self-update channel | BAR |
| --- | --- | --- | --- |
| daily | `0.3.0-daily.26530.1` | daily | Yes |
| preview | `0.3.0-preview.1.26530.1` | preview | No |
| ga | `0.3.0` | stable | No |

GA variants are **placeholders**, not approved releases. They have stable version
branding so the build/release handoff can be exercised before GA support is enabled.

## Artifact contract

Each RID job uploads a `Dotnetup_<job-name>` pipeline artifact containing
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
artifact. Preview and GA remain exclusively in the `Dotnetup_<job-name>` candidate
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
