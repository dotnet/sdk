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
|---|---|
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

`src/Utilities.UnitTests/` did not migrate — a ported change to `src/Utilities/` has no
test project to land in.

## What reliably breaks on a straight copy

- **Tests are MSTest here, xUnit upstream:**

  | xUnit | MSTest here |
  |---|---|
  | `[Fact]` | `[TestMethod]` |
  | `[Theory]` + `[InlineData(...)]` | `[TestMethod]` + `[DataRow(...)]` |
  | `[Theory]` + `[MemberData(nameof(X))]` | `[TestMethod]` + `[DynamicData(nameof(X))]` |
  | class with no attribute | `[TestClass]` on the class |
  | `Assert.Equal/True/Throws` | `Assert.AreEqual/IsTrue/ThrowsExactly` |
  | `Assert.Equal` on a sequence | `CollectionAssert.AreEqual` — MSTest's `AreEqual` uses `EqualityComparer<T>.Default`, so it compares arrays by reference where xUnit compares element-wise |
  | `xunit.TheoryData<...>` | `Test.Utilities.TheoryData<...>` (shim, up to 4 type args) |
  | `[Fact(Skip = "...")]` | `[Ignore("https://github.com/dotnet/sdk/issues/N")]` |

- **Rule IDs drift.** The ID the upstream PR used is very likely taken now. Re-allocate
  with `scripts/NextDiagnosticId.cs` and rename every occurrence — analyzer,
  `AnalyzerReleases.Unshipped.md`, test markup (`{|CA####:...|}`), and doc comments.
- **Nullable reference warnings are errors.** Upstream code predating a nullable
  annotation change will not compile.
- **`RS0030` (banned `new DiagnosticDescriptor(...)`) did not migrate.** It is a
  convention here rather than an enforced rule; still use
  `DiagnosticDescriptorHelper.Create`.
