---
name: add-net-analyzer
description: >
  Add, port, or change a .NET code analysis rule (CA####) under
  src/Microsoft.CodeAnalysis.NetAnalyzers. USE FOR: implementing a new CA analyzer and
  its code fixer, porting a rule or PR from the retired dotnet/roslyn-analyzers repo,
  allocating a diagnostic ID from DiagnosticCategoryAndIdRanges.txt, choosing
  RuleLevel/severity/category, wiring resx + xlf strings, recording the rule in
  AnalyzerReleases.Unshipped.md, regenerating the analyzer documentation/sarif files, and
  writing MSTest analyzer/code-fix tests with the VerifyCS/VerifyVB harness. DO NOT USE
  FOR: NETSDK#### MSBuild diagnostics (src/Tasks), CS####/BC#### compiler diagnostics or
  IDE#### analyzers (dotnet/roslyn), or CONTAINER#### diagnostics (src/Containers).
license: MIT
---

# Add or port a .NET code analysis (CA) rule

[`AGENTS.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/AGENTS.md) maps the tree and
carries the build/test commands and environment gotchas. Paths below are relative to `$NA`
= `src/Microsoft.CodeAnalysis.NetAnalyzers`.

| File | Load when |
|---|---|
| [`references/authoring-patterns.md`](references/authoring-patterns.md) | Writing the analyzer, the fixer, or the tests. |
| [`references/porting-from-roslyn-analyzers.md`](references/porting-from-roslyn-analyzers.md) | Porting a rule or PR from the archived `dotnet/roslyn-analyzers`. |

## 1. Confirm the rule is wanted

New CA rules are proposed and triaged before implementation — .NET API-related ones in
`dotnet/runtime` under the `code-analyzer` label. If an API review already decided the
category, severity, and whether a fixer is wanted, **follow that decision** and cite it in
the PR rather than re-deriving one. Ask before diverging from it.

## 2. Allocate the diagnostic ID

`DiagnosticCategoryAndIdRanges.txt` records only *merged* work, so the "next" ID is
routinely already claimed by an open PR or a concurrent branch. Run:

```powershell
./.dotnet/dotnet .github/skills/add-net-analyzer/scripts/NextDiagnosticId.cs Performance
```

It scans forward from the end of the category's range until it finds an ID unclaimed in the
working tree, on any local branch, and in any open `dotnet/sdk` PR, prints the exact range
edit to apply, and reports anything it skipped. Exit `0` means every check ran, `1` means
the ID is proposed but open PRs went unchecked, and `2` is a hard failure — including "every
candidate in the scan window is already taken". The PR check matches titles and bodies
rather than diffs, so treat it as a strong heuristic, not proof.

## 3. Implement the analyzer and fixer

Read [`references/authoring-patterns.md`](references/authoring-patterns.md) first.

The language-agnostic analyzer goes in
`$NA/src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.<Group>.Analyzers/<Category>/<Name>.cs`,
the fixer in `<Name>.Fixer.cs` beside it. Derive C#/VB types only where you genuinely need
syntax; those go at the same relative path inside
`$NA/src/Microsoft.CodeAnalysis.CSharp.NetAnalyzers/` or `…VisualBasic.NetAnalyzers/`. The
folder is the *rule group's* category — the `category:` you report can differ, and comes
from the `DiagnosticCategory` constants, never a raw string. The decisions that are yours
rather than pattern-matching an existing rule:

- **`RuleLevel`** — `IdeSuggestion` unless you have a reason. Everything at `IdeSuggestion`
  or stronger is required to have **no false positives**; `IdeHidden_BulkConfigurable` is
  the first level that tolerates any, and `BuildWarning` additionally breaks builds under
  `TreatWarningsAsErrors`.
- **Whether to ship a fixer at all** — if applying it could change semantics, report the
  diagnostic without one.

The two things reviewers reject on:

- **The analyzer owns the eligibility decision.** Binding a symbol does not guarantee the
  syntax has the shape you expect, and a fixer that declines because the code was never
  eligible leaves the user with a lightbulb that does nothing. (Its own defensive checks
  against a stale span stay.)
- **Fix-all.** `WellKnownFixAllProviders.BatchFixer` produces a wrong tree when diagnostics
  overlap or nest. If yours can, fix the whole document in one pass, inside-out.
- **A fix must produce compiling code**, on every shape it offers itself on — not just on
  the shapes the tests cover. If a rewrite is only valid in some contexts, the fixer
  declines in the rest; report the diagnostic without a fix rather than emitting a build
  break. Narrowing the tests to the safe shapes hides the bug instead of fixing it.

## 4. Add the strings

Append to the resx for the rule group — e.g.
`$NA/src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.NetCore.Analyzers/MicrosoftNetCoreAnalyzersResources.resx`
— one entry each for
`<Name>Title`, `<Name>Message`, `<Name>Description`, plus `<Name>CodeFixTitle` if there is
a fixer. Reference them via `CreateLocalizableResourceString(nameof(<Name>Title))` with
`using static <Resources>;` at the top of the namespace.

`<Name>CodeFixTitle` names the *action* the fix performs ("Extract to static readonly
field"), not the problem the analyzer reports. Terms that must not be translated get
`<comment>{Locked="static readonly"}</comment>`; multiple terms are adjacent braces with no
separator.

Then regenerate the 13 `.xlf` files in the `xlf/` subfolder beside it. Run the target
against the project that owns the resx — passing `/t:UpdateXlf` to `build.cmd` fails with
`MSB4057`, because Arcade applies the target to its own `Build.proj` rather than to the
projects being built:

```powershell
./.dotnet/dotnet msbuild src/Microsoft.CodeAnalysis.NetAnalyzers/src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.csproj /t:UpdateXlf
```

## 5. Record the rule in release tracking

Each of the three analyzer projects has its own `AnalyzerReleases.Unshipped.md` at its
root; the row goes in the project that *declares the descriptor*, which for a
language-agnostic rule is `Microsoft.CodeAnalysis.NetAnalyzers`. The `RS2000`/`RS2001`
meta-analyzers fail the build if you skip this, and they ship a code fix that writes the
row for you.

```
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CA#### | Performance | Info | <Name>Analyzer, [Documentation](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca####)
```

`Severity` is the release-tracking severity for your `RuleLevel`, which is **not** always
the descriptor's `DiagnosticSeverity` — a disabled rule carries `Warning` on the descriptor
but tracks as `Disabled`:

| `RuleLevel` | `Severity` column |
|---|---|
| `BuildError` | `Error` |
| `BuildWarning` | `Warning` |
| `IdeSuggestion`, `BuildWarningCandidate` | `Info` |
| `IdeHidden_BulkConfigurable` | `Hidden` |
| `Disabled`, `CandidateForRemoval` | `Disabled` |

The `Documentation` link is the **lowercased** ID and must match the rule ID in the same
row. Nothing validates it, so a row copy-pasted from the one above — carrying that rule's ID
or a stray capital — silently ships a link to the wrong page. Check it by eye; wrong links
are already in these files.

Rows move to `AnalyzerReleases.Shipped.md` at release time — don't move them yourself.

## 6. Write the tests

Tests go in
`$NA/tests/Microsoft.CodeAnalysis.NetAnalyzers.UnitTests/Microsoft.<Group>.Analyzers/<Category>/<Name>Tests.cs`,
mirroring the analyzer's folder. Full conventions are in
[`references/authoring-patterns.md`](references/authoring-patterns.md); the coverage bar:

- C# fully; VB at least mainline positive and negative, fully if any VB-specific code
  exists. Group by behavior and cover both languages in one test method.
- When a fixer exists, write *every* test as a code-fix test, and include a trivia case.
  If the diagnostic can nest, add a nested case — that is what catches a broken fix-all.
- The negative cases you reasoned about while designing. Reviewers will ask for them.

## 7. Build and test

```powershell
# ~10s incremental once .dotnet is provisioned; the first run provisions it.
./build.cmd -projects src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.slnx -c Debug

./.dotnet/dotnet test src/Microsoft.CodeAnalysis.NetAnalyzers/tests/Microsoft.CodeAnalysis.NetAnalyzers.UnitTests/Microsoft.CodeAnalysis.NetAnalyzers.UnitTests.csproj --filter "FullyQualifiedName~<Name>Tests"
```

Then `git status` and commit the regenerated files along with your change. `./build.sh` on
Linux/macOS; never pass `-restore`/`-build` alongside `-projects`.

## 8. Validate the rule against real code

Unit tests prove the rule fires; they don't tell you the false-positive rate, and that is
what decides the `RuleLevel`. Every level from `IdeSuggestion` up requires **no false
positives**, so before proposing one, run the built analyzer over a large real codebase
(`dotnet/runtime`, `dotnet/roslyn`) and triage every hit. Report the result in the PR.
[`docs/netcore-getting-started.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/netcore-getting-started.md)
has the mechanics and the full definition of done.

## 9. Documentation

Each `CA####` is auto-assigned the help link
`https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca####`,
backed by `ca####.md` in
[`dotnet/docs`](https://github.com/dotnet/docs/tree/main/docs/fundamentals/code-analysis/quality-rules).
A docs PR is required **within one week** of the rule merging, or the implementation may be
reverted. Say so in the PR description, then open the docs PR.

## Checklist

- [ ] Rule proposal reviewed and accepted, and the decision cited in the PR.
- [ ] ID unclaimed by any local branch or open PR, and the category's range extended to
      cover it.
- [ ] `AnalyzerReleases.Unshipped.md` row added, its `Documentation` link matching the row's
      own ID in lowercase.
- [ ] `.resx` edited and `.xlf` regenerated via `/t:UpdateXlf` — neither hand-edited.
- [ ] Regenerated `.md` / `.sarif.template` / `RulesMissingDocumentation.md` committed.
- [ ] Targeted test run passes, covering VB and the negative cases.
- [ ] Fix-all handles nesting (not the batch fixer), if the diagnostic can overlap or nest.
- [ ] Every shape the fix offers itself on produces compiling code.
- [ ] Rule run against a real codebase and hits triaged, at `IdeSuggestion` or stronger.
- [ ] `dotnet/docs` PR opened, or explicitly committed to in the PR description.

