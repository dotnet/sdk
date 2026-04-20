The sample in this folder demonstrates using `derived` parameter to:

 - PascalCase a parameter value.
 - camelCase a parameter value.
 - kebab-case a paremeter value.
 - Regex replace a parameter value.
 - Chain value forms.

See

 - [`template.json`](./MyProject.Con/.template.config/template.json)
 - [`Program.cs`](./MyProject.Con/Program.cs)

Details

 - A `derived` `type` with a value transformation is performed using [value forms](https://github.com/dotnet/templating/blob/main/docs/Value-Forms.md) (`ValueForms` type).
 - The sample uses `replace`, `titleCase`, `kebabCase`, `firstLowerCaseInvariant` and `chain` value forms.
 - More value forms can be found in [the source code](https://github.com/dotnet/templating/tree/main/src/Microsoft.TemplateEngine.Orchestrator.RunnableProjects/ValueForms).

Related
 - [Change String Casing](../11-change-string-casing/README.md)
