# Designing the analyzer

Detail for the [roslyn-analyzers](SKILL.md) skill. Authoring rules for a correct,
maintainable `DiagnosticAnalyzer`. The running example is a `UseIsNull` analyzer
(flag `== null` / `!= null`, prefer `is null`); this page explains the choices
behind it.

## Project setup recap

The analyzer assembly must be `netstandard2.0` so it loads in every compiler host
(command-line `csc`, the .NET SDK, Visual Studio's .NET Framework host, VS Code).
RS1038 enforces this. Keep `EnforceExtendedAnalyzerRules=true` on - it turns on the
`RS####` authoring analyzers that catch most of the mistakes below at build time.
Both `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` must exist
as `AdditionalFiles` (RS2008), and every new rule ID must be listed in the
unshipped file (RS2000).

## Rule 1: an analyzer instance is stateless and thread-safe

The host creates one analyzer instance and reuses it across compilations, threads,
and (in the IDE) edits. Never store per-analysis state in an instance or static
field. All state lives in locals inside the registered callbacks, or in
per-compilation state captured by `RegisterCompilationStartAction` (see
[performance.md](performance.md)). Storing a `Compilation` or `ISymbol` in a field
is doubly wrong: besides the race, it **roots** that compilation's object graph
across every later edit. Resolve well-known symbols at compilation start and capture
them in the closure - see the rooting rule in
[performance.md](performance.md#the-rooting-rule-capture-in-the-closure-never-in-a-field).

In `Initialize`, always:

```csharp
public override void Initialize(AnalysisContext context)
{
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();
    // register narrow actions here
}
```

- `EnableConcurrentExecution()` lets the host run your callbacks in parallel. It is
  also an assertion that they are thread-safe - which they are, if you followed the
  no-shared-state rule.
- `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` stops the
  analyzer from firing on generated code (designer files, source-generator output).
  Skipping this is a common false-positive source.

## Rule 2: register the narrowest action, with a kind filter

The registration *is* the performance contract - the host only calls you for the
kinds you ask for. Prefer, in order:

1. **`RegisterSymbolAction(..., SymbolKind.X)`** - for declaration-level rules
   (types, methods, properties). The host walks symbols for you.
2. **`RegisterOperationAction(..., OperationKind.X)`** - for semantic, code-body
   rules. `IOperation` is the bound, language-agnostic tree; prefer it for anything
   that depends on what the code *means* (see rule 3).
3. **`RegisterSyntaxNodeAction(..., SyntaxKind.X)`** - for purely syntactic rules
   where you only need the shape of the source, like `UseIsNullAnalyzer` keying on
   `EqualsExpression` / `NotEqualsExpression`.

Avoid `RegisterSyntaxTreeAction` and `RegisterSemanticModelAction` unless you truly
need to scan a whole file - they hand you the entire tree and make *you* do the
walking, which is where slow analyzers come from. Never call
`SyntaxNode.DescendantNodes()` to hunt for nodes a registration filter would have
delivered directly.

Do not recursively visit syntax, operations, or embedded-language trees whose depth
comes from source. A `StackOverflowException` can terminate the host rather than
surface as a recoverable `AD0001`. Use an explicit work stack for arbitrary depth,
or a tested stack budget around every recursive phase. A guard around parsing does
not protect later diagnostic or capture walks.

For rules that walk declarations, [symbol-actions.md](symbol-actions.md) covers
parameters/type parameters, locals, synthetic names, overrides, and explicit
interface implementations.

When analysis state belongs to one symbol, prefer a symbol-start action with
symbol-end reporting over compilation-start plus compilation-end reporting.
Compilation-end diagnostics are not produced during normal live IDE analysis, so
a rule that reports only there can appear to work in builds while remaining silent
as the user edits. Use compilation-end only for facts that genuinely require the
whole compilation and validate its intended host behavior explicitly.

## Rule 3: prefer `IOperation` over raw syntax when semantics matter

Raw syntax is a literal transcription of the source - it cannot tell you what a
name binds to, whether an implicit conversion happened, or whether `+` is string
concatenation or numeric addition. If your rule depends on meaning, register an
operation action and inspect the `IOperation`; it is bound, so you get the resolved
symbol, the converted type, and constant values without re-deriving them, and the
same analyzer then works for both C# and VB.

Use raw syntax only when the rule genuinely is about source shape (a specific
operator token, a `using` directive's position, brace style). `UseIsNullAnalyzer`
is legitimately syntactic: "is this the `== null` *spelling*" is a question about
the source text, not the binding, so it keys on `SyntaxKind` and never touches the
semantic model.

When a syntactic match needs *one* semantic confirmation, do the cheap syntax check
first and only then reach for `context.SemanticModel` - never the other way round
(see [performance.md](performance.md)).

Treat nullable and optional semantic results as ordinary IDE states. Error recovery,
incomplete edits, and speculative models can produce operations with a null `Type`,
missing symbols, or several declared locals where the common form has one. Prove a
`GetRequired*` helper's precondition for the exact node in hand; otherwise check and
return. Likewise, index an analysis map only when its producer proves total coverage;
unsupported or newly added kinds should normally use `TryGetValue` and bail out.

Choose a symbol identity policy for maps and sets. If constructed generic symbols
and their definitions represent the same logical declaration, normalize both
insertion and lookup to `OriginalDefinition`. Comparing one normalized side to one
constructed side leaves a locally consistent cache that still misses valid inputs.

## Rule 4: a stable, well-formed `DiagnosticDescriptor`

```csharp
private static readonly DiagnosticDescriptor s_rule = new(
    id: "ABCD0001",
    title: "Use pattern matching for null checks",
    messageFormat: "Use '{0}' instead of '{1}'",
    category: "Usage",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "Comparisons against the null literal should use 'is null' / 'is not null'.",
    helpLinkUri: "https://github.com/your-org/your-repo");
```

- **ID** is a permanent contract - users pin severities to it in `.editorconfig`.
  Pick the `<PREFIX>####` prefix and never reuse or renumber an ID.
- Cache the descriptor in a `static readonly` field and return a cached
  `ImmutableArray` from `SupportedDiagnostics` (`ImmutableArray.Create(s_rule)`).
  Do not allocate a new descriptor or array per call.
- If one descriptor must be canonical, carry explicit order. Dictionary iteration
  order is not a selection contract, and descriptor equality may omit operationally
  relevant properties such as `CustomTags`.
- `messageFormat` is a format string; pass arguments at `Diagnostic.Create`. Do not
  pre-format with interpolation - it defeats localization and allocates.
- Set a real `helpLinkUri`.
- Verify the link for every new diagnostic ID. If its fragment is derived from the
  ID, the matching documentation anchor must exist before the rule ships.
- For an analyzer you intend to *ship and localize*, move `title`/`messageFormat`/
  `description` into a `.resx` and use `LocalizableResourceString`. For an
  internal, English-only repo rule the inline strings above are acceptable.

## Rule 5: report at the tightest location

Report the diagnostic on the smallest span that identifies the problem - the
offending operator, identifier, or argument - not the whole statement. The squiggle
should land exactly on what the user must change. Pass the precise
`Location`/`SyntaxNode.GetLocation()` to `Diagnostic.Create`.

## Rule 6: honor configuration; do not hardcode severity behavior

Report the diagnostic unconditionally and let the host's configuration decide
whether it is an error, warning, suggestion, or suppressed. Do not read severity
configuration yourself. The descriptor's `defaultSeverity` and
`isEnabledByDefault` establish the initial state; host configuration determines
the effective severity.

A rule that enforces house style should usually set `isEnabledByDefault: false`.
Consumers enable it with `dotnet_diagnostic.<id>.severity`, which also controls
the effective severity of every report under that ID. Do not rely on per-report
severity to preserve independent sub-rule levels once the consumer sets the
ID-wide severity; use analyzer-specific options to turn sub-rules off instead.

For analyzer-specific options, make zero/default safe or validate before use.
Missing and malformed EditorConfig values are normal inputs, and future enum values
must not turn dictionary indexing or supposedly unreachable branches into `AD0001`.

## Rule 7: release tracking is part of the change

Every new, removed, or changed rule updates the analyzer release files in the same
commit. Follow [release-tracking.md](release-tracking.md); `RS20xx` diagnostics
enforce the file format and agreement with `SupportedDiagnostics`.

## Code-fix providers (optional)

A code fix is a separate type from the analyzer:

- `[ExportCodeFixProvider(LanguageNames.CSharp)]` plus `[Shared]` on a
  `CodeFixProvider`.
- `FixableDiagnosticIds` returns the analyzer's ID(s). Hardcode the id strings (a
  stable public contract) rather than referencing the analyzer assembly - the
  code fix lives in a different assembly (see below).
- Implement `RegisterCodeFixesAsync`; compute the edit as an immutable
  `Document`/`Solution`/`SyntaxNode` transformation and register a `CodeAction`.
  `SyntaxGenerator` (from `Microsoft.CodeAnalysis.Editing`) is the language-neutral
  way to edit modifiers/declarations - e.g. `generator.WithModifiers(decl,
  generator.GetModifiers(decl).WithIsReadOnly(true))`.
- Override `GetFixAllProvider()` so "fix all occurrences" works. Use
  `WellKnownFixAllProviders.BatchFixer` only when independently computed text
  changes cannot conflict; otherwise use one coherent document edit. Read
  [fix-all.md](fix-all.md) before choosing the provider.
- Use a stable, descriptive `equivalenceKey` on the `CodeAction` so FixAll can group
  identical fixes.

### Diagnostic eligibility and fix eligibility are separate

The analyzer reports every source shape that violates the rule, including shapes
the fixer cannot safely rewrite. Do not narrow analyzer registration or reporting
to make every diagnostic fixable.

`RegisterCodeFixesAsync` independently decides whether to offer an action. Validate
that the target still has the expected syntax and semantics, and that it is
editable. For example, check `member.DeclaringSyntaxReferences` is non-empty so a
fix does not appear for metadata. If the fix is ineligible, register no action;
do not register an action whose transformation later returns the unchanged
document.

Carry analyzer-derived facts needed by the fixer in `Diagnostic.Properties` or
additional `Location`s. `DiagnosticDescriptor.CustomTags` describe the rule and
are shared by every report; they are not per-diagnostic data. Use stable property
keys and values that survive diagnostic serialization; pass source spans as
additional locations rather than encoding positions into strings. The fixer must
validate transported values against the current document because diagnostics can
outlive the snapshot that produced them.

### Disclose semantic-changing fixes

Prefer a semantics-preserving transformation when it is straightforward. A useful
fix may intentionally change behavior, but its action title must include
`(may change semantics)` and its before/after tests must demonstrate the changed
interpretation. When both interpretations are useful, offer separate actions with
distinct titles and equivalence keys rather than silently choosing one.

The fix owns the validity of the target shape it creates. Make a reasonable effort
to produce code that binds and compiles for the supported input, including required
parentheses, conversions, imports, and trivia. It need not repair unrelated errors
already present elsewhere in the document. Test intentional error-recovery inputs
with the expected compiler diagnostics so this boundary is explicit.

### Code fixes go in a SEPARATE assembly (RS1022)

A `CodeFixProvider` references the Roslyn **Workspaces** layer
(`Document`, `CodeAction`, `SyntaxGenerator`). **RS1022 forbids any reference to
Workspaces types from an assembly that also contains a `DiagnosticAnalyzer`** -
analyzers must stay Workspaces-free so they load in the command-line compiler. So
a repo that ships code fixes needs a second project:

- `<root>.analyzers` - the `DiagnosticAnalyzer`s. References
  `Microsoft.CodeAnalysis.CSharp`, `EnforceExtendedAnalyzerRules=true`.
- `<root>.analyzers.codefixes` -
  the `CodeFixProvider`s. `netstandard2.0`, signed, `IsPackable=false`,
  `IncludeBuildOutput=false`. References
  `Microsoft.CodeAnalysis.CSharp.Workspaces`. Do **not** set
  `EnforceExtendedAnalyzerRules` and do **not** add
  `Microsoft.CodeAnalysis.Analyzers` here - those are for the analyzer assembly.

Both assemblies pack into `analyzers/dotnet/cs/` from the **same**
`_AddAnalyzersToPackage` target (a second `MSBuild Targets="GetTargetPath"` call
feeding the same `_PackageFiles` group). The command-line compiler loads the
code-fix dll but never instantiates the provider (no analyzer in it), so the
absence of Workspaces at build time is fine; the IDE supplies Workspaces when it
offers the fix. This is the standard StyleCop/Roslynator split.

**Packaging gotcha:** `MSBuild Targets="GetTargetPath"` returns the code-fix
assembly path but does **not** build it, and nothing else in the library's graph builds
it either (it is not an analyzer *of* the library). Add a build-ordering
`ProjectReference` from `<root>.csproj` to the code-fix project with
`ReferenceOutputAssembly="false" PrivateAssets="all"` and **no**
`OutputItemType="Analyzer"` (adding that would load Workspaces into the
command-line compiler's analyzer context). Without the reference the pack fails
with "Could not find a part of the path ...\<root>.analyzers.codefixes\...".

## Checklist

- [ ] `netstandard2.0`, `EnforceExtendedAnalyzerRules=true`, release files present.
- [ ] No instance/static mutable state.
- [ ] `EnableConcurrentExecution()` +
  `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`.
- [ ] Narrowest registration with a kind filter; no manual tree walks.
- [ ] No recursion whose depth is controlled by source or embedded input.
- [ ] `IOperation` where semantics matter; raw syntax only for source-shape rules.
- [ ] Nullable semantic results, option defaults, and partial maps are handled.
- [ ] Constructed/definition symbol identity is normalized consistently.
- [ ] Cached descriptor + cached `SupportedDiagnostics`; `messageFormat` args, not interpolation.
- [ ] Diagnostic reported at the tightest location.
- [ ] Diagnostic and fix eligibility are evaluated independently.
- [ ] Per-diagnostic fixer data uses stable properties or additional locations and
  is revalidated against the current document.
- [ ] Semantic-changing actions disclose the change in their title.
- [ ] The fix produces a valid target shape without claiming to repair unrelated
  compiler errors.
- [ ] FixAll provider is justified by the edit shape and tested for conflicts.
- [ ] New rule ID recorded in `AnalyzerReleases.Unshipped.md`.
