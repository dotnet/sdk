# Symbol-action analyzers

Detail for [design.md](design.md) Rule 2. Use this when a rule inspects declared
symbols across a compilation, especially naming or declaration-style rules.

## Cover each declaration shape once

`RegisterSymbolAction` is appropriate for namespaces, named types, methods,
properties, fields, events, and parameters. Register `SymbolKind.Parameter` for
member, primary-constructor, and indexer parameters.

`SymbolKind.TypeParameter` is not supported for symbol actions (RS1003). Visit
type parameters from the named type or method that declares them.

Local functions, lambdas, anonymous methods, and their parameters live in
executable code rather than the declaration symbol walk. Reach ordinary local
variable declarators with `OperationKind.VariableDeclarator`, local functions with
`OperationKind.LocalFunction`, and lambdas/anonymous methods with
`OperationKind.AnonymousFunction`; inspect the operation's method symbol for its
parameters and type parameters. Test each registration path so a declaration is
neither missed nor reported twice.

Do not treat `VariableDeclarator` as complete coverage for every local symbol.
Declaration expressions (`out var`, deconstruction) and declaration patterns use
different operation shapes. Enumerate the source forms the rule promises to cover
and add the matching operation/syntax registrations and tests deliberately.

## Report only names owned at the report site

Before reporting or offering a rename, exclude declarations whose source name is
synthetic or dictated elsewhere:

- `IsImplicitlyDeclared`, no source location, or an empty name;
- `ISymbol.IsOverride` - the overridden declaration owns the name;
- non-empty `ExplicitInterfaceImplementations` on methods, properties, or events;
- method kinds whose names are compiler-defined, such as accessors and operators.

For C# naming rules, also exclude `IPropertySymbol.IsIndexer`: its symbol name is
the synthetic `this[]`, not source text a rename can change. Do not apply that
assumption to Visual Basic default properties, whose names are user-defined.

A diagnostic with no valid local remedy teaches users to suppress the rule. Keep
the candidate filter beside the report path and test every excluded shape.

## Diagnose analyzer crashes and static initialization

An exception thrown by a registered analyzer callback is surfaced as `AD0001`.
Build output may contain only the exception type and message, so reproduce it in
the analyzer test harness or under a debugger rather than guessing from the
diagnostic location. Failures while loading the analyzer assembly or constructing
the analyzer can instead surface as load diagnostics such as C# `CS8032` or the
corresponding language diagnostic.

Static field and auto-property backing-field initializers execute in their
initialization order. An initializer that reads a field/backing field whose
initializer has not run yet observes its zero value. This is especially deceptive
when the value is a struct: it can look valid while containing null reference
fields, then fail much later in unrelated code. Do not rely on textual order across
partial-type parts. Declare dependencies before consumers within one ordered part,
avoid initializer dependency chains, and add a test that forces type initialization.
