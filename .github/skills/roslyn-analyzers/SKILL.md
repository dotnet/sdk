---
name: roslyn-analyzers
description: Design, build, validate, adopt, and ship a Roslyn analyzer (and optional code fix). Use when asked to "write an analyzer", "create a Roslyn/diagnostic analyzer", "add an analyzer rule", "add a code fix", "enable an analyzer suite", "migrate to an analyzer package", "enforce a convention at build time", or "flag a pattern in code". ALWAYS checks whether an existing analyzer suite (the .NET SDK CA/IDE rules, BannedApiAnalyzers, an EditorConfig rule, Roslynator, StyleCop, Meziantou, etc.) already covers new-rule requests before authoring anything. Covers analyzer project layout and packaging, consumer-adoption inventory, statelessness/concurrency and IOperation-vs-syntax design, Microsoft.CodeAnalysis.Testing validation, and analyzer and Fix All performance. For BenchmarkDotNet runtime microbenchmarks see `performance-testing`; for auditing untrusted-input handling see `security-review`.
license: MIT
compatibility: Requires the .NET SDK, Roslyn packages, and a test project capable of running Microsoft.CodeAnalysis.Testing fixtures.
metadata:
  portability: portable
  applicability: dotnet-project-gated
  binding: optional-overlay
  risk: local-write
  maturity: canary
  requires: none
  related: performance-testing, security-review, pre-pr-self-review, il-copy-inspection
---

# Roslyn analyzers

If `overlay.md` exists beside this file, read it before acting; it contains
repository-specific bindings. This core remains usable without it.

Author or adopt a Roslyn diagnostic analyzer suite the right way: confirm nothing
already does the job before adding a rule, build custom analyzers to the authoring
rules, validate diagnostics and fixes against real consumers, and keep IDE and Fix
All costs within measured bounds.

If your repo already has a working analyzer, read it first and copy its shape - a
real in-repo example is the best template. Otherwise the patterns below are
distilled from working analyzers and the official Roslyn SDK guidance.

## Step 0 - the prime directive: is it already covered?

**Do not write a new analyzer until you have ruled out the existing ones.** A
hand-rolled analyzer is code you own, test, and pay for on every build and
keystroke forever. Most "I want to flag X" requests are already solved by a rule
that ships in the box and only needs a severity bump in `.editorconfig`, or by a
configuration-only analyzer (banned APIs, naming rules). The full survey and the
decision checklist are in [existing-analyzers.md](existing-analyzers.md).

The short version, in priority order:

1. **An existing rule + `.editorconfig` severity** (the .NET SDK `CA*`/`IDE*`
   analyzers are on by default). Cheapest possible answer.
2. **A configuration-only analyzer** - `BannedApiAnalyzers` (ban a type/member),
   an EditorConfig naming rule, `PublicApiAnalyzers` (lock public surface). No
   custom code.
3. **A third-party suite already in the graph or worth adding** - Roslynator,
   StyleCop, Meziantou, SonarAnalyzer.
4. **Only if none fit** - author a custom analyzer. Continue below.

If the answer is "an existing rule is right except in one context", you may be
looking at a `DiagnosticSuppressor` rather than an analyzer - but read Rule 0 of
[suppressors.md](suppressors.md) first, because most suppressors should not exist.

## Where analyzers live (the project layout)

By convention the analyzer ships as a small cluster of projects alongside the
library it guards (`<root>` is the library project name):

- **`<root>.analyzers`** - the analyzer assembly. `netstandard2.0`,
  `EnforceExtendedAnalyzerRules=true`, `IsPackable=false`,
  `IncludeBuildOutput=false`, signed. Needs both `AnalyzerReleases.Shipped.md` and
  `AnalyzerReleases.Unshipped.md` or RS2008 fails the build.
- **`<root>.analyzers.tests`** - a modern .NET test project (MSTest / xUnit).
  If your repo enforces XML-doc comments or treats warnings as errors, it may
  need a local `.editorconfig` relaxing rules like `CS1591` on the test snippets.
- **`<root>.analyzers.codefixes`** - the `CodeFixProvider`s. A **separate** assembly
  because a code fix references the Roslyn Workspaces layer, which RS1022 forbids in
  the analyzer assembly. Only needed when you ship fixes; see the code-fix section
  in [design.md](design.md).
- **Ship the analyzer inside your library's NuGet package**, not as its own
  package. A pack target adds the analyzer and code-fix assemblies to
  `analyzers/dotnet/cs/` in the library's `.nupkg`, and an
  `OutputItemType="Analyzer"` project reference from the library project dogfoods
  the analyzers against its own sources.

## Workflow

1. **Run step 0.** Rule out existing analyzers ([existing-analyzers.md](existing-analyzers.md)).
   If one fits, configure it and stop - no new code.
2. **Pick the diagnostic ID and descriptor.** A stable `<PREFIX>####` ID (a short
   uppercase prefix unique to your project, e.g. `ABCD0001`), category, default
   severity, `helpLinkUri`. Add the row to `AnalyzerReleases.Unshipped.md` in the
   same change or RS2000 fails.
3. **Design the analyzer** to the statelessness, registration, and
   `IOperation`-vs-syntax rules in [design.md](design.md). Copy the shape of a
   known-good analyzer. For declaration-wide rules, also read
   [symbol-actions.md](symbol-actions.md).
4. **Validate.** Follow [validation.md](validation.md), including malformed state,
   adversarial depth, and real-code triage before enabling by default. Run Debug
   and Release.
5. **Check analyzer performance.** Follow [performance.md](performance.md): keep
   the IDE path cheap and cache bounds explicit.
6. **Check Fix All performance.** Measure elapsed time and peak memory on
   representative bulk input in the real host.
7. **Validate consumer adoption.** Before broad enablement, inventory diagnostic
   and fix coverage, then test the packaged analyzer against the consumer's full
   project and target-configuration graph.
8. **Decide whether to dogfood it** on your own source. If yes, the analyzer must
   be clean against the existing tree or scoped off where it shouldn't apply - e.g.
   a directory-level `.editorconfig` that disables the rule for generated or ported
   code that should be exempt.
9. **Self-review and ship.** Run the `pre-pr-self-review` skill; new public diagnostic
   surface needs tests, and a perf claim needs a measurement.

## Deep dives

- [existing-analyzers.md](existing-analyzers.md) - the find-first survey: SDK
  `CA`/`IDE` rules, `BannedApiAnalyzers`, EditorConfig naming, `PublicApiAnalyzers`,
  Roslynator/StyleCop/Meziantou/Sonar, and how to tell what is already active.
- [design.md](design.md) - authoring rules: stateless and thread-safe, narrowest
  registration, `IOperation` over raw syntax, descriptors, release tracking, and a
  note on code-fix providers.
- [fix-all.md](fix-all.md) - choosing among `FixAllProvider.Create`,
  `DocumentBasedFixAllProvider`, `BatchFixer`, and a custom provider; conflict and
  resource analysis; equivalence-key filtering; edit ordering; and bulk tests.
- [symbol-actions.md](symbol-actions.md) - complete symbol coverage, names that
  cannot be fixed at the report site, and analyzer-crash diagnosis.
- [release-tracking.md](release-tracking.md) - shipped/unshipped file format,
  immutable release history, release promotion, and the `RS20xx` diagnostics.
- [validation.md](validation.md) - testing: the `Microsoft.CodeAnalysis.Testing`
  markup harness, a lightweight in-memory harness, malformed/deep-input coverage,
  explicit no-fix contexts, Fix All scale and host validation, real-code
  false-positive triage, consumer-adoption validation, and the dogfood probe.
- [performance.md](performance.md) - analyzer IDE budgets, Fix All resource
  discipline, cache lifetime and cardinality, traversal/allocation hygiene,
  `ReportAnalyzer`, and real-host code-fix measurement.
- [suppressors.md](suppressors.md) - `DiagnosticSuppressor`: when owning the domain
  beats suppressing, what is suppressible at all, the id convention, why suppressions
  cannot be release-tracked, and how to prove one is load-bearing.

## Cross-skill

- Validate library runtime perf (not analyzer perf) with `performance-testing`.
- Run `pre-pr-self-review` before opening a PR; `create-pr` to publish.
- If the analyzer parses or reinterprets untrusted input, run `security-review`.
- For a rule about struct copies (defensive-copy / `[NonCopyable]` analyzers),
  `il-copy-inspection` is the post-build, ground-truth counterpart: it reads emitted
  IL to confirm a prediction and to find the compiler-synthesized copies an
  `IOperation`-based analyzer cannot see.
- A consuming repository wires the concrete analyzer-project names, the example
  analyzer to copy from, the diagnostic-ID prefix, and these cross-references in
  its overlay.

## Disambiguation

"Performance" means two different things here. Tuning how fast the **analyzer**
runs inside the IDE is this skill ([performance.md](performance.md)). Measuring how
fast the **library code** runs at execution time with BenchmarkDotNet is
`performance-testing`. They do not share a harness or a budget.
