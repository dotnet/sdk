# Rules with no code fix

These rules report a diagnostic and leave the user to fix it by hand. The list is here so
that a rule wanting a fixer is discoverable rather than something you find out by grepping
for one that is not there.

Adding a fixer for any of these is self-contained work;
[`netcore-getting-started.md`](netcore-getting-started.md) covers the mechanics. Delete the
row when the fixer lands.

A fixer is not automatically the right answer. Where applying one could change semantics,
the rule should keep reporting without it - decide that before writing code. Do not export a
placeholder that registers nothing: the generated rule table reports `CodeFix: True` for any
rule with an exported fixer, so an empty one advertises a fix that never appears.

## Shipping rules (33)

| Rule | Title | Category |
|------|-------|----------|
| [CA1010](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1010) | Generic interface should also be implemented | Design |
| [CA1014](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1014) | Mark assemblies with CLSCompliant | Design |
| [CA1016](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1016) | Mark assemblies with assembly version | Design |
| [CA1017](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1017) | Mark assemblies with ComVisible | Design |
| [CA1024](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1024) | Use properties where appropriate | Design |
| [CA1030](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1030) | Use events where appropriate | Design |
| [CA1040](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1040) | Avoid empty interfaces | Design |
| [CA1044](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1044) | Properties should not be write only | Design |
| [CA1050](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1050) | Declare types in namespaces | Design |
| [CA1058](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1058) | Types should not extend certain base types | Design |
| [CA1060](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1060) | Move pinvokes to native methods class | Design |
| [CA1061](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1061) | Do not hide base class methods | Design |
| [CA1063](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1063) | Implement IDisposable Correctly | Design |
| [CA1200](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1200) | Avoid using cref tags with a prefix | Documentation |
| [CA1304](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1304) | Specify CultureInfo | Globalization |
| [CA1305](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1305) | Specify IFormatProvider | Globalization |
| [CA1307](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1307) | Specify StringComparison for clarity | Globalization |
| [CA1308](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1308) | Normalize strings to uppercase | Globalization |
| [CA1710](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1710) | Identifiers should have correct suffix | Naming |
| [CA1711](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1711) | Identifiers should not have incorrect suffix | Naming |
| [CA1715](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1715) | Identifiers should have correct prefix | Naming |
| [CA1716](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1716) | Identifiers should not match keywords | Naming |
| [CA1721](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1721) | Property names should not match get methods | Naming |
| [CA1724](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1724) | Type names should not match namespaces | Naming |
| [CA1812](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1812) | Avoid uninstantiated internal classes | Performance |
| [CA1814](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1814) | Prefer jagged arrays over multidimensional | Performance |
| [CA1816](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1816) | Dispose methods should call SuppressFinalize | Usage |
| [CA2002](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2002) | Do not lock on objects with weak identity | Reliability |
| [CA2008](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2008) | Do not create tasks without passing a TaskScheduler | Reliability |
| [CA2207](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2207) | Initialize value type static fields inline | Usage |
| [CA2211](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2211) | Non-constant fields should not be visible | Usage |
| [CA2215](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2215) | Dispose methods should call base class dispose | Usage |
| [CA2216](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2216) | Disposable types should declare finalizer | Usage |

## Rules whose analyzer is also unimplemented (11)

These are stubs end to end. The analyzer's `SupportedDiagnostics` is empty, so the rule does
not ship, has no release-tracking row, and never reports. A fixer is moot until the analyzer
itself is written.

| Rule | Title | `RuleLevel` |
|------|-------|-------------|
| [CA1301](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1301) | Avoid duplicate accelerators | `Disabled` |
| [CA1306](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1306) | Set locale for data types | `Disabled` |
| [CA1414](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1414) | Mark boolean PInvoke arguments with MarshalAs | `Disabled` |
| [CA1500](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1500) | Variable names should not match field names | `Disabled` |
| [CA1601](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1601) | Do not use timers that prevent power state changes | `Disabled` |
| [CA1726](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1726) | Use preferred terms | `Disabled` |
| [CA2001](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2001) | Avoid calling problematic methods | `Disabled` |
| [CA2205](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2205) | Use managed equivalents of win32 api | `CandidateForRemoval` |
| [CA2212](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2212) | Do not mark serviced components with WebMethod | `Disabled` |
| [CA2236](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2236) | Call base class methods on ISerializable types | `Disabled` |
| [CA2239](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2239) | Provide deserialization methods for optional fields | `Disabled` |
