# Check applicable existing mechanisms

Use this check when the requested solution is open-ended. If the task explicitly
targets a custom analyzer, an approved diagnostic, or behavior owned by a specific
suite, note relevant overlap and continue rather than refusing the work.

## Quick check

### Existing first-party rule

Modern .NET projects can use the SDK's **`Microsoft.CodeAnalysis.NetAnalyzers`**
(`CA####` quality rules) and Roslyn **`IDE####`** code-style rules. CA rules
participate in SDK builds according to `AnalysisLevel` and `AnalysisMode`; IDE rules
are available in the editor, but code-style build enforcement is off by default. A
huge fraction of "I want the build to complain about X" is an existing rule whose
severity just needs raising.

- Search the rule catalogs: the `CA####` quality rules and the `IDE####` style
  rules are documented under the .NET "Code analysis" reference. Match the request
  to a rule ID first.
- Raise it to `warning` or `error` in `.editorconfig`:

  ```ini
  dotnet_diagnostic.CA2007.severity = error
  dotnet_diagnostic.IDE0005.severity = warning
  ```

- Tune breadth with `AnalysisMode` / `AnalysisLevel` rather than per-rule lines
  when you want a whole band on.
- Code-style rules only run in the build when `EnforceCodeStyleInBuild=true`.
  Confirm that is set before assuming an `IDE####` rule will fail CI.

If the existing rule exactly meets the requested behavior, configure it and validate
the effect on the consumer. If it only overlaps, record the distinction and continue.

### Configuration-only mechanism

These are real analyzers, but you feed them a config file instead of writing C#:

- **`Microsoft.CodeAnalysis.BannedApiAnalyzers`** - ban specific types or members
  through `BannedSymbols.txt`.
- **EditorConfig naming rules** - `dotnet_naming_rule.*` enforces identifier
  naming conventions without custom code.
- **`Microsoft.CodeAnalysis.PublicApiAnalyzers`** - lock the public API surface
  with `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`.

Use one when it fully expresses the requirement. A custom analyzer remains valid when
the required semantics, configuration, ownership, or distribution differ.

### Analyzer packages already in the repository

Check packages already in the graph when they are relevant to the requested behavior.
Do not add an external dependency or block custom work solely because another project
implements a similar rule.

## How to tell what is already active

When the result depends on current analyzer configuration, check what is running:

- **Build with the analyzer report on** and read which analyzers ran:

  ```pwsh
  dotnet build <root>.csproj -c Release -p:ReportAnalyzer=true -bl
  ```

  Open the resulting `msbuild.binlog` in the MSBuild Structured Log Viewer and
  search the compiler invocation's analyzer list. See
  [performance.md](performance.md).
- **In the IDE**, type code that should trip the rule and watch for a squiggle or
  a lightbulb.
- **Inspect the active `.editorconfig`** up the directory chain; a rule may be
  present but disabled or lowered in severity.
- **Check the package graph** (`dotnet list package` / `Directory.Packages.props`)
  for analyzer packages already referenced.

## Continue with the requested outcome

Use the simplest mechanism that meets the stated requirements. Whether configuring an
existing diagnostic or implementing a custom one, continue with consumer validation in
[validation.md](validation.md#validate-consumer-adoption-as-a-migration).
