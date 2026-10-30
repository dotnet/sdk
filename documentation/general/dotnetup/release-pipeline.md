# dotnetup release pipeline (dry run)

The manual `dotnetup-release` pipeline, defined in
[`.vsts-dotnetup-release.yml`](../../../.vsts-dotnetup-release.yml), consumes
prebuilt preview or placeholder GA candidates from `dotnet-dnup-official-ci`.
It does not build, rebrand, sign, or publish dotnetup.

**There are no external writes and no live-mode switch.** The C#
[release tool](../../../eng/dotnetup-release/Program.cs) performs a read-only
Azure DevOps build lookup, validates downloaded candidates, and prints the proposed
CDN and aka.ms operations. Its JSON plan is uploaded as `dotnetup-release-plan`.
The pipeline has no CDN or aka.ms write credentials.

## Run a rehearsal

1. Select the YAML branch containing the release tool.
2. Select a successful `dotnet-dnup-official-ci` run under the `candidate` pipeline
   resource. It must include the variant pipeline artifacts described in
   [Building dotnetup release candidates](releasing.md), and have the `PME Signed` tag.
3. Choose `quality: preview` or `quality: ga`.
4. Optionally replace the explicit `UNCONFIGURED` aka.ms ownership parameters with
   the intended owners, actor, and group owner. Do not copy another team's ownership.
5. Inspect the console output and `dotnetup-release-plan` artifact.

Development source branches are permitted for rehearsals; this pipeline cannot
publish them. Source build definition, repository, successful completion, signing
tag, commit, and every candidate manifest's build ID/commit must agree. Each quality
must have exactly one candidate for each of the eight supported RIDs, with the same
version. The tool rejects missing or unexpected files, invalid layout or identity,
placeholder mismatches, empty/linked payloads, and SHA-512 mismatches.

Preview plans use `dotnet/dotnetup/preview/` shortlinks. GA plans use the product's
`dotnet/dotnetup/stable/` channel, but **GA artifacts remain placeholders** and
must not be enabled for live promotion.

## Proposed publication sequence

The [planner](../../../eng/dotnetup-release/ReleasePlanner.cs) prints:

1. Versioned uploads to both `dotnetorigin` and `dotnetcli`, in container `dotnet`
   under `dotnetup/<version>/`, including native binaries, bootstrap scripts, and
   checksums. Headers follow the official release pipeline's content-type, cache,
   and attachment conventions.
2. After all uploads and verification succeed, aka.ms lookups followed by PUT for
   an existing link or POST only for a 404, including the corresponding service
   payloads. Targets use `https://builds.dotnet.microsoft.com/dotnet/dotnetup/<version>/`.

The dry run does not authenticate to storage or the redirection service, check
existing blobs/links, or claim the operations would be authorized. These checks are
explicit preconditions in the plan. A future live implementation must create blobs
immutably, verify existing bytes rather than overwrite conflicting versions,
authenticate aka.ms with the documented scope, reject other lookup errors, and
upload/verify every blob before changing any link. Link updates are not atomic.

The implementation follows CDN and redirection-service conventions from
`dotnet/dotnet-release`'s `BlobAssetPublishingService`, `BlobCdnAttributesHelper`,
and `AkaLinksApiService`. Enabling live writes requires a separate reviewed change,
approved service connections and ownership, and a policy for non-placeholder
release eligibility.

## Local planner tests

`ReleasePlannerTests` in [dotnetup.Tests](../../../test/dotnetup.Tests/ReleasePlannerTests.cs)
uses synthetic candidates and does not contact external services. It covers both
qualities, origin/shortlink payloads, provenance, RID completeness/uniqueness,
mixed versions, unexpected files, and corrupted or empty payloads/checksums.
