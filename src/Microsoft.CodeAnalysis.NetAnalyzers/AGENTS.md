# NetAnalyzers Agent Instructions

Guidance for changes under `src/Microsoft.CodeAnalysis.NetAnalyzers` — the .NET code
analyzers (the `CA####` rules), migrated here from the retired `dotnet/roslyn-analyzers`.

For the end-to-end workflow of adding or porting a rule, use the
[`add-net-analyzer`](../../.github/skills/add-net-analyzer/SKILL.md) skill.

## Where things live

Paths are relative to `src/Microsoft.CodeAnalysis.NetAnalyzers`.

| Path | Role |
|------|------|
| `src/Microsoft.CodeAnalysis.NetAnalyzers/` (+ `…CSharp.NetAnalyzers/`, `…VisualBasic.NetAnalyzers/`) | The analyzer assemblies. Rules are grouped into `Microsoft.CodeQuality.Analyzers`, `Microsoft.NetCore.Analyzers`, `Microsoft.NetFramework.Analyzers`, then into a category folder. |
| `src/Utilities/{Compiler,Compiler.CSharp,FlowAnalysis,Workspaces}` | Shared analyzer/flow-analysis helpers, linked in as shared projects (`.shproj`) rather than referenced as assemblies. |
| `src/Microsoft.CodeAnalysis.NetAnalyzers.Package.csproj` | Packaging **and** the generated-file regeneration target. |
| `tests/Microsoft.CodeAnalysis.NetAnalyzers.UnitTests/` | Tests, mirroring the analyzer folder structure. |
| `tests/Test.Utilities/` | The `VerifyCS`/`VerifyVB` verifier harness. |
| `tools/GenerateDocumentationAndConfigFiles/` | Generates rule docs, rulesets, editorconfig, and SARIF. |
| `docs/` | Rule-design guidance, the `.editorconfig` option reference, and the dataflow-analysis framework walkthrough. |

## Build & test

```powershell
./build.cmd -projects src/Microsoft.CodeAnalysis.NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.slnx -c Debug

./.dotnet/dotnet test src/Microsoft.CodeAnalysis.NetAnalyzers/tests/Microsoft.CodeAnalysis.NetAnalyzers.UnitTests/Microsoft.CodeAnalysis.NetAnalyzers.UnitTests.csproj --filter "FullyQualifiedName~<Name>Tests"
```

`./build.sh` on Linux/macOS. Do **not** pass `-restore`/`-build` alongside `-projects` —
the driver already implies them and the combination fails. To regenerate `.xlf` after a
`.resx` change, run `/t:UpdateXlf` against the project that owns the resx; passing it to
`build.cmd` fails with `MSB4057`, as Arcade routes the target to its own `Build.proj`.

## Conventions & gotchas

- **Diagnostic IDs are allocated centrally** in
  `src/Utilities/Compiler/DiagnosticCategoryAndIdRanges.txt` — take the ID after the
  category's range end and extend the range. That file only reflects *merged* work, so
  concurrent branches routinely collide;
  `.github/skills/add-net-analyzer/scripts/NextDiagnosticId.cs` checks the working
  tree, local branches, and open PR titles and bodies for you.
- **Release tracking is mandatory (not `PublicAPI.txt`).** Any new, changed, or removed
  diagnostic ID must be recorded in the declaring project's `AnalyzerReleases.Unshipped.md`
  or `RS2000`/`RS2001` fails the build.
- **Analyzer file pattern**: `<Name>.cs` (declaring `<Name>Analyzer`) and its
  `<Name>.Fixer.cs` sit together under `<Group>/<Category>/`, with a test at the mirrored
  path under `tests/…`. A fixer that needs language-specific syntax APIs instead goes to
  the same relative path inside `…CSharp.NetAnalyzers/` or `…VisualBasic.NetAnalyzers/`.
- **Descriptors come from `DiagnosticDescriptorHelper.Create`**, never
  `new DiagnosticDescriptor(...)` — the helper derives the `learn.microsoft.com` help
  link from the ID and applies the telemetry/FxCop-compat tags. `RuleLevel`
  (`src/Utilities/Compiler/RuleLevel.cs`) is the severity knob; its XML doc is the
  rubric reviewers apply.
- **Building rewrites committed files.** `GenerateAnalyzerConfigAndDocumentationFiles` in
  the Package project regenerates `src/Microsoft.CodeAnalysis.NetAnalyzers.md` and
  `src/Microsoft.CodeAnalysis.NetAnalyzers.sarif.template`. CI runs the same generator in
  validate-only mode and fails when they're stale — commit whatever the local build
  produces. It also owns `src/RulesMissingDocumentation.md`, but that file stays empty in
  practice: the help-link check is skipped whenever the generator runs offline, and the
  product build forces offline on. Nothing verifies that a rule's help page exists.
- **Nullable reference warnings are errors** in the analyzer source projects. The unit-test
  project sets `<Nullable>disable</Nullable>`.
- Pooled collections from `src/Utilities/Compiler/PooledObjects/` must be returned on every
  path; prefer `using var x = PooledHashSet<T>.GetInstance();`.

## Tests

- **MSTest** (`[TestClass]`/`[TestMethod]`/`[DataRow]`/`[DynamicData]`), *not* xUnit —
  upstream `dotnet/roslyn-analyzers` tests use xUnit and must be translated when ported.
  `tests/Test.Utilities/TheoryData.cs` is an MSTest-friendly shim for xUnit's `TheoryData`,
  consumable from `[DynamicData]`.
- Embedded C# test sources default to `LanguageVersion.CSharp7_3`; set `LanguageVersion`
  explicitly when the source uses newer syntax.
- `test/ConditionalTests.props` registers a `NetAnalyzers` scope, so PR validation can skip
  these tests when nothing under this directory changed. Shared-infrastructure paths listed
  as global triggers force every scope active, and non-PR CI always runs them.
