---
core: roslyn-analyzers
core-pin: sdk-local
---

# .NET SDK analyzer overlay

Use this overlay for `CA####` work under
[`src/Microsoft.CodeAnalysis.NetAnalyzers`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/).
Read the area's [`AGENTS.md`](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/AGENTS.md)
and [SDK bindings](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md).
For archived ports, also read the
[porting guide](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/porting-from-roslyn-analyzers.md).

## Routing

Override the core's new-project layout: use the existing analyzer assemblies and the
standalone `Microsoft.CodeAnalysis.NetAnalyzers` package. Do not add a separate code-fix
project or embed these analyzers in another library package.

Do not use it for:

- `NETSDK####` MSBuild diagnostics, which are generally implemented under `src/Tasks`;
- `CS####`, `BC####`, or `IDE####` compiler and IDE diagnostics, which belong in
  `dotnet/roslyn`; or
- `CONTAINER####` diagnostics, which are implemented under `src/Containers`.

## CA rule workflow

### 1. Confirm the rule is accepted

Require an accepted proposal before implementation. API-related proposals are triaged in
`dotnet/runtime` under `code-analyzer`; non-API proposals are filed in `dotnet/sdk`.
Follow the accepted category, severity, and fixer decision.

### 2. Allocate an unclaimed diagnostic ID

Run the allocator with the accepted category:

```powershell
./.dotnet/dotnet src/Microsoft.CodeAnalysis.NetAnalyzers/tools/NextDiagnosticId.cs Performance
```

Apply its range edit. Exit `1` means open PRs were not fully checked; exit `2` is a hard
failure.

### 3. Implement the analyzer and optional fixer

Apply the core guidance plus the [SDK substitutions and layout](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md).
Use `DiagnosticDescriptorHelper.Create`, `RuleLevel`, and the existing helper APIs. Use
`IdeSuggestion` unless the accepted proposal or evidence supports another level.

### 4. Add and localize resources

Edit the owning `.resx`; never hand-edit `.xlf`. Regenerate localization with:

```powershell
./.dotnet/dotnet msbuild src/Microsoft.CodeAnalysis.NetAnalyzers/src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.csproj /t:UpdateXlf
```

### 5. Record and test the rule

Add the descriptor to the declaring project's `AnalyzerReleases.Unshipped.md`, using the
[SDK `RuleLevel` mapping](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md#rulelevel-and-release-tracking).
Verify that its documentation URL contains the same lowercased rule ID.

### 6. Write focused C# and Visual Basic tests

Use the [MSTest verifier bindings](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/sdk-netanalyzers.md#test-bindings)
and [core coverage guidance](validation.md). Cover C# fully and Visual Basic at least for
mainline positive and negative cases; fully cover language-specific implementations.

### 7. Build and run targeted tests

```powershell
./build.cmd -projects src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.slnx -c Debug
```

Use `./build.sh` on Linux or macOS. Then use [`run-tests`](../run-tests/SKILL.md) for the
NetAnalyzers unit-test project, filtered to the changed test class. Include generated
documentation and SARIF changes; do not hand-edit them.

### 8. Validate on real code

Before proposing `IdeSuggestion` or stronger, run against representative real code and
triage every report. Follow the
[area instructions](../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/netcore-getting-started.md#validating-against-a-real-codebase).

### 9. Arrange the rule documentation

Account for the required `dotnet/docs` page. Its PR is due within one week after the rule
merges.

## Completion check

- Accepted proposal, unclaimed ID, and updated category range.
- Analyzer, fixer, resources, release metadata, generated files, and tests agree.
- Targeted build and tests pass; real-code evidence supports the `RuleLevel`.
- Required `dotnet/docs` work is accounted for.
