# Find-first: is an analyzer already doing this?

Detail for the [roslyn-analyzers](SKILL.md) skill, step 0. Writing a custom
analyzer is the **last** option, not the first. This page is the survey to walk
before authoring anything, plus how to tell what is already running.

## Why this gate exists

A custom analyzer is permanent cost: a `netstandard2.0` assembly you build, sign,
pack, version, test, and execute on every keystroke in every consumer's IDE. Most
"flag X" / "enforce Y" requests are already covered by a rule that ships in the
box or by a configuration-only analyzer that needs zero custom code. Spending five
minutes here routinely saves writing and maintaining a whole analyzer.

## The priority ladder

Work top to bottom. Stop at the first option that fits.

### 1. An existing rule, re-tuned in `.editorconfig`

The .NET SDK ships **`Microsoft.CodeAnalysis.NetAnalyzers`** (the `CA####` quality
rules) and the Roslyn **`IDE####`** code-style rules, both enabled by default on
modern SDKs. A huge fraction of "I want the build to complain about X" is an
existing rule whose severity just needs raising.

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

If an existing rule covers it, **that is the answer.** No new code.

### 2. A configuration-only analyzer (no custom code)

These are real analyzers, but you feed them a config file instead of writing C#:

- **`Microsoft.CodeAnalysis.BannedApiAnalyzers`** - ban specific types or members.
  This is the correct tool for "nobody should call `DateTime.Now`" / "don't use
  `string.Format`" style requests. You list the banned symbols in a
  `BannedSymbols.txt`; the analyzer (RS0030) reports every use. Writing a custom
  analyzer to ban an API is almost always reinventing this.
- **EditorConfig naming rules** - `dotnet_naming_rule.*` enforces identifier
  naming (interfaces start with `I`, constants PascalCase, private fields
  `_camelCase`). Do not write an analyzer for a naming convention.
- **`Microsoft.CodeAnalysis.PublicApiAnalyzers`** - lock the public API surface
  (RS0016/RS0017); additions must be recorded in `PublicAPI.Unshipped.txt`. The
  right tool for "fail the build if the public surface changes unexpectedly."

### 3. A third-party suite

If the request is a *family* of rules (style, correctness, framework-specific
smells), an established suite probably already has it and is battle-tested:

- **Roslynator** - large general-purpose analyzer + refactoring set.
- **StyleCop.Analyzers** - layout and documentation style.
- **Meziantou.Analyzer** - correctness/perf rules (culture-sensitive APIs,
  `Task` misuse, allocations).
- **SonarAnalyzer.CSharp** - bug/code-smell catalog.
- Domain suites: **`Microsoft.VisualStudio.Threading.Analyzers`** (async/threading),
  **`xunit.analyzers`** / **MSTest analyzers** (test-authoring), the
  `Microsoft.CodeAnalysis.Analyzers` **`RS####`** rules (analyzer authoring itself).

Adding a curated package and turning on the handful of rules you want is cheaper
and more reliable than maintaining equivalents by hand. Vet a third-party package
the same way the `manage-skills` security gate vets public skills: pin the version,
read what it enables by default, and turn rules on deliberately rather than
accepting the whole default set blind.

### 4. Author a custom analyzer

Only when the rule is genuinely specific to this codebase's conventions and none of
the above expresses it - e.g. "prefer `MyLib.EnumExtensions.AreFlagsSet` over
`Enum.HasFlag`", "seed `ValueStringBuilder` with a stack buffer", "call
`Value.Create()` not `new Value()`". These encode *local* knowledge no shipping
suite knows about. That is the legitimate case for `<root>.analyzers`. Proceed to
[design.md](design.md).

## How to tell what is already active

Before claiming "nothing flags this," check what is actually running:

- **Build with the analyzer report on** and read which analyzers ran:

  ```pwsh
  dotnet build <root>.csproj -c Release -p:ReportAnalyzer=true -bl
  ```

  Open the resulting `msbuild.binlog` in the MSBuild Structured Log Viewer and
  search the `csc` invocation's analyzer list. (Console output buries it; the
  binlog is the reliable read. See [performance.md](performance.md).)
- **In the IDE**, type code that should trip the rule and watch for a squiggle or
  a lightbulb - if an existing analyzer already reports it, you are done.
- **Inspect the active `.editorconfig`** up the directory chain; a rule may be
  present but set to `silent`/`none`, which only needs a severity bump (option 1).
- **Check the package graph** (`dotnet list package` / `Directory.Packages.props`)
  for analyzer packages already referenced - their rules may just be disabled.

## The decision, in one line

If an existing rule, a banned-API list, a naming rule, or a curated third-party
rule expresses the intent, **configure that and stop.** Author a custom analyzer
only for conventions unique to this codebase that nothing off the shelf can state.
