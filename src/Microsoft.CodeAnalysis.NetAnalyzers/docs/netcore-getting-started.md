# Getting started with .NET analyzers

The `CA####` analyzers live in `src/Microsoft.CodeAnalysis.NetAnalyzers`, migrated into
`dotnet/sdk` from the retired `dotnet/roslyn-analyzers` repo. PRs target `dotnet/sdk`;
servicing fixes target the relevant `release/<major>.<minor>.<band>xx` branch.

For the step-by-step authoring workflow — ID allocation, resource strings, release
tracking, tests — use the
[`add-net-analyzer`](../../../.github/skills/add-net-analyzer/SKILL.md) skill and
[`AGENTS.md`](../AGENTS.md). This document covers the parts that skill doesn't: the
definition of done, and validating a rule against real code.

## Background reading

1. The [.NET Compiler Platform SDK](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/)
   overview, for the Roslyn concepts (syntax nodes, tokens, trivia) and the factory APIs.
2. The [analyzer/code-fix tutorial](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix),
   which walks an analyzer, a fixer, and unit tests end to end.
3. [Guidelines about new rule ids and docs](guidelines-for-new-rules.md).

## Building

```powershell
./build.cmd -projects src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.slnx -c Debug
```

Use `./build.sh` on Linux/macOS. Do **not** pass `-restore`/`-build` alongside `-projects`;
the driver already implies them and the combination fails. Output lands in
`artifacts/bin/Microsoft.CodeAnalysis.{,CSharp.,VisualBasic.}NetAnalyzers/<Configuration>/netstandard2.0/`.

## Definition of done

- Analyzer implemented to work for C# and VB.
  - Unit tests for C#:
    - All scenarios covered.
      - Prefer markup syntax for the majority of tests.
      - If your analyzer has placeholders in the diagnostic message and you want to test the arguments, write a smaller number of tests using the `VerifyCS.Diagnostic` syntax to construct specific diagnostic forms.
    - Unit tests for VB:
      - Obvious positive and negative scenarios covered.
      - If the implementation uses any syntax-specific code, then all scenarios must be covered.
- Fixer implemented for C#, using the language-agnostic APIs if possible.
  - If the fixer can be entirely implemented with language-agnostic APIs `(IOperation)`, then VB support is essentially free.
  - With a language-agnostic fixer, apply the attribute to indicate the fixer also applies to VB and add mainline VB tests.
  - If language-specific APIs are needed to implement the fixer, the VB fixer is not required.
  - Do not separate analyzer tests from code fix tests. If the analyzer has a code fix, then write all your tests as code fix tests.
    - Calling `VerifyCodeFixAsync(source, source)` verifies that the analyzer either does not produce diagnostics, or produces diagnostics where no code fix is offered.
    - Calling `VerifyCodeFixAsync(source, fixedSource)` verifies the diagnostics (analyzer testing) and verifies that the code fix on source produces the expected output.
  - Fix-all is part of the fixer. `WellKnownFixAllProviders.BatchFixer` applies every fix
    against the original document and merges the results, which produces a wrong tree when
    diagnostics overlap or nest. Derive from
    [`SyntaxEditorBasedCodeFixProvider`](../src/Microsoft.CodeAnalysis.NetAnalyzers/SyntaxEditorBasedCodeFixProvider.cs)
    instead, and cover the nested case in tests.
- Run the analyzer locally against `dotnet/runtime` and `dotnet/roslyn` ([instructions](#validating-against-a-real-codebase)).
  - Review each of the failures in those repositories and determine the course of action for each.
  - Use the failures to discover nuance and guide the implementation details.
  - Document for review: matching and non-matching scenarios, including any discovered nuance.
  - All warnings and errors in these repos are addressed (to prevent build failures).
    - `Info` level diagnostics do not need to be fully resolved or suppressed as they do not cause build failures.
- Document for review: severity, default, categorization, numbering, titles, messages, and descriptions.
- Create the appropriate documentation for [learn.microsoft.com](https://github.com/dotnet/docs/tree/main/docs/fundamentals/code-analysis/quality-rules) within **ONE WEEK**, instructions available on [Contribute docs for .NET code analysis rules to the .NET docs repository](https://learn.microsoft.com/contribute/dotnet/dotnet-contribute-code-analysis).
- PR merged into `dotnet/sdk`.
- Validate the analyzer's behavior with end-to-end testing using the command-line and Visual Studio:
  - Use `dotnet new console` and `dotnet build` from the command-line, updating the code to introduce diagnostics and ensuring warnings/errors are reported at the command-line.
  - Use Visual Studio to create a new project, introduce diagnostics, and observe the warnings/errors/info messages without invoking a build.

## Validating against a real codebase

Unit tests prove the rule fires. They say nothing about the false-positive rate, which is
what actually decides the `RuleLevel` and what reviewers will ask about.

Since the analyzers migrated into `dotnet/sdk` they ship *inside* the SDK, at
`<dotnet-root>/sdk/<version>/Sdks/Microsoft.NET.Sdk/analyzers/`, laid down by
`PublishNETAnalyzers` in
[`GenerateLayout.targets`](../../Layout/redist/targets/GenerateLayout.targets). A repo that
only sets `<AnalysisLevel>` consumes that copy, so overwriting the NuGet cache does nothing
for it. A repo that `PackageReference`s `Microsoft.CodeAnalysis.NetAnalyzers` is the
reverse: the package carries a props file setting `EnableNETAnalyzers=false`, which switches
the SDK copy off in its favour. `dotnet/runtime` does the latter, in
[`eng/Analyzers.targets`](https://github.com/dotnet/runtime/blob/main/eng/Analyzers.targets).
Work out which of the two applies before copying anything.

Either route below changes only what the **command line** uses. At design time Visual Studio
redirects `Microsoft.CodeAnalysis.NetAnalyzers.dll` out of the SDK to its own deployed copy
whenever the major and minor versions match, so VS keeps running the shipped analyzers and
your change appears to do nothing. Set `DOTNET_ANALYZER_REDIRECTING=0` before launching VS
to suppress it;
[`analyzer-redirecting.md`](../../../documentation/general/analyzer-redirecting.md) has the
matching rules.

### Point the target repo at a locally built SDK

The cleanest route. A full `build.cmd` produces a complete SDK containing your analyzers:

```powershell
./build.cmd -c Release
# -> artifacts/bin/redist/Release/dotnet
```

Then run the target repo's build against it, either by setting `DOTNET_ROOT` and putting
that `dotnet` first on `PATH`, or by pointing the target repo's `global.json` at the
version it contains.

### Overwrite the analyzers in an existing SDK

Faster inner loop, and it avoids a full redist build. Build only the analyzer solution,
then copy the three assemblies over the SDK the target repo actually uses — often that
repo's own `.dotnet`. The copy below is PowerShell; use `cp` on Linux/macOS.

```powershell
./build.cmd -projects src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.slnx -c Release

$dest = "<target-repo>/.dotnet/sdk/<version>/Sdks/Microsoft.NET.Sdk/analyzers"
Copy-Item artifacts/bin/Microsoft.CodeAnalysis.CSharp.NetAnalyzers/Release/netstandard2.0/*.dll $dest -Force
Copy-Item artifacts/bin/Microsoft.CodeAnalysis.VisualBasic.NetAnalyzers/Release/netstandard2.0/Microsoft.CodeAnalysis.VisualBasic.NetAnalyzers.dll $dest -Force
```

If the target repo *does* pin the `Microsoft.CodeAnalysis.NetAnalyzers` package, copy into
`~/.nuget/packages/microsoft.codeanalysis.netanalyzers/<version>/analyzers/dotnet/` instead
— into **both** `cs` and `vb`, since the language-agnostic assembly is duplicated into each
— and build with `/p:AssemblyVersion=<version> /p:AutoGenerateAssemblyVersion=false
/p:OfficialBuild=true` so the assembly version matches what the package declares. Build the
target repo with `/bl` and
[read the binlog](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Binary-Log.md#replaying-a-binary-log)
if you are unsure which copy is in play.

### dotnet/runtime

Set your rule to `warning` in
[`eng/CodeAnalysis.src.globalconfig`](https://github.com/dotnet/runtime/blob/main/eng/CodeAnalysis.src.globalconfig)
— e.g. `dotnet_diagnostic.CA1234.severity = warning` — then build. If nothing fires,
introduce a violation to prove the rule actually ran; pick a project nothing else depends
on, so a deliberate error doesn't cascade.

Triage every hit. Reduce false positives in the analyzer, fix the genuine violations, and
suppress only in rare edge cases.

### dotnet/roslyn

`dotnet/roslyn` builds `AnalyzerRunner`, which reports diagnostics over a solution without
requiring an analyzer-enabled build of the whole repo.

1. Build `dotnet/roslyn`: `./Build.cmd -restore -Configuration Release` (their `build.sh` on
   Linux/macOS — see roslyn's own docs for its flags).
2. Build the analyzers here in `Debug`.
3. From the roslyn root, point `AnalyzerRunner` at the analyzer output directory. It
   multi-targets .NET and .NET Framework, so pick the .NET build rather than taking the
   first folder under `artifacts/bin/AnalyzerRunner/Release/`, and launch it through
   `dotnet exec` so the path is the same on every platform:

   ```powershell
   $runner = Get-ChildItem artifacts/bin/AnalyzerRunner/Release/*/AnalyzerRunner.dll |
       Where-Object { $_.Directory.Name -notlike 'net4*' } | Select-Object -First 1
   dotnet exec $runner <sdk-repo>/artifacts/bin/Microsoft.CodeAnalysis.CSharp.NetAnalyzers/Debug/netstandard2.0 `
       ./Roslyn.slnx /stats /concurrent /a <AnalyzerTypeName> /log Output.txt
   ```

   The `/a` value is the analyzer type name. Results land in `Output.txt`.

## Debugging an analyzer inside Visual Studio

1. Build the analyzers in `Debug` and copy them into the SDK the target project uses, as
   above.
2. Open the project you want to analyze in Visual Studio.
3. Analyzers run in `ServiceHub.RoslynCodeAnalysisService.exe`; note its process ID.
   **Code fixes run in `devenv.exe` instead** — attach to that one to debug a
   `CodeFixProvider`.
4. In a second Visual Studio instance, open
   `src\Microsoft.CodeAnalysis.NetAnalyzers\Microsoft.CodeAnalysis.NetAnalyzers.slnx`, set
   your breakpoints, and *Debug -> Attach to Process...* onto the ID from step 3.
5. Type in the first instance; the breakpoints should hit. If they don't, either the build
   you copied and the solution you attached from are out of sync, or VS redirected the
   analyzer — check `DOTNET_ANALYZER_REDIRECTING=0` is set.
