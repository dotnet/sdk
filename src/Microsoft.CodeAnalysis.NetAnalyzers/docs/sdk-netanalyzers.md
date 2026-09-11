# .NET SDK NetAnalyzers bindings

Concrete repository bindings for the portable analyzer workflow. Paths in this document
are relative to `$NA = src/Microsoft.CodeAnalysis.NetAnalyzers` unless stated otherwise.

## Layout

| Artifact | Location |
| --- | --- |
| Language-agnostic analyzer | `$NA/src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.<Group>.Analyzers/<Category>/<Name>.cs` |
| Language-specific analyzer | The same path under the C# or Visual Basic analyzer project. |
| Code fixer | `<Name>.Fixer.cs` beside the applicable analyzer implementation |
| Unit test | `$NA/tests/Microsoft.CodeAnalysis.NetAnalyzers.UnitTests/Microsoft.<Group>.Analyzers/<Category>/<Name>Tests.cs` |
| Shared analyzer utilities | `$NA/src/Utilities/` |
| Test verifier utilities | `$NA/tests/Test.Utilities/` |

Prefer a language-agnostic `IOperation` analyzer. Add C# or Visual Basic implementations
only when syntax APIs are genuinely required.

The portable core's default advice to create a separate code-fix assembly does **not**
apply to this established suite. NetAnalyzers intentionally compiles language-agnostic
fixers and shared Workspaces utilities into `Microsoft.CodeAnalysis.NetAnalyzers`, and
language-specific fixers into the existing C# or Visual Basic assembly. Its local build
props document and suppress the corresponding compiler-extension warning. Follow the
existing project layout; do not add another code-fix project or enable
`EnforceExtendedAnalyzerRules` merely to match the portable scaffold.

## Repository API substitutions

Use the repository wrappers instead of the general Roslyn APIs in the portable core:

| General API or pattern | Use in NetAnalyzers |
| --- | --- |
| `new DiagnosticDescriptor(...)` | `DiagnosticDescriptorHelper.Create(...)`, which derives the Learn help link and applies telemetry and FxCop compatibility tags. |
| `defaultSeverity` plus `isEnabledByDefault` | `RuleLevel`; its XML documentation is the review rubric. |
| `compilation.GetTypeByMetadataName(...)` | `WellKnownTypeProvider`, with the name declared in `WellKnownTypeNames.cs`. |
| `arguments[i]` for parameter `i` | `arguments.GetArgumentForParameterAtIndex(i)`, or its `Try` overload, so named and reordered arguments work. |
| Hand-written document Fix All | `SyntaxEditorBasedCodeFixProvider`, or `SyntaxEditorFixAllProvider.Create`. |
| Manual parenthesizing in a C# fixer | `SyntaxGeneratorExtensions.Parenthesize`, which adds `Simplifier.Annotation`. |
| Hot-path `HashSet<T>` or `Dictionary<TKey, TValue>` | `PooledObjects` helpers; return them on every path. |
| Direct `AnalyzerConfigOptions` parsing | `AnalyzerOptionsExtensions` and `EditorConfigOptionNames`; reuse existing names. |

`DiagnosticDescriptorHelper.Create` requires `isPortedFxCopRule` and `isDataflowRule`; a
new ordinary rule passes `false` for both. Set `isDataflowRule: true` only for analyzers
built on `$NA/src/Utilities/FlowAnalysis/`; those rules ship disabled because of their
cost. The helper's `isReportedAtCompilationEnd` argument applies the corresponding tag.

Do not compare invocation arguments by source position. `IOperation` presents named and
reordered arguments differently between C# and Visual Basic; match `Parameter.Ordinal`
through the repository extension.

For pooled objects, prefer a `using` declaration such as
`using PooledHashSet<ISymbol> symbols = PooledHashSet<ISymbol>.GetInstance();` so every
return path frees the collection.

## Descriptors and resources

Use `DiagnosticCategory` constants rather than category string literals. Reference
localized strings through `CreateLocalizableResourceString(nameof(<Name>Title))`, normally
with a static import of the owning resource type.

Add these entries to the analyzer group's `.resx` file:

- `<Name>Title`;
- `<Name>Message`;
- `<Name>Description>`; and
- `<Name>CodeFixTitle`, when a fixer exists.

Terms that must not be translated use a resource comment such as
`<comment>{Locked="static readonly"}</comment>`; place multiple lock directives adjacent
with no separator.

## RuleLevel and release tracking

Release tracking uses the level's product meaning, which is not always the descriptor's
`DiagnosticSeverity`:

| `RuleLevel` | `Severity` column |
| --- | --- |
| `BuildError` | `Error` |
| `BuildWarning` | `Warning` |
| `IdeSuggestion`, `BuildWarningCandidate` | `Info` |
| `IdeHidden_BulkConfigurable` | `Hidden` |
| `Disabled`, `CandidateForRemoval` | `Disabled` |

The row belongs in the project that declares the descriptor. Include a documentation link
whose lowercased CA ID exactly matches the row:

```text
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CA#### | Performance | Info | <Name>Analyzer, [Documentation](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca####)
```

Rows move to `AnalyzerReleases.Shipped.md` during release preparation; do not move them as
part of ordinary rule authoring.

## Fixer bindings

Apply the portable core's [fixer design](../../../.github/skills/roslyn-analyzers/design.md#code-fix-providers-optional)
and [Fix All](../../../.github/skills/roslyn-analyzers/fix-all.md) guidance with the
project-layout exception above. Prefer `SyntaxEditorBasedCodeFixProvider` when diagnostics
can overlap or nest; it applies all edits through one `SyntaxEditor` in the required order
while leaving registration under the derived fixer's control. Use
`SyntaxEditorFixAllProvider.Create` directly when inheritance is not possible; its
`TState` overload supports multiple equivalence keys.

## Test bindings

Tests use MSTest, not xUnit: `[TestClass]`, `[TestMethod]`, `[DataRow]`, and
`[DynamicData]`. Use the wrappers in `Test.Utilities`, which already select
`DefaultVerifier`:

```csharp
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.<Category>.<Name>Analyzer,
    Microsoft.NetCore.CSharp.Analyzers.<Category>.CSharp<Name>Fixer>;
```

Apply the portable core's [fixture and coverage guidance](../../../.github/skills/roslyn-analyzers/validation.md).
The embedded C# source defaults to `LanguageVersion.CSharp7_3`; set the test's language
version explicitly when a snippet uses newer syntax. Reference assemblies default to
`AdditionalMetadataReferences.Default`; select the member carrying any packages the
scenario needs.

When a fixer exists, use code-fix tests for positive and negative cases. An identical
input and expected output verifies either no diagnostic or a diagnostic with no offered
fix; use an explicit zero-action assertion when distinguishing those outcomes matters.
C# receives full coverage. Visual Basic receives at least mainline positive and negative
coverage, and full coverage when language-specific code exists.

The unit-test project runs methods in parallel. Keep fixtures, data sources, filesystem
paths, and process-global state safe for concurrent test methods.

## Build outputs and generated files

Build the analyzer solution from the repository root:

```powershell
./build.cmd -projects src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.slnx -c Debug
```

The package project regenerates committed analyzer documentation and SARIF files,
including `Microsoft.CodeAnalysis.NetAnalyzers.md` and
`Microsoft.CodeAnalysis.NetAnalyzers.sarif.template`. CI regenerates them in validation
mode and fails when they are stale. Commit the generated output with the product change;
do not hand-edit it.

The rule documentation link is not validated by this generation step. The generated
`RulesMissingDocumentation.md` remains empty when the generator runs offline, so the
required `dotnet/docs` PR is the only reliable check that the help page will exist.

## Area references

- [`AGENTS.md`](../AGENTS.md) maps the source tree and records build, test, and concurrency
  conventions.
- [`netcore-getting-started.md`](netcore-getting-started.md) defines completion and
  real-code validation.
- [`guidelines-for-new-rules.md`](guidelines-for-new-rules.md) owns proposal, ID, and
  external documentation policy.
- [`analyzer-configuration.md`](analyzer-configuration.md) catalogs supported
  `.editorconfig` options.
- [`writing-dataflow-analysis-based-analyzers.md`](writing-dataflow-analysis-based-analyzers.md)
  applies only when a rule uses the dataflow framework.
