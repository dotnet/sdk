# Writing an analyzer and code fixer

Public `Microsoft.CodeAnalysis` APIs throughout, so the shapes transfer to any analyzer
repo. **[In this repo](#in-this-repo) replaces several of them** — in the skeleton below
alone, `new DiagnosticDescriptor(...)` by `DiagnosticDescriptorHelper.Create(...)`,
`defaultSeverity` + `isEnabledByDefault` by `RuleLevel`, and
`compilation.GetTypeByMetadataName(...)` by `WellKnownTypeProvider`. Read that section
before copying from here.

## Analyzer skeleton

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class ExampleAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "EXAMPLE0001",
        title: ...,
        messageFormat: ...,
        category: ...,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (context.Compilation.GetTypeByMetadataName("System.Span`1") is not { } spanType)
        {
            return;
        }

        context.RegisterOperationAction(ctx => Analyze(ctx, spanType), OperationKind.Invocation);
    }
}
```

Non-negotiable bits:

- **No state in analyzer fields.** One analyzer instance is reused across many
  compilations, so a field written during analysis is a correctness bug, not just a leak —
  and immutability is not sufficient. A field must hold nothing derived from a compiler
  API: an `ImmutableArray<INamedTypeSymbol>` is immutable and still roots the compilation
  it came from. Compiler data that is itself constant is fine, such as the
  `ImmutableArray<SyntaxKind>` of kinds you register for. Everything per-compilation is
  computed in the compilation-start action and reaches the nested callbacks by closure or
  by a per-compilation state object.
- **All symbol lookup happens once, in the compilation-start action**, and the analyzer
  returns *without registering* the inner action when a required type is absent. That is
  what keeps the analyzer free on compilations that can't possibly match. Never look a type
  up per-node, and never register the inner action before the bail-out checks.
- Prefer `IOperation` (`RegisterOperationAction`, `RegisterOperationBlockStartAction`) over
  syntax actions: it is language-agnostic, so one analyzer covers C# and VB. The fixer does
  not come along for free — it edits syntax, so expect per-language work there even when
  the analyzer is shared.
- **Match with a pattern, not a check-then-extract pair.**
  `if (operation is IInvocationOperation { TargetMethod: { Name: "Slice" } method })` keeps
  the matched shape and the data you need as one expression; a `Kind` test followed by a
  cast and a property read is two things that have to stay in sync. Where the shape isn't
  obvious, one comment giving the source it matches (`// span.Slice(0, n)`) beats a prose
  description of it.
- **`RegisterSymbolStartAction` when the rule needs per-symbol state and a decision at
  symbol end**, in preference to a compilation-end action. Symbol-end diagnostics surface
  live in the IDE; compilation-end ones never do — they appear only in a complete build.
  Reach for `RegisterCompilationEndAction` only when the decision genuinely has to
  aggregate across multiple symbol definitions, and put
  `WellKnownDiagnosticTags.CompilationEnd` on the descriptor when you do.

## Choosing a severity

Severity is a claim about the code, not about how much you care. `Error` says *this is not
valid — its meaning is undefined and it cannot be what you wanted*. `Warning` says *this is
legal, but you almost certainly did not intend it, and you need to think about it either
way*. Both put the burden of acting on the user, so both require that the rule is right
essentially every time. Unit tests prove the rule fires; they say nothing about how often
it is wrong, so measure that against a large real codebase before proposing anything above
`Hidden`.

| Default severity | Enabled | Use when |
|---|---|---|
| `Error` | yes | The code is broken, not merely suspicious. Reserved — effectively source-generator-only; needs owner sign-off. |
| `Warning` | yes | Legal code the user almost certainly did not mean, and will nearly always change. **No false positives** — it breaks builds under `TreatWarningsAsErrors`. |
| `Info` | yes | **The default for a new rule.** Still no false positives, but leaving it alone is a defensible choice. Worth surfacing in the IDE, not worth enforcing in CI. |
| `Hidden` | yes | The judgement is genuinely arguable, or the rule has some false positives. Effectively off, but still reachable through bulk configuration. |
| any | no | Opt-in only, by an explicit rule-ID severity entry. |

## Code fixers

- Export it: `[ExportCodeFixProvider(LanguageNames.CSharp), Shared]` with
  `using System.Composition;`. It is a real MEF v2 export attribute and non-shared is the MEF
  default, so `[Shared]` is load-bearing wherever the fixer is composed. It has no effect on
  the analyzer-package path — there the host finds the type by reflection and constructs one
  cached instance per reference — but write it anyway, as nearly every fixer here does.
- `equivalenceKey` must be a `nameof`, not a literal — it identifies the action for
  fix-all and for the test harness.
- **The fix title describes the action**, not the problem: *"Extract to a static readonly
  field"*, not a restatement of the analyzer title. It is its own localizable string.
- Build edits with `DocumentEditor` + `SyntaxGenerator` rather than raw `SyntaxFactory` —
  it keeps a fixer language-agnostic and gives you a single changed document at the end.
  It does **not** move trivia for you: when you replace a node, carry the original's trivia
  across explicitly (`WithTriviaFrom`), or the user loses their comments.
- **Parenthesize any expression you substitute into an arbitrary context.** Replacing
  `And(y, z)` with `y & z` inside `x * …` silently changes the meaning. Add
  `Simplifier.Annotation` to the parentheses you introduce: the code-fix pipeline runs the
  simplifier over annotated nodes and drops the ones that turn out to be redundant, so you
  can parenthesize unconditionally rather than reasoning about precedence at each site.
  Check [In this repo](#in-this-repo) before hand-rolling this — there is a helper that
  applies the annotation for you.
- **Preserve semantics where doing so is trivial.** Precedence (see the bullet above), operand
  and evaluation order, overflow, rounding — if the fix can keep the original meaning without
  meaningful extra work, it should. Arithmetic deserves the most care, because a rewrite there
  changes results silently instead of failing to compile. Where preserving it is not trivial
  the fix may still change semantics, but it must say so — suffix the action with
  `(may change semantics)`, and offer the semantics-preserving fix alongside it when both
  readings are reasonable. The failure mode is a title that reads as pure cleanup.
- **Make a reasonable effort to produce valid output.** If the fix can reach a correct form with
  the information it has, it should; most rewrites have one clear target and should generate
  valid code for the shapes they handle. That does not mean the fixed document must always
  compile — the original code may already be broken, or further user edits may still be
  required, and neither is a reason to decline. A fixer that never produces correct code in any
  shape is probably not worth offering; one that falls short in some edge cases is still
  valuable. The failure mode is withholding a useful fix because it cannot guarantee
  compilation.
- Where the fixer *is* language-agnostic, VB is cheap — export it for both languages and add
  mainline VB tests. If it needs language-specific syntax APIs, the VB fixer is optional.

### Report the diagnostic; decide separately whether you can fix it

Whether the code is worth reporting and whether you can rewrite it are different questions.
A shape you cannot fix is still worth a diagnostic — never narrow the analyzer to what the
fixer happens to handle. What the fixer must not do is register an action it cannot carry
out. Registering nothing is the correct outcome for an unfixable shape: the user sees the
diagnostic with no fix offered. Registering and then returning the document unchanged is
the bug — that is the lightbulb that does nothing.

So the eligibility check runs *before* `RegisterCodeFix`, and where it needs semantics the
analyzer and the fixer share one helper rather than each growing their own:

```csharp
// core: the semantics, shared by every language
protected virtual bool IsCandidate(IInvocationOperation invocation) => ...;

// C#: the syntax it needs, then defer
protected override bool IsCandidate(IInvocationOperation invocation)
{
    if (invocation.Syntax is not InvocationExpressionSyntax)
    {
        return false;
    }

    return base.IsCandidate(invocation);
}
```

Binding is not a shape guarantee — error recovery will happily bind an invocation with too
few or too many arguments, so `symbol is not null` does not mean
`invocation.Arguments.Length == 2`. Expose the check as a shared helper and call it from
both the analyzer and the fixer. `Debug.Assert` is not that validation: everything that
consumes a shipped analyzer runs release builds, so an assert catches nothing outside your
own tests.

This is not a licence to drop the fixer's own defensive checks. A fixer re-finds its nodes
in the *current* document, which may have changed since the diagnostic was computed, so
pattern-match what you find and `return` when it doesn't match. The distinction is that
those guards handle a stale span, not an eligibility question you should have answered
before registering.

### Flowing data from the analyzer to the fixer

Use `Diagnostic.Properties` (`ImmutableDictionary<string, string?>`), or additional
`Location`s. **Not `CustomTags`** — tags describe the *rule*, not an individual report.

```csharp
// analyzer
var properties = ImmutableDictionary<string, string?>.Empty.Add(ReplacementKey, replacement);
context.ReportDiagnostic(Diagnostic.Create(Rule, location, properties, messageArgs));

// fixer
if (!diagnostic.Properties.TryGetValue(ReplacementKey, out string? replacement))
{
    return;
}
```

Keep it small. The more the analyzer stores for the fixer, the more it holds alive; the
fixer can usually recompute from the span the diagnostic already carries.

### Fix-all

`WellKnownFixAllProviders.BatchFixer` is the cheap default, and most fixers use it, but it
is the *simplest* implementation rather than a good one. It runs every fix independently
against the original document and merges the resulting edits. That means:

- N independent forks, each running the code-action cleanup pass — slow.
- Real **incorrectness**, but not everywhere. Merging happens at the *text* level, through
  an interval tree of `TextChange`s, so the question is whether the edits conflict as spans
  — not whether the diagnostics share a parent node. What conflicts:
  - **Nested or overlapping rewrites.** For `Add(x, Add(y, z))` one fix wants
    `x + Add(y, z)` and the other `Add(x, y + z)`; the spans overlap.
  - **The same node rewritten into two different shapes** — one overload added per pass.
  - **Two insertions at the same position.** An empty span cannot overlap anything, so
    these are easy to miss, but the merger rejects them as ambiguous: it cannot know which
    order you meant. This is the usual reason a fixer that *adds* a member, overload, or
    argument fixes only one diagnostic per pass.

  And the loss is per-fix, not per-edit: one conflicting hunk discards **every** change
  that fix made to the document.

  What the merger sees is the *diff* between the original and fixed documents, not the edit
  you made. Those are not the same span: the code-action cleanup pass can reflow a region
  wider than the edit, so two rewrites that look disjoint still collide. Measured example —
  a fixer whose fix is a single `RemoveNode` merges 33 diagnostics cleanly when they are all
  field initializers, and fails at 18 once two property initializers are in the mix. The
  shape of the fix does not tell you the width of the diff, so treat the list above as
  *where to look first*, never as a substitute for the test below.

If your diagnostics can conflict this way, use `FixAllProvider.Create` to route the whole
document through one `SyntaxEditor`. Check [In this repo](#in-this-repo) before hand-rolling
it — there is a base class that packages this:

1. Fix all diagnostics in a document in a single callback.
2. Order them by `Location.SourceSpan.Start` **descending**, so inner nodes are handled
   before the outer nodes containing them. If two diagnostics can share a start — a chained
   call reported at two different lengths — add a shorter-span-first tie-break, since a sort
   on start alone leaves those in enumeration order.
3. Apply each through `editor.ReplaceNode(node, (currentNode, generator) => ...)` — the
   lambda overload, so outer rewrites observe the inner ones.

Two caveats come with `FixAllProvider.Create`: the fix must stay within the document the
diagnostic is in — anything cross-file needs a hand-written `FixAllProvider` — and it does
not filter by `FixAllContext.CodeActionEquivalenceKey` the way the batch fixer does, so a
fixer registering more than one action has to skip the diagnostics its action does not cover.

A shared base class that packages this is worth reaching for, but check how it registers
before you derive from one. If its `RegisterCodeFixesAsync` is sealed and offers the action
for every diagnostic in `context.Diagnostics`, it is only suitable when *every* diagnostic
the rule reports is fixable. A rule whose single ID also covers shapes the fixer cannot
handle needs conditional registration, and a base that forecloses it surfaces a lightbulb
that does nothing.

The `Microsoft.CodeAnalysis.Testing` harness exercises fix-all-in-document/project/solution
separately from the iterative case, so a batch-fixer correctness bug shows up as
`Expected '1' iterations but found '2' iterations` — that is a real bug, not a test
artifact. Likewise a `CodeActionValidationMode` failure means your fix produced a tree that
differs from what the compiler would parse from the same text; fix the fix, don't lower the
mode.

A multi-diagnostic test that *passes* proves nothing on its own, because "fix-all converged
in one pass" and "fix-all never ran" look identical from the outside. Before concluding a
fixer is fine, run the positive control: set `NumberOfFixAllIterations = 2` against the
unchanged fixer and confirm it fails with `Expected '2' iterations but found '1'`.

A test that *fails* needs reading for the same reason. Confirm the message is an iteration
count and not a content mismatch: a verbatim string literal written with `\n` into a repo
whose `.cs` files are CRLF produces a diff that reads exactly like a fix-all bug.

If you have a correct `FixAllProvider` and *still* see that iteration failure, suspect the
rewrite itself. Removing several nodes from one `SeparatedSyntaxList` by chaining
`.Remove(node)` silently no-ops after the first call: `Remove` returns a new list whose
surviving nodes are re-created, so a later `Remove` passed an original node reference no
longer finds it. Collect the indices and `RemoveAt` in descending order instead. The same
hazard applies to any rewrite that holds node references across an edit — re-find or
re-index rather than reusing them.

## Tests

The `Microsoft.CodeAnalysis.Testing` harness is the standard way to test both. Give each
test file a per-language verifier alias rather than naming the harness types at every call
site — that is the general recommendation, not a local convention:

```csharp
using VerifyCS = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    ExampleAnalyzer,
    ExampleFixer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
```

It is test-framework agnostic; the snippet below omits the parameterized-test attribute
your framework supplies.

```csharp
public async Task Match_ReportsDiagnostic(string typeName)
{
    // lang=C#-test
    string code = $$"""
        public class C
        {
            public void M() => [|Target<{{typeName}}>()|];
        }
        """;

    await VerifyCS.VerifyAnalyzerAsync(code);
}
```

- **Use raw string literals for embedded sources.** Not for the escaping — for the
  indentation. A `@"..."` source has to start at column 0, so it collides with the test
  method's own indentation and becomes hard to scan. The `// lang=C#-test` comment gives
  the IDE syntax colorization inside the literal.
- Markup: `[|...|]` when the rule has a single ID; `{|EXAMPLE0001:...|}` when the file
  exercises more than one ID or you need to be explicit. With a single rule, keep the ID
  out of test *names* too. Use `Diagnostic(Rule).WithLocation(...).WithArguments(...)` only
  when asserting message arguments.
- **Collapse mechanical permutations into data rows.** When the scaffolding is identical
  from test to test and only a type or method name varies, N near-identical test methods
  are noise; the rows *are* the signal, lining the cases up where you can see at a glance
  which are covered and which are missing. The limit: no logic in the test body driven by
  the data — only when input and expected output are mechanically transformable.
- **Split by both behavior and language.** One scenario per test method, and a separate
  method per language, so a failure says immediately whether the bug is language-specific.
  Split a large rule across partial-class files by scenario rather than one enormous file.
- **Use realistic scenarios** — call APIs that would actually accept the input you're
  passing. A contrived call that couldn't compile in real code is a weak test.
- **`LanguageVersion` and `ReferenceAssemblies` have defaults you will outgrow.** The
  harness pins an older C# version than you probably expect, so a test source using newer
  syntax must set it explicitly. The reference set likewise only carries the framework —
  add the packages your scenario needs.
- Cover both languages. C# gets full coverage; VB needs at least mainline positive and
  negative cases, and full coverage if any syntax-specific code exists.
- **When a fixer exists, write every test as a code-fix test.** A fix test with identical
  input and expected output asserts "diagnostic but no fix offered" or "no diagnostic"; a
  differing pair asserts both the diagnostic and the fix output. Don't split analyzer-only
  and fixer-only test classes.
- **Enumerate the contexts where the rewrite is *invalid*, and pin each with a no-fix
  test.** The harness compiles the fixed output, but that only proves the shapes you thought
  to write down compile — its strictness is not coverage. Work out what the new form cannot
  do that the old one could: a conversion the old type had, a ref struct in an expression
  tree or held across an `await`, an overload that only binds the original type. If every
  fix test happens to use `var` or discard the result, that is the eligibility gap showing
  rather than a coincidence.
- **Add trivia tests** — a source with comments and blank lines around the fixed node.
- Negative tests are not optional — the false-positive cases you thought about during
  design are the ones a reviewer will ask for.
- **Shapes that earn a test of their own for any invocation-shaped rule**, positive or
  negative depending on what your rule does with them:
  - **Nested occurrences** (`Add(x, Add(y, z))`) — the case that catches a broken fix-all.
  - **Named and reordered arguments** (`Divide(right: y, left: x)`). `IOperation` exposes
    arguments in *evaluation* order — syntactic order in C#, parameter order in VB — so a
    fixer that indexes `Arguments[0]` positionally reads the wrong argument in C# while the
    same code stays correct in VB.
  - **The match nested inside another expression** (`Console.WriteLine(X.Add(a, b))`) — so
    you find the invocation node, not the enclosing argument node, and so you notice a
    missing parenthesization.

## Performance

Analyzers run on every keystroke in the IDE and on every build, so small savings —
allocations especially — add up. Beyond the usual (no LINQ or allocating closures on
per-node paths, cheapest predicate first, don't re-query the semantic model for something
the `IOperation` already carries):

- **Scope every cache to the compilation.** The closure of the
  `RegisterCompilationStartAction` lambda is the right holder: when the IDE drops a
  compilation it drops the registered actions with it, and the caches go too. A cache in a
  `static` or in an analyzer field outlives the compilation and keeps it alive.
- **Cache negative results too.** If you look up whether a symbol carries an attribute,
  cache the "no" as well, or every subsequent hit repeats the lookup.
- **When the rule is trying to match an invocation to a set of library methods, build the
  lookup once.** If the rule is trying to find all invocations of a specific set of library
  methods (or generally all references to a library member), where each member will have a
  slightly different set of applied rules, build the mapping of member->kind up front. The
  per-node action is then one dictionary probe rather than N `Contains` calls on every
  invocation in the compilation.
- Don't compare symbols by `ToDisplayString()` or `Name`. It allocates, and it's wrong for
  identity — use `SymbolEqualityComparer.Default`. Compare `OriginalDefinition` only when
  you mean to ignore construction; it equates `List<int>.Add` with `List<string>.Add`.

## In this repo

`src/Microsoft.CodeAnalysis.NetAnalyzers` wraps several of the calls above; paths below are
relative to it. Use the wrapper, not the raw API:

| General API above | Use here instead |
|---|---|
| `new DiagnosticDescriptor(...)` | `DiagnosticDescriptorHelper.Create(...)` — derives the `learn.microsoft.com` help link from the lowercased ID and applies the telemetry/FxCop custom tags. |
| `defaultSeverity` + `isEnabledByDefault` | `RuleLevel` (`src/Utilities/Compiler/RuleLevel.cs`). Its XML doc is the rubric reviewers apply; `IdeSuggestion` is the default for a new rule. |
| `compilation.GetTypeByMetadataName(...)` | `WellKnownTypeProvider.GetOrCreate(compilation).GetOrCreateTypeByMetadataName(...)`, with the metadata name added to `src/Utilities/Compiler/WellKnownTypeNames.cs`. |
| `arguments[i]` to reach parameter `i` | `arguments.GetArgumentForParameterAtIndex(i)` (`src/Utilities/Compiler/Extensions/IOperationExtensions.cs`) — matches on `Parameter.Ordinal`, so it survives named and reordered arguments. Use the `Try` overload where the parameter may not be matched. |
| hand-rolled `FixAllProvider.Create` | Derive from [`SyntaxEditorBasedCodeFixProvider`](../../../../src/Microsoft.CodeAnalysis.NetAnalyzers/src/Microsoft.CodeAnalysis.NetAnalyzers/SyntaxEditorBasedCodeFixProvider.cs) — you supply `FixableDiagnosticIds`, an `ApplyFixAsync` holding the whole fix, and a `RegisterCodeFixesAsync` calling `RegisterCodeFix(context, title, equivalenceKey)`. A single invocation and a fix-all pass both run `ApplyFixAsync` into one `SyntaxEditor`, ordered for you. Registration stays yours, so a rule that also reports shapes the fixer cannot handle can still register conditionally. A fixer that cannot take the base class calls [`SyntaxEditorFixAllProvider.Create`](../../../../src/Microsoft.CodeAnalysis.NetAnalyzers/src/Microsoft.CodeAnalysis.NetAnalyzers/SyntaxEditorFixAllProvider.cs) itself; its `TState` overload is where you honor `CodeActionEquivalenceKey`. |
| manual parenthesizing | `Analyzer.Utilities.Extensions.SyntaxGeneratorExtensions.Parenthesize` — applies `Simplifier.Annotation` for you. C#-only (`src/Utilities/Compiler.CSharp/`); a VB fixer parenthesizes by hand. |
| `HashSet<T>` / `Dictionary<K,V>` on hot paths | `src/Utilities/Compiler/PooledObjects/`. Must be freed on every path — prefer `using var x = PooledHashSet<T>.GetInstance();`. |
| reading `AnalyzerConfigOptions` directly | `src/Utilities/Compiler/Options/` (`AnalyzerOptionsExtensions`, `EditorConfigOptionNames`), e.g. `context.Options.MatchesConfiguredVisibility(Rule, symbol, compilation)`. Reuse an existing option name before adding one, and document new ones in `docs/analyzer-configuration.md`. |

`DiagnosticDescriptorHelper.Create` also requires `isPortedFxCopRule` and `isDataflowRule`;
a new rule passes `false` for both. `isDataflowRule: true` is for rules built on the
flow-analysis framework in `src/Utilities/FlowAnalysis/` — they ship `Disabled` because
flow analysis costs far more than an `IOperation` walk, and writing one is a separate
undertaking covered by
[`docs/writing-dataflow-analysis-based-analyzers.md`](../../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/writing-dataflow-analysis-based-analyzers.md).
The helper also takes `isReportedAtCompilationEnd`, which applies the compilation-end tag
for you.

**Tests are MSTest**, not xUnit (`[TestMethod]`, `[DataRow]`, `[DynamicData]`), and the
verifier alias points at the `Test.Utilities` wrapper, which bakes in `DefaultVerifier` so
the alias takes two type arguments rather than three:

```csharp
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.<Category>.<Name>Analyzer,
    Microsoft.NetCore.CSharp.Analyzers.<Category>.CSharp<Name>Fixer>;
```

which is what supplies `VerifyCS.VerifyAnalyzerAsync` / `VerifyCodeFixAsync`.
`ReferenceAssemblies` defaults to `AdditionalMetadataReferences.Default`; pick the member
carrying the packages your scenario needs. `LanguageVersion` defaults to `CSharp7_3` in
`CSharpCodeFixVerifier.Test` — raw string literals in the *test file* are fine, that's the
test project's own language version.

**Validating against a real codebase**: the analyzers ship inside the SDK layout now, so
point the target repo at a locally built SDK or overwrite
`<dotnet-root>/sdk/<version>/Sdks/Microsoft.NET.Sdk/analyzers/` — not the NuGet cache.

## Further reading

In-repo docs under `src/Microsoft.CodeAnalysis.NetAnalyzers/docs/`, each worth reading only
when it applies:
[`netcore-getting-started.md`](../../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/netcore-getting-started.md)
(definition of done, validating against a real codebase, debugging in VS),
[`guidelines-for-new-rules.md`](../../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/guidelines-for-new-rules.md)
(proposal and documentation requirements),
[`analyzer-configuration.md`](../../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/analyzer-configuration.md)
(long; the `.editorconfig` option catalog — grep it for a specific option rather than
reading it through), and
[`writing-dataflow-analysis-based-analyzers.md`](../../../../src/Microsoft.CodeAnalysis.NetAnalyzers/docs/writing-dataflow-analysis-based-analyzers.md)
(only if your rule needs the dataflow framework).
