---
core: roslyn-analyzers
core-pin: sdk-local
---

# .NET SDK analyzer overlay

Repository-specific bindings for the portable core beside this file. The core owns
general Roslyn analyzer design, validation, performance, and Fix All guidance. This
overlay owns the workflow for `CA####` rules in
[`src/Microsoft.CodeAnalysis.NetAnalyzers`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/).

Read the area's [`AGENTS.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/AGENTS.md)
before acting. Use the area's
[`sdk-netanalyzers.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md)
when writing the analyzer, fixer, resources, or tests. When porting work from the retired
`dotnet/roslyn-analyzers` repository, also read
[`porting-from-roslyn-analyzers.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/porting-from-roslyn-analyzers.md).

**Project-layout and packaging override:** do not create the separate code-fix assembly or
package these analyzers inside another consuming library as described by the portable
core. This established suite produces the standalone `Microsoft.CodeAnalysis.NetAnalyzers`
package and intentionally builds fixers and Workspaces utilities in its existing core and
language assemblies. The exact binding and local analyzer-rule exceptions are in
[`sdk-netanalyzers.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md#layout).

## Routing

Use this overlay for adding, porting, or changing a .NET code analysis rule implemented
under `src/Microsoft.CodeAnalysis.NetAnalyzers`, including its code fix, resources,
release tracking, generated files, and tests.

Do not use it for:

- `NETSDK####` MSBuild diagnostics, which are generally implemented under `src/Tasks`;
- `CS####`, `BC####`, or `IDE####` compiler and IDE diagnostics, which belong in
  `dotnet/roslyn`; or
- `CONTAINER####` diagnostics, which are implemented under `src/Containers`.

For analyzer adoption, configuration, or authoring outside the NetAnalyzers area, follow
the portable core without these bindings.

## Workflow for a CA rule

### 1. Confirm the rule is accepted

Run the core's existing-analyzer survey before proposing custom code. New CA rules also
require a reviewed proposal before implementation. API-related proposals are triaged in
`dotnet/runtime` under the `code-analyzer` label; non-API proposals are filed in
`dotnet/sdk`. Follow the accepted category, severity, and fixer decision. Ask before
diverging from it.

### 2. Allocate an unclaimed diagnostic ID

`DiagnosticCategoryAndIdRanges.txt` records merged work only, so the next ID may already
be used by an open PR or local branch. Run:

```powershell
./.dotnet/dotnet src/Microsoft.CodeAnalysis.NetAnalyzers/tools/NextDiagnosticId.cs Performance
```

Replace `Performance` with the accepted category. The script scans the working tree,
local branches, and open `dotnet/sdk` PR titles and bodies, then prints the range edit.
Exit `0` means every check ran, `1` means it proposed an ID without checking open PRs,
and `2` is a hard failure. Treat the PR search as a strong heuristic rather than proof.

### 3. Implement the analyzer and optional fixer

Follow the core's [design.md](design.md), [fix-all.md](fix-all.md), and
[performance.md](performance.md), then apply the SDK substitutions and layout in
[`sdk-netanalyzers.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md).
In particular:

- use `DiagnosticDescriptorHelper.Create` and `RuleLevel`;
- use the shared well-known-type, options, argument, pooling, and fixer helpers;
- use the syntax-editor Fix All infrastructure whenever edits can overlap or nest.

Use `IdeSuggestion` for a new rule unless the accepted proposal or evidence supports
another level. `IdeHidden_BulkConfigurable` is the first level that tolerates false
positives; `BuildWarning` can break builds under `TreatWarningsAsErrors`.

### 4. Add and localize resources

Add title, message, and description entries to the owning analyzer group's `.resx` file;
add a code-fix title when applicable. Lock terms that translators must preserve with a
`{Locked="..."}` comment.

Never edit `.xlf` files by hand. Regenerate them against the project that owns the
`.resx` file:

```powershell
./.dotnet/dotnet msbuild src/Microsoft.CodeAnalysis.NetAnalyzers/src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.csproj /t:UpdateXlf
```

Passing `/t:UpdateXlf` to `build.cmd` targets Arcade's `Build.proj` and fails with
`MSB4057`.

### 5. Record release metadata

Add the rule to the declaring project's `AnalyzerReleases.Unshipped.md`. For a
language-agnostic analyzer, that is normally the core
`Microsoft.CodeAnalysis.NetAnalyzers` project. Follow
[release-tracking.md](release-tracking.md), with the SDK-specific `RuleLevel` mapping in
[`sdk-netanalyzers.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md).

Check the documentation URL by eye. It must contain the new rule's own lowercased ID;
nothing validates a copied link that points to a different CA rule. The page may return
404 until the matching `dotnet/docs` PR lands.

### 6. Write focused C# and Visual Basic tests

Use the SDK's MSTest verifier wrappers described in
[`sdk-netanalyzers.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md),
then apply the core's full
[validation.md](validation.md) coverage. C# receives full coverage. Visual Basic receives
at least mainline positive and negative coverage, and full coverage when language-specific
code exists.

When a fixer exists, use code-fix tests for cases that expect the rule's diagnostic,
including cases where no action should be offered. Analyzer-only tests remain appropriate
for no-diagnostic and analyzer-robustness cases where no fixer behavior can be observed.

### 7. Build and run targeted tests

```powershell
./build.cmd -projects src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.slnx -c Debug
```

Use `./build.sh` on Linux or macOS. Do not pass `-restore` or `-build` with `-projects`.
Then invoke the [`run-tests`](../run-tests/SKILL.md) skill for
`Microsoft.CodeAnalysis.NetAnalyzers.UnitTests.csproj`, filtered to the changed test class.
This test project does not consume the assembled SDK, so its runner may skip the redist
freshness check.

The build regenerates committed analyzer documentation and SARIF templates. Include those
generated changes, but never hand-edit them.

### 8. Validate on real code

Before proposing `IdeSuggestion` or stronger, run the built analyzer against a large real
codebase such as `dotnet/runtime` or `dotnet/roslyn` and triage every report. The area's
[`netcore-getting-started.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/netcore-getting-started.md)
explains how to load the locally built analyzer from the SDK or package layout and how to
disable Visual Studio analyzer redirection.

### 9. Arrange the rule documentation

Every `CA####` descriptor receives a Learn help link backed by `ca####.md` in
`dotnet/docs`. A documentation PR is required within one week of the implementation
merging, or the analyzer change may be reverted. Raise this as outstanding work and leave
the remote action to the user unless they explicitly authorize it.

## Completion check

- The rule proposal is accepted and the implementation follows its decisions.
- The ID is unclaimed and its category range is updated.
- Analyzer, fixer, resources, release metadata, generated files, and tests agree.
- C# and Visual Basic coverage includes the designed negative cases.
- Every offered fix produces a valid target shape and preserves trivia.
- Fix All handles overlapping or nested diagnostics when those shapes are possible.
- Targeted build and tests pass.
- Real-code results support the selected `RuleLevel`.
- The required `dotnet/docs` work is explicitly accounted for.

## Updating

While the SDK owns both layers, `core-pin: sdk-local` records that no external immutable
core exists yet. When the portable core is moved to the .NET skills repository, replace
that value with its immutable tag or commit, verify the core is an unchanged mirror, and
review every binding in this overlay against the new pin.
