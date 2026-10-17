# Diagnostic suppressors

Detail for the [roslyn-analyzers](SKILL.md) skill. A `DiagnosticSuppressor` removes a
diagnostic another analyzer produced. It is the tool for "that rule is right in general
but wrong in this context", not for "we want a different rule".

Read Rule 0 before writing any of the rest. Most suppressors should not exist.

## Rule 0: prefer owning the domain over suppressing

Before writing a suppressor, ask **who should own this category of diagnostic**. A
suppressor is only justified when the other analyzer's rule is right in general and you
are carving out a genuine exception. If you find yourself suppressing a rule so your own
rule can say something different about the *same* symbols, you have two analyzers
competing for one domain, and the cheaper fix is to stop configuring the other one for
that domain.

Concretely, for the case that most often prompts a suppressor - the built-in naming rule
IDE1006 - the built-in conventions cover types, properties, methods, events and
interfaces, and have **no defaults for fields**. A repo that wants custom field naming can
define zero field naming rules, leaving IDE1006 silent on fields, and own field naming
entirely in its own analyzer. No suppressor, no coupling. Establish the equivalent fact
for whatever rule you are about to suppress before you write any code.

The cost you avoid is not just the suppressor's own code. It is the coupling in Rule 4 and
the permanent obligation to keep two rules agreeing.

**A worked case against.** One suppressor written to this guidance shipped and was deleted
a week later, once the suppressed rule was replaced outright rather than argued with.
Everything below still applies when you genuinely need a suppressor, but the strongest
evidence here is that this one should never have existed. All three signals were visible
before it was written:

- The suppressed rule and the replacement rule disagreed about the *same* symbols, rather
  than the replacement carving out an exception to a rule it otherwise agreed with.
- The suppressor had to reimplement the other rule's decision in order to know when to
  fire (Rule 4), so a change on either side would silently desynchronize them.
- The id could not be release-tracked at all (Rule 5) - a hint that the ecosystem does not
  treat suppressions as first-class shipped artifacts.

If you cannot answer "why is the other rule right in general?", stop.

## Rule 1: know what is suppressible

A diagnostic can be programmatically suppressed only when **all** hold:

1. It is not already suppressed in source (pragma or `SuppressMessageAttribute`).
2. Its **`DefaultSeverity`** is not `Error`.
3. It is not tagged `WellKnownDiagnosticTags.NotConfigurable`.

The severity test is on the **descriptor's default**, not the effective severity, in
`AnalyzerDriver.ApplyProgrammaticSuppressionsCore`:

```csharp
var suppressableDiagnostics = reportedDiagnostics.Where(d => !d.IsSuppressed &&
                                                             !d.IsNotConfigurable() &&
                                                             d.DefaultSeverity != DiagnosticSeverity.Error &&
                                                             !_diagnosticsProcessedForProgrammaticSuppressions.Contains(d));
```

So an `IDE####` or `CA####` rule that a repo raised to `error` in `.editorconfig` is still
suppressible - its descriptor default is below error. A rule whose *descriptor* is `Error`
never is. Do not infer which case you are in from the build output.

## Rule 2: API mechanics that bite

- `DiagnosticSuppressor.Initialize` is **sealed** and already calls
  `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`. Do not try to
  override it; there is nowhere to register actions.
- `SupportedDiagnostics` is sealed to empty. Suppressors expose `SupportedSuppressions`
  instead. Cache that array in a `static readonly` field for the same reason analyzers
  cache `SupportedDiagnostics`.
- `Suppression.Create(descriptor, diagnostic)` **throws** `ArgumentException` when
  `descriptor.SuppressedDiagnosticId != diagnostic.Id`. `context.ReportedDiagnostics` is
  documented as pre-filtered to your supported ids, but guard on `diagnostic.Id` anyway -
  the throw is a crash, not a diagnostic.
- Suppressors run concurrently with **each other**, but each `ReportSuppressions` call is
  single-threaded, so plain (non-concurrent) local caches inside it are safe.
- Suppression is additive across suppressors: several may suppress the same diagnostic,
  and the driver unions them.

## Rule 3: suppress only what the replacement rule accepts

If your suppressor exists because your own analyzer owns the naming or shape decision,
suppress **only the cases your analyzer accepts**. Suppressing unconditionally means
turning your rule off (`severity = none`) silently disables enforcement altogether,
because the original rule is suppressed too. Conditional suppression keeps the original
diagnostic as the backstop.

## Rule 4: you are coupling to another analyzer's report shape

Locating the symbol behind a suppressed diagnostic usually means
`root.FindNode(diagnostic.Location.SourceSpan)` and then pattern-matching a syntax node.
That encodes an **undocumented assumption about where the other analyzer reports**. If it
moves the location, your suppressor silently stops suppressing and the build breaks with
the original diagnostic.

Mitigate by matching defensively - a node shape that is wrong should `continue`, not throw
- and by keeping the negative control in Rule 7 so the coupling is exercised.

## Rule 5: suppressions cannot be release-tracked

The analyzer release files have no section for suppressions, and both workarounds fail the
build:

| Attempt | Result |
| --- | --- |
| `### Suppressions` heading | **RS2007** - "missing or invalid release header" |
| Suppression id row under `### New Rules` | **RS2002** - "is part of the next unshipped analyzer release, but is not a supported diagnostic for any analyzer" |

RS2002 fires because a suppressor declares `SupportedSuppressions`, never
`SupportedDiagnostics`, so the tracker cannot match the id to any analyzer.

Do **not** reach for `dotnet_diagnostic.RS2002.severity = none` - that disables
stale-entry detection for every id in the file, which is the check that catches a leftover
row after a rule is renamed or deleted.

Record suppressions as a `;`-commented row in the same shape as a rule, in the unshipped
file's header block, and move it with the release by hand:

```md
; Rule ID | Category | Severity | Notes
; --------|----------|----------|-------
; <PREFIX>SUPPRESS0001 | Suppression | Info | Suppresses <ID> for <condition>
```

## Rule 6: id convention

A suppression id shares a namespace with rule ids - an end user disables either with the
same `dotnet_diagnostic.<id>.severity` key or `NoWarn` entry - so an id that looks like a
rule id can collide with a future rule. Roslyn's design doc requires only "a unique
suppression ID" and prescribes no shape. What shipping packages do:

| Package | Rule ids | Suppression ids | Strategy |
| --- | --- | --- | --- |
| dotnet/runtime | `SYSLIB####` | `SYSLIBSUPPRESS0001` | Infix token + own counter |
| CommunityToolkit.Mvvm | `MVVMTK####` | `MVVMTKSPR0001` | Infix token + own counter |
| xunit.analyzers | `xUnit####` | `xUnitSuppress-CA1515` | Derived from the suppressed id |
| nunit.analyzers | `NUnit1xxx/2xxx` | `NUnit3001`-`3004` | Reserved numeric band |

Prefer an **infix token** (`<PREFIX>SUPPRESS####`, numbered from 0001 independently of the
rules). It makes collision with a future `<PREFIX>####` rule structurally impossible,
which a numeric band does not - a band is enforced only by memory, and RS2002 cannot
police it. Derive-from-the-suppressed-id also works and needs no counter, but cannot give
one reason two suppressed ids.

An id names the **reason**, so one id may legitimately cover several suppressed rules -
CommunityToolkit uses `MVVMTKSPR0001` for both CS0657 and CS0658.

## Rule 7: prove the suppressor is load-bearing

A suppressor that does nothing looks exactly like a suppressor that works: the build is
clean either way. Disable it and confirm the diagnostics come back:

```pwsh
dotnet build <project> --no-incremental -p:NoWarn=<SuppressionId>
```

`SuppressionDescriptor.IsDisabled` reads `SpecificDiagnosticOptions`, which MSBuild's
`NoWarn` feeds, so this turns the suppression off without editing anything. The expected
result is the exact set of diagnostics you believe are being suppressed - if it is a
different set, or empty, the suppressor is not doing what you think.

Do this once per suppressor and record the expected count.

## Rule 8: order the per-diagnostic work cheapest-first

`ReportSuppressions` runs over every reported diagnostic with your ids. Put the cheapest
discriminator first and defer anything expensive:

- Configuration (`AnalyzerConfigOptionsProvider.GetOptions(tree)`) is **per tree**; cache
  it per tree rather than reading it per diagnostic.
- `context.GetSemanticModel(tree)` is expensive; build it only for diagnostics that
  already passed the syntactic checks, and cache per tree.
- `SyntaxTree.GetRoot()` does **not** need caching - `ParsedSyntaxTree.GetRoot` is
  `return _root;` and `LazySyntaxTree` caches the parse via `Interlocked.CompareExchange`.
  A reviewer will suggest caching it; the honest answer is that it is a field read.
- `ISymbol.GetAttributes()` is likewise cached per symbol (`GetAttributesBag()` returns
  early on a sealed bag), so repeat calls are cheap - but do not *write* redundant calls,
  since they read as oversights.

## Testing a suppressor

The rule you are suppressing usually lives in an assembly the test project does not
reference (the IDE code-style analyzers, for example). Stand it in with a stub analyzer
that reports the same id at the same location:

- Give the stub a **default severity below `Error`**, matching the real rule, or Rule 1
  makes it unsuppressible and every test fails for the wrong reason.
- Run analyzer and suppressor together with
  `CompilationWithAnalyzersOptions(..., reportSuppressedDiagnostics: true)` so a test can
  distinguish *suppressed* from *never produced*. Without it, suppressed diagnostics are
  dropped and both cases look identical.
- Assert the **suppression id**, not the suppressed id.
  `Diagnostic.ProgrammaticSuppressionInfo` is internal; go through the public
  `Diagnostic.GetSuppressionInfo(Compilation)`, which means the harness has to return the
  compilation alongside the diagnostics:

  ```csharp
  SuppressionInfo? suppressionInfo = diagnostic.GetSuppressionInfo(compilation);

  suppressionInfo!.ProgrammaticSuppressions.Should().ContainSingle()
      .Which.Descriptor.Id.Should().Be(MySuppressor.SuppressionId);
  ```

- Cover the error-severity case explicitly with
  `CompilationOptions.WithSpecificDiagnosticOptions(id, ReportDiagnostic.Error)`, so
  Rule 1's default-vs-effective distinction stays pinned.

### The stub analyzer trips the analyzer authoring rules

Adding any `[DiagnosticAnalyzer]`-attributed type to a **test** project pulls that project
into the rules meant for shippable analyzer assemblies:

- With the attribute: RS1036 (`EnforceExtendedAnalyzerRules`), RS1038 (Workspaces
  reference), RS1041 (target framework), RS2008 (release tracking).
- Without the attribute: RS1001 (missing attribute).

There is no shape that satisfies both. Keep the attribute and turn those four off in the
**test project's** `.editorconfig`, with a comment saying the project only hosts test
doubles handed to `CompilationWithAnalyzers` directly. Leave them on in the real analyzer
project.
