# Building dotnetup release candidates

The official `dotnet-dnup-official-ci` pipeline builds three variants in one Arcade
invocation on each RID agent. Intermediate and output directories are isolated; the
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

To produce variants locally:

```powershell
.\eng\common\build.ps1 -restore -build -pack -configuration Release `
    /p:BuildDotnetupVariants=true /p:DotnetupBuildVariant=daily
```

The pipeline additionally signs and publishes the candidates. Local nonofficial
builds retain Arcade's development versions; an `OfficialBuildId` produces the
date-stamped identities shown above.

## Publishing boundaries

Only non-test official runs on `dnup`, `release/dnup`, or `release/dotnetup` (and the
explicit corresponding internal release branches) immediately publish daily assets.
User/dev branches can register daily assets in BAR but do **not** run the explicit
Darc publishing step. No preview or GA assets can be promoted through the daily BAR
record.

Do not use the former `BuildQuality=preview` Maestro workaround: it changes link
names but cannot select or rebrand preview binaries. Preview/GA promotion must select
the corresponding pipeline artifacts without rebuilding or restamping.
