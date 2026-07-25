# Vendored source files

A small set of source files in this repo are copied (vendored) from other
repositories rather than consumed as a NuGet package. Today the tracked set is
the **`dotnet test` ↔ [Microsoft.Testing.Platform](https://github.com/microsoft/testfx)
shared source**: the named-pipe wire contract and the terminal reporter that the
`dotnet test` command hard-forks from
[`microsoft/testfx`](https://github.com/microsoft/testfx).

We deliberately **copy** these files instead of referencing a source-only NuGet
(`Microsoft.Testing.Platform.Internal.DotnetTest`) because a source-only package
does not flow cleanly through source-build / the VMR
([dotnet/dotnet#7529](https://github.com/dotnet/dotnet/pull/7529)). The trade-off
is drift: when the upstream files change we need to notice and decide whether to
port. That is what this mechanism is for.

We don't want to lose track of these when upstream fixes/updates land. To make
this manageable, the repo tracks each copied file in a manifest and a
[scheduled GitHub Actions workflow](../.github/workflows/check-vendored-files.yml)
opens (or updates) a tracking issue whenever the **upstream** file changes.

The implementation is intentionally simple: no auto-merging, no patch
application, and the **local** file content is never inspected. The bot compares
the *upstream* file's current blob SHA against the recorded baseline and, on any
change, **notifies**; a human decides what (if anything) to port. This is why a
hard fork works fine here — the signal is "upstream moved", not "the two files
differ".

> **Source of truth.** For the shared `dotnet test` source, `microsoft/testfx`
> is the source of truth. It enumerates the exact same set of files via
> `src/Platform/Microsoft.Testing.Platform/ServerMode/DotnetTest/IPC/DotnetTestProtocolContract.props`
> and
> `src/Platform/Microsoft.Testing.Platform/OutputDevice/Terminal/TerminalReporterContract.props`,
> so testfx contributors can see which files are consumed here.

## Files involved

| File | Purpose |
| --- | --- |
| [`vendored-files.json`](./vendored-files.json) | Manifest. Source of truth for every tracked file and its baseline. |
| [`../.github/scripts/check_vendored_files.py`](../.github/scripts/check_vendored_files.py) | Drift detection logic (`validate` + `check` modes). |
| [`../.github/workflows/check-vendored-files.yml`](../.github/workflows/check-vendored-files.yml) | Weekly schedule + manual dispatch + PR validation. |

## Manifest schema

```jsonc
{
  "entries": [
    {
      "id": "stable-kebab-case-id",
      "local_path": "src/path/to/Local.cs",
      "notes": "free-form description of local adaptations",
      "sources": [
        {
          "repo": "owner/repo",
          "ref": "main",
          "path": "path/in/upstream/repo.cs",
          "baseline_ref_sha": "<40-char SHA>",   // upstream branch HEAD at last sync
          "baseline_blob_sha": "<40-char SHA>",  // upstream file blob SHA at last sync (primary drift signal)
          "scope": "optional human description (e.g. 'lines 41-59 only')"
        }
      ]
    }
  ]
}
```

A single local file may declare multiple upstream sources. For example the
terminal reporter is one file in this repo
(`src/Cli/dotnet/Commands/Test/MTP/Terminal/TerminalTestReporter.cs`) but many
partial files upstream (`TerminalTestReporter.*.cs`), so its entry lists each
upstream partial as a separate source. Each source is tracked independently.

## How drift is detected

For every `(entry, source)` pair the workflow:

1. Calls `GET /repos/{repo}/contents/{path}?ref={ref}` to get the current blob SHA.
2. Compares it with `baseline_blob_sha`. Equal → no drift, no issue activity.
3. Otherwise fetches the baseline content (via the blobs API, robust to
   force-pushes) and the current content (via `raw.githubusercontent.com`),
   computes a unified diff, and opens/updates a tracking issue labelled
   `area-vendored-sync` containing:
   - links to the upstream file history, baseline blob, current blob, and the
     whole-repo compare URL,
   - the upstream-only diff (truncated at 300 lines),
   - the local adaptation notes,
   - a reconciliation checklist.

Idempotency uses a hidden marker `<!-- vendored-sync:id={id}:{source-index} -->`
in the issue body, not the title. Existing matching issues are updated in place
when the rendered body changes; otherwise they are left alone.

The workflow never auto-closes issues. A reviewer closes the issue after the
reconciliation PR is merged.

## Adding a new vendored file

1. Add a comment in the local file pointing at the upstream URL (existing files
   use `// Copied from <url>` or a "must be kept aligned with the testfx repo"
   note).
2. Add an entry to `eng/vendored-files.json` with a stable kebab-case `id`,
   the local path, and at least one source. Fill in:
   - `baseline_ref_sha`: the upstream branch SHA you copied from
     (`gh api repos/{repo}/commits/{ref} --jq .sha`),
   - `baseline_blob_sha`: the upstream file's blob SHA at that ref
     (`gh api "repos/{repo}/contents/{path}?ref={ref}" --jq .sha`).
3. Run `python .github/scripts/check_vendored_files.py validate` locally to
   confirm the structure is correct.

## Reconciling drift

When a `[vendored-sync]` issue is opened:

1. Read the upstream diff in the issue body and check the file history link.
2. Port the relevant changes to the local file. If the upstream change is
   irrelevant to the copied region (e.g. an unrelated method was modified),
   no code change is required.
3. Update the corresponding `baseline_ref_sha` and `baseline_blob_sha` in
   `eng/vendored-files.json`.
4. Open a PR with the local change (if any) and the manifest bump in a single
   commit. After it merges, close the tracking issue.

If the issue is closed without bumping the manifest, the next scheduled run
will detect the same drift and open a new issue. That is intentional: closing
without a manifest update is a "remind me later" action.

## Excluded files

Some `dotnet test` shared-source files are intentionally **not** tracked here
because they have no single upstream file to watch:

- `src/Cli/dotnet/Commands/Test/MTP/Terminal/ErrorMessage.cs`,
  `IProgressMessage.cs`, `WarningMessage.cs` — reporter-internal helper types
  that the SDK fork keeps but that testfx has since removed/refactored away.
  There is no upstream file to diff against.
- `src/Cli/dotnet/Commands/Test/MTP/IPC/Models/` and
  `src/Cli/dotnet/Commands/Test/MTP/IPC/Serializers/` — the message models and
  serializers are hand-copied but use a different class shape than testfx
  (upstream still couples them to `TestMetadataProperty`). Only the
  zero-dependency wire contract (`ObjectFieldIds.cs` + the constants in
  `CliConstants.cs`) is tracked; the models/serializers are reconciled together
  with any wire-contract drift the tracked files surface.
