# Porting from dotnet/roslyn-analyzers

`dotnet/roslyn-analyzers` is retired: its default branch is now `archive`, which carries no
source tree, so an upstream PR's paths can no longer be corrected at the source.

## Reaching the archived source

`main` still exists and still carries `src/`, so `?ref=main` works. Pin the PR's merge SHA
instead when you want the tree as the PR saw it:

```powershell
gh api "repos/dotnet/roslyn-analyzers/contents/<path>?ref=<main-or-sha>" --jq .download_url
# or, for a whole PR:
gh pr view <N> --repo dotnet/roslyn-analyzers --json commits,files
git fetch https://github.com/dotnet/roslyn-analyzers <sha>
```

Only the **NetAnalyzers** package migrated. `Microsoft.CodeAnalysis.Analyzers`,
`PublicApiAnalyzers`, `BannedApiAnalyzers`, `Roslyn.Diagnostics.Analyzers`,
`Text.Analyzers`, and `PerformanceSensitiveAnalyzers` are not in `dotnet/sdk`.

## Path translation

`$NA` is `src/Microsoft.CodeAnalysis.NetAnalyzers`.

| roslyn-analyzers | dotnet/sdk |
| --- | --- |
| `src/NetAnalyzers/Core/` | `$NA/src/Microsoft.CodeAnalysis.NetAnalyzers/` |
| `src/NetAnalyzers/CSharp/` | `$NA/src/Microsoft.CodeAnalysis.CSharp.NetAnalyzers/` |
| `src/NetAnalyzers/VisualBasic/` | `$NA/src/Microsoft.CodeAnalysis.VisualBasic.NetAnalyzers/` |
| `src/NetAnalyzers/UnitTests/` | `$NA/tests/Microsoft.CodeAnalysis.NetAnalyzers.UnitTests/` |
| `src/Utilities/` | `$NA/src/Utilities/` |
| `src/Test.Utilities/` | `$NA/tests/Test.Utilities/` |
| `src/NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.sarif` | `$NA/src/Microsoft.CodeAnalysis.NetAnalyzers.sarif.template` |
| `RoslynAnalyzers.sln` | `$NA/Microsoft.CodeAnalysis.NetAnalyzers.slnx` |

Below the project root the folder layout is unchanged, so
`src/NetAnalyzers/Core/Microsoft.NetCore.Analyzers/Runtime/Foo.cs` maps to
`$NA/src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.NetCore.Analyzers/Runtime/Foo.cs`.

`src/Utilities.UnitTests/` did not migrate; a ported change to `src/Utilities/` has no
test project to land in.

## What reliably breaks on a straight copy

- **Tests are MSTest here, xUnit upstream.** The
  [`migrate-xunit-to-mstest`](../../../.github/skills/migrate-xunit-to-mstest/SKILL.md)
  skill carries the attribute, assertion, and lifecycle mapping. Two SDK details remain:
  upstream `xunit.TheoryData<...>` maps to the `Test.Utilities.TheoryData<...>` shim, up to
  four type arguments, and a skipped test uses `[TestMethod]` plus
  `[Ignore("https://github.com/dotnet/sdk/issues/N")]`.
- **Rule IDs drift.** The ID used by an old PR is likely taken now. Reallocate it with
  [`NextDiagnosticId.cs`](../tools/NextDiagnosticId.cs), then rename every analyzer,
  release-tracking, test-markup, and documentation occurrence.
- **Nullable reference warnings are errors.** Older code may require annotations before
  it compiles in the SDK.
- **`RS0030` did not migrate.** The ban on `new DiagnosticDescriptor(...)` is a repository
  convention rather than an enforced rule here; use `DiagnosticDescriptorHelper.Create`.
