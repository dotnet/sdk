# Analyzer release tracking

Detail for [design.md](design.md) Rule 7. Each analyzer assembly that contributes
diagnostics to a package needs `AnalyzerReleases.Shipped.md` and
`AnalyzerReleases.Unshipped.md` as `AdditionalFiles`.

## File format

The unshipped file represents the next release and starts empty. Add only the
sections needed by the pending change:

- `### New Rules`;
- `### Removed Rules`;
- `### Changed Rules`.

New and removed rules use this table shape:

```text
Rule ID | Category | Severity | Notes
--------|----------|----------|------
ABCD0001 | Usage | Warning | Short description or documentation link
```

Changed rules must record both states:

```text
Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|------
ABCD0001 | Reliability | Warning | Usage | Info | Reason for the change
```

Use `Disabled` in a severity column when `isEnabledByDefault` is false; otherwise
use the `DiagnosticSeverity` name (`Hidden`, `Info`, `Warning`, or `Error`).

The shipped file is append-only release history. Each release uses a numeric
heading such as `## Release 1.2.0`, followed by the same section/table shapes.
Prerelease labels are not part of the release-heading grammar; record an exact
prerelease package version in a `;` comment when needed.

Once a rule ships, its ID and release metadata are history. Record a later
category, default-severity, or enabled-by-default change under `### Changed
Rules`; record removal under `### Removed Rules`. Do not rewrite an older release
section to describe current behavior.

Suppression descriptors are not supported diagnostics and cannot be live table
rows. Use the commented-row convention in [suppressors.md](suppressors.md) when a
repository chooses to track them alongside rules.

## During development

- New diagnostic: add it to `### New Rules` in the unshipped file.
- Removed shipped diagnostic: add it to `### Removed Rules`.
- Changed category, default severity, or enabled-by-default status of a shipped
  diagnostic: add it to `### Changed Rules`. For a new unshipped diagnostic,
  update its `### New Rules` row instead.
- Build the analyzer project after every edit; the release-tracking analyzer
  checks the files against `SupportedDiagnostics`.

## At release

1. Append a new numeric release section to the shipped file.
2. Move every unshipped section and row under it.
3. Return the unshipped file to its empty header state.
4. Build and pack with the intended package version so the recorded release and
   package agree.

## Diagnostic guide

| ID | Meaning |
| --- | --- |
| RS2000 | A supported diagnostic is missing from analyzer release tracking. |
| RS2001 | A tracked diagnostic entry is not up to date. |
| RS2002 | An unsupported diagnostic appears as New/Changed instead of Removed. |
| RS2003 | A shipped diagnostic is no longer reported but lacks an unshipped Removed entry. |
| RS2004 | A diagnostic marked Removed is still reported by an analyzer. |
| RS2005 | The same diagnostic appears more than once in one release. |
| RS2006 | A diagnostic has a duplicate entry across releases instead of a valid later Changed/Removed entry. |
| RS2007 | The release file contains an invalid heading, table, or entry. |
| RS2008 | Release tracking is not enabled; add both files as `AdditionalFiles`. |

Treat these as contract failures, not warnings to suppress. A stale row can be
more dangerous than a missing row because it publishes the wrong category or
default behavior to consumers.
