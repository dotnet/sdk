# `dotnet test` artifact post-processing (Microsoft.Testing.Platform)

## Overview

When `dotnet test` runs a solution (or several projects) on
[Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro)
(MTP), every test application produces its own artifacts — one TRX report per module, one
code-coverage file per module, and so on. Left as-is, the run summary lists each of those
files individually, and downstream consumers (Azure DevOps `PublishTestResults`,
`ReportGenerator`, CI dashboards) have to glob dozens of paths or merge them out of band.

**Artifact post-processing** consolidates compatible artifacts produced by different test
applications into a single merged artifact, so a multi-project run surfaces one report per
format instead of one per module. It is on by default and runs automatically; nothing in the
project or the command line is required to opt in.

This document describes what the .NET SDK actually does. The upstream concept is defined by
microsoft/testfx
[RFC 018](https://github.com/microsoft/testfx/blob/main/docs/RFCs/018-Artifact-Post-Processing.md);
where the two differ, this document reflects the SDK's behavior.

> This feature applies only to the Microsoft.Testing.Platform runner for `dotnet test`. It is
> unrelated to the VSTest artifact post-processing described under [Caveats](#caveats).

## When it runs

Post-processing happens after every test application has exited and before the run summary is
rendered. To keep the merged report trustworthy, the SDK
[skips it entirely](../../src/Cli/dotnet/Commands/Test/MTP/MicrosoftTestingPlatformTestCommand.cs)
when any of the following is true:

- `--help` was requested (`dotnet test --help`).
- `--list-tests` was requested (test discovery, not execution).
- `--no-artifact-post-processing` was passed.
- The run was cancelled with <kbd>Ctrl</kbd>+<kbd>C</kbd>.
- The run was cut short by `--maximum-failed-tests` or `--timeout`. A truncated run produced
  the artifacts of a truncated run — modules that never started contributed nothing, and
  modules killed mid-flight wrote whatever they had — so merging them into one
  authoritative-looking report would hide the truncation. The per-module artifacts are left
  as they are.

Post-processing can never change the run's exit code; it only affects which artifacts are
listed. See [Failure behavior](#failure-behavior).

## How the SDK decides what to merge

The SDK never inspects artifact contents to decide what is mergeable. It works entirely from
data it already has: the capabilities each test application advertised during the MTP
handshake, and the artifacts that streamed live over the `dotnet-test` pipe during the run.
The grouping and election logic lives in
[`ArtifactPostProcessingPlanner`](../../src/Cli/dotnet/Commands/Test/MTP/ArtifactPostProcessingPlanner.cs).

### Capability comes from the handshake

During its handshake, each MTP test application reports the post-processors registered inside
it through two properties: `SupportedPostProcessorKinds` (reverse-DNS artifact *kinds*, e.g.
`microsoft.testing.trx`) and `SupportedPostProcessorExtensionsLegacy` (lowercase file
extensions, for producers that do not tag a kind). Both are semicolon-separated. An
application that advertises neither simply never participates — it is neither a candidate to
perform a merge nor a source of mergeable groups. The handshake property ids are defined in
[`CliConstants`](../../src/Cli/dotnet/Commands/Test/CliConstants.cs).

### Grouping

Artifacts are de-duplicated by path and then grouped:

1. **Kind first.** Artifacts that carry a reverse-DNS `Kind` are grouped by that kind.
2. **Extension fallback.** Artifacts with no kind are grouped by lowercase file extension.

A group is a merge candidate only if a compatible application advertised the matching kind or
extension, and only if it has **at least two inputs** — merging a single file is pointless. (A
kind group and a matching extension group that individually have one input each can still be
merged together when a shared application supports both and the combined count reaches two.)

For binary code-coverage artifacts (kind `microsoft.codecoverage` / extension `.coverage`),
an application is a candidate only when its architecture matches the inputs. Coverage blobs
are architecture-specific, so an `x64` application is not asked to merge `arm64` coverage.
This constraint applies only to coverage; TRX and other text formats are
architecture-agnostic.

### Election

Multiple applications may be able to merge the same group. The planner runs a greedy minimal
[set-cover](https://en.wikipedia.org/wiki/Set_cover_problem): it repeatedly elects the
application that covers the most still-uncovered groups, so the **fewest** test applications
are relaunched. Ties are broken deterministically — preferring an application that produced
more of the inputs, then the higher target-framework version, then path order — so a given
input set always produces the same plan.

## How the merge is performed

Each elected application is relaunched **once** — not to run tests, but as an MTP tool. The tool
name is passed as the first argument, followed by the manifest
(`<test application> internal-merge-artifacts --manifest <manifest.json>`), and the process is
connected to a fresh `dotnet-test` pipe. The launch, timeout, and argument construction live in
[`TestApplication`](../../src/Cli/dotnet/Commands/Test/MTP/TestApplication.cs); the tool name
and manifest option are defined in
[`CliConstants`](../../src/Cli/dotnet/Commands/Test/CliConstants.cs).

The SDK writes a JSON manifest listing the input artifacts (path, kind, producing module,
target framework, architecture, execution id) and the output directory, then hands it to the
relaunched tool. The merged artifacts flow **back over the same pipe** as ordinary
file-artifact messages, so they re-enter the normal reporter path. In the summary, the SDK
[removes the inputs it consumed and adds the merged output](../../src/Cli/dotnet/Commands/Test/MTP/ArtifactPostProcessingManager.cs)
in their place.

The original per-module artifacts are **never deleted from disk** — only the run summary
changes. If you need the individual files (for example a per-module TRX), they are still where
each module wrote them.

## Where the merged artifact lands

The output directory is chosen by
[`ArtifactPostProcessingManager`](../../src/Cli/dotnet/Commands/Test/MTP/ArtifactPostProcessingManager.cs):

- **With `--results-directory <dir>`**, the merged artifact is written under that directory.
- **Without `--results-directory`**, each application writes beside its own binaries and no
  single directory belongs to the run, so the SDK picks the directory of an input produced by
  the elected application (falling back to the first input directory in path order). The
  merged artifact then lands next to the reports it summarizes instead of inside an unrelated
  project's output.

The merging extension — not the SDK — decides the final file name and may nest its output. The
version of `Microsoft.Testing.Extensions.TrxReport` currently referenced by the SDK names its output
`merged-<runId>.trx`, where `runId` is derived from the inputs, and writes it into a `merged/`
subdirectory of the supplied output directory. Because the merged report is nested, a non-recursive
`*.trx` glob over the results directory picks up the per-module inputs but not the merged report, so
it does not double-count tests. CI that wants the merged report must target the `merged/` subdirectory
explicitly, for example with `merged/merged-*.trx`; a broad recursive glob would also pick up the
per-module inputs and double-count tests.

## What currently merges

Today only **TRX** consolidates: the only shipping post-processor is the one in the
`Microsoft.Testing.Extensions.TrxReport` package. Code coverage and other formats will only be
merged once a post-processor ships in the extension that produces them; until then those
artifacts are listed individually, exactly as before. This is a capability question, not a
configuration one — the SDK merges whatever the installed extensions advertise. The contract for
adding a format is public; see [Extending it to another artifact format](#extending-it-to-another-artifact-format).

## Extending it to another artifact format

The post-processing contract is **public API on `Microsoft.Testing.Platform`**, so a third-party
extension can consolidate its own format without any change to the SDK. It is gated behind the
`TPEXP` experimental diagnostic, so it can still change; suppressing that diagnostic is how you
opt in.

The relevant public types live in `Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing`:

| Type | Role |
|---|---|
| `IArtifactPostProcessor` | The contract: `SupportedKinds`, `SupportedFileExtensionsFallback`, and `ProcessAsync(inputs, outputDirectory, cancellationToken)`. |
| `IArtifactPostProcessingManager` | Registration, via `AddArtifactPostProcessor(Func<IServiceProvider, IArtifactPostProcessor>)`. |
| `InputArtifact` | One input: path, kind, producing test module, target framework, architecture, execution id. |
| `ProcessedArtifact` | The merged result: path, kind, display name, description. |

The dispatcher that runs post-processors, the manifest, and the handshake plumbing are all
internal — an extension never interacts with them.

A minimal processor:

```csharp
#pragma warning disable TPEXP // Artifact post-processing is experimental.
internal sealed class MyArtifactPostProcessor : IArtifactPostProcessor
{
    public string Uid => "Contoso.MyReport.PostProcessor";
    public string Version => "1.0.0";
    public string DisplayName => "Contoso report merger";
    public string Description => "Merges Contoso reports.";

    public IReadOnlyList<string> SupportedKinds { get; } = ["contoso.myreport"];
    public IReadOnlyList<string> SupportedFileExtensionsFallback { get; } = [".myreport"];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs, string outputDirectory, CancellationToken cancellationToken)
    {
        if (inputs.Count < 2)
        {
            return null; // Nothing worth merging; the originals stay listed.
        }

        string output = Path.Combine(outputDirectory, "merged.myreport");
        await MergeAsync(inputs.Select(i => i.Path), output, cancellationToken);
        return new ProcessedArtifact(output, "contoso.myreport", "Contoso report (merged)", null);
    }
}
#pragma warning restore TPEXP
```

What `dotnet test` expects of a processor:

- **Tag the artifacts you produce with the same `Kind` you advertise.** Grouping is by kind first;
  the file-extension list is only a fallback for producers that have not adopted kinds.
- **Do not claim an overly generic fallback extension.** Claiming something like `.xml`, which
  several unrelated formats use, opts your extension into groups of artifacts that are not yours.
  Prefer kind-only routing for formats whose file extension is not distinctive.
- **Return `null` rather than throwing** when there is nothing to do. The SDK only ever asks you to
  merge a group of two or more inputs, but your own policy may still decline (for example, inputs
  a binary format cannot safely combine).
- **Treat inputs as read-only** and write under the supplied `outputDirectory`. Never return one of
  the inputs as your output, and never delete a source file.
- **Set `Kind` on the artifact you return.** That is what the SDK uses to decide which originals the
  merged artifact replaced in the run summary.
- **Be deterministic**: the same set of inputs should produce the same output path.

Two SDK behaviors are worth knowing about. The SDK relaunches the *fewest* test applications that
cover all mergeable groups, so a processor may be asked to merge artifacts produced by a different
test application than the one hosting it. And architecture compatibility is enforced only for code
coverage (kind `microsoft.codecoverage` / extension `.coverage`), because that format is a binary
blob; every other kind may be elected regardless of architecture.

The design behind all of this, including the election algorithm and the reasoning for kinds over
file extensions, is
[microsoft/testfx RFC 018](https://github.com/microsoft/testfx/blob/main/docs/RFCs/018-Artifact-Post-Processing.md).

## Failure behavior

Post-processing is a best-effort convenience layered on top of an already-completed run, so it
**cannot change the run's exit code**. A merge failure, a non-zero exit from the merge host, or
a timeout is degraded to a warning, and the original per-module artifacts stay listed. A run
whose tests all passed still reports success even if merging failed.

## Configuration

| Knob | Effect |
|---|---|
| `--no-artifact-post-processing` | Skip post-processing entirely. Each test application's artifacts are listed individually, one per module. |
| `DOTNET_CLI_TEST_ARTIFACT_POST_PROCESSING_TIMEOUT_SECONDS` | Override the default 15-minute bound on a single merge host. `0` removes the bound (useful for attaching a debugger to a merge host), as does any value large enough that the runtime could not wait on it (above roughly 49.7 days). An absent, non-numeric, or negative value keeps the default. |
| `--results-directory` | Determines the output directory recorded in the manifest (see [Where the merged artifact lands](#where-the-merged-artifact-lands)). It is not passed to the merge host on the command line — the merged output location travels in the manifest so the SDK keeps control of it even when it has to be derived. |
| `--config-file` | Forwarded to the merge host, so it resolves and enables the same extensions as the test run. |
| `--diagnostic-output-directory` | Forwarded to the merge host, so its diagnostic logs land beside the run's. |

## Caveats

- **`--no-build` uses whatever binary is on disk.** If that binary no longer advertises the
  kind (for example it was rebuilt without the TRX extension), post-processing silently does
  nothing — there is no candidate to elect.
- **Merging is scoped to a single `dotnet test` invocation.** There is deliberately no
  cross-invocation correlation. Artifacts produced by separate `dotnet test` runs are never
  merged together.
- **This is not VSTest artifact post-processing.** The VSTest runner has its own, unrelated
  two-phase flow (`--artifactsProcessingMode-collect` / `--artifactsProcessingMode-postprocess`
  with a `--testSessionCorrelationId`) and its own opt-out environment variable,
  `VSTEST_DISABLE_ARTIFACTS_POSTPROCESSING`. That variable has **no effect on MTP runs**, and
  `--no-artifact-post-processing` has **no effect on VSTest runs**. The two mechanisms are
  independent.
- **Retried tests.** `Microsoft.Testing.Extensions.Retry` re-runs only previously failed
  tests, so each attempt's report holds a different subset of results. De-duplicating across
  attempts is the responsibility of the merging extension, not the SDK.

## Troubleshooting

- **Did post-processing run?** When it starts, the SDK prints
  `Merging compatible artifacts produced by different test applications...`. If merging
  succeeded, the artifacts list shows a single merged path in place of the per-module inputs.
  If the line never appears, no group met the [criteria for merging](#how-the-sdk-decides-what-to-merge)
  (for example, only one artifact of a kind, or no installed extension advertised a
  post-processor for it).
- **Merge host command line and failures.** Setting `DOTNET_CLI_TEST_TRACEFILE` to a file path
  enables SDK trace logging, which records the merge host command line and any failure details.
  This is the fastest way to see exactly which application was relaunched and why a merge did
  not happen.
