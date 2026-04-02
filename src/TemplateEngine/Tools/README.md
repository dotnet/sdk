# Authoring tools

This is the home for template engine tools.

The following tools are distributed publicly as part of [authoring tools](../docs/authoring-tools/Authoring-Tools.md).

|Package name|Description|Documentation|Available since|
|---|---|---|---|
| [`Microsoft.TemplateEngine.Authoring.Tasks`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Authoring.Tasks) | MSBuild tasks for template authoring. These tasks are supposed to be added on template package build. The following tasks are available: <br/> - `Localize` task - creates the localization files for the templates on the package build. <br/> - `Validate` task ([planned](https://github.com/dotnet/templating/issues/2623)) - validates the templates for errors and warnings. | [Localization](../docs/authoring-tools/Localization.md) | .NET SDK 7.0.200 |
| [`Microsoft.TemplateEngine.Authoring.CLI`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Authoring.CLI) | `dotnet CLI` [tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) with utilities for template authoring. Offers the following commands: <br/> - `localize` - creates or updates the localization files for the templates.  <br/> - `verify` - allows to test the templates and compare them with expected output (snapshot). <br/> - `validate` ([planned](https://github.com/dotnet/templating/issues/2623)) - validates the template(s) for errors and warnings  |[Localization](../docs/authoring-tools/Localization.md) </br>[Template testing](../docs/authoring-tools/Templates-Testing-Tooling.md#cli)| .NET SDK 7.0.200 |
| [`Microsoft.TemplateEngine.Authoring.TemplateVerifier`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Authoring.TemplateVerifier) | The class library containing [snapshot testing framework](../docs/authoring-tools/Templates-Testing-Tooling.md) for the templates. Facilitates writing the tests for templates using Xunit test framework.|[Template testing](../docs/authoring-tools/Templates-Testing-Tooling.md#api)| .NET SDK 7.0.200 |

The following tools are only used internally:

|Package name|Description|Documentation|Available since|
|---|---|---|---|
| `Microsoft.TemplateEngine.Authoring.TemplateApiVerifier` | The class library containing the basic template engine host that can be used with [snapshot testing framework](../docs/authoring-tools/Templates-Testing-Tooling.md) to test the templates with using `Microsoft.TemplateEngine.Edge` only. |[Test examples](https://github.com/dotnet/templating/blob/main/test/Microsoft.TemplateEngine.IDE.IntegrationTests/SnapshotTests.cs)| .NET SDK 7.0.200 |
| `Microsoft.TemplateEngine.TemplateDiscovery` | The CLI tool to generate the search cache for users of `Microsoft.TemplateSearch.Common` and `dotnet new search` command || .NET SDK 3.1 |

The unit and integration tests for the tools are in [`test`](https://github.com/dotnet/templating/tree/main/test) folder.

## Notes

Prior to .NET SDK 7.0.2xx, `Microsoft.TemplateEngine.Authoring.Tasks` package was distributed as [`Microsoft.TemplateEngine.Tasks`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Tasks). If you are using old package, please consider switching to new package.

Prior to .NET SDK 7.0.2xx, the `dotnet CLI` tool for localization was distributed as [`Microsoft.TemplateEngine.TemplateLocalizer`](https://www.nuget.org/packages/Microsoft.TemplateEngine.TemplateLocalizer) package. 
If you are using `Microsoft.TemplateEngine.TemplateLocalizer` tool, please consider switching to `Microsoft.TemplateEngine.Authoring.CLI` tool.