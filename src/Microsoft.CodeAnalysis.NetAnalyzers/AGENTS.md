# NetAnalyzers Agent Instructions

Guidance for changes under `src/Microsoft.CodeAnalysis.NetAnalyzers` — the .NET code
analyzers (the `CA####` rules).

## Where things live

| Path | Role |
|------|------|
| `src/Microsoft.CodeAnalysis.NetAnalyzers` (+ `CSharp`, `VisualBasic`) | The analyzer assemblies. Rules live under here grouped into `Microsoft.CodeQuality.Analyzers`, `Microsoft.NetCore.Analyzers`, `Microsoft.NetFramework.Analyzers`. |
| `src/Utilities/`| Shared analyzer/flow-analysis helpers linked into the analyzers. |
| `tests/` | Tests and the verifier harness. |
| `tools/GenerateDocumentationAndConfigFiles` | Generates rule docs, rulesets, editorconfig, and SARIF. |

## Conventions & gotchas

- **Release tracking is mandatory (not `PublicAPI.txt`).** Any new/changed/removed
  diagnostic ID **must** be recorded in the project's `AnalyzerReleases.Unshipped.md`
  (it moves to `AnalyzerReleases.Shipped.md` at release). The `RS2000`/`RS2001`
  analyzers fail the build if you skip this.
- **Analyzer file pattern**: `XxxAnalyzer.cs` + a **co-located** `Xxx.Fixer.cs`
  + a test under `tests/…` mirroring the analyzer's folder.
- **Diagnostic IDs are allocated centrally** in
  `src/Utilities/Compiler/DiagnosticCategoryAndIdRanges.txt` — take the next free
  `CA####` in the category's range and update that file.
