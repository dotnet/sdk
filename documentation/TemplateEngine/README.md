# .NET Template Engine

## Overview

The .NET Template Engine provides the libraries for template instantiation and template package management used in [`dotnet new`](https://learn.microsoft.com/dotnet/core/tools/dotnet-new), the [New Project Dialog](https://learn.microsoft.com/visualstudio/ide/create-new-project?view=vs-2022), and the New Item Dialog in Visual Studio. The source code is located in this repository under [`src/TemplateEngine/Microsoft.TemplateEngine.*`](../../src/TemplateEngine) and the libraries are distributed as NuGet packages on nuget.org.

> **Note:** The template engine was previously maintained in the [dotnet/templating](https://github.com/dotnet/templating) repository and has been merged into this repository (dotnet/sdk).

## Key Packages

| Package | Description |
|---|---|
| `Microsoft.TemplateEngine.Edge` | The template engine infrastructure: managing template packages, templates, components, and executing templates. Main API surface for products aiming to use the template engine. See the [Inside the Template Engine](api/Inside-the-Template-Engine.md) article for more information. |
| `Microsoft.TemplateEngine.Abstractions` | Contains the main contracts between `Edge` and components. |
| `Microsoft.TemplateEngine.Orchestrator.RunnableProjects` | The template generator based on `template.json` configuration. |
| `Microsoft.TemplateSearch.Common` | Facilitates template package search on nuget.org. |
| `Microsoft.TemplateEngine.IDE` | Lightweight API overlay over `Microsoft.TemplateEngine.Edge`. |
| `Microsoft.TemplateEngine.Authoring.Tasks` | Authoring tools: MSBuild tasks for template authoring. |
| `Microsoft.TemplateEngine.Authoring.CLI` | Authoring tools: dotnet CLI tool with utilities for template authoring. |
| `Microsoft.TemplateEngine.Authoring.TemplateVerifier` | Authoring tools: [snapshot testing framework](authoring-tools/Templates-Testing-Tooling.md) for templates. |

## `dotnet new`

The `dotnet new` CLI command is located in [`src/Cli/Microsoft.TemplateEngine.Cli`](../../src/Cli/Microsoft.TemplateEngine.Cli).

Issues for `dotnet new` CLI UX should be opened in this repository ([dotnet/sdk](https://github.com/dotnet/sdk)).

## Template Content Repositories

[.NET default templates](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-sdk-templates) are located across several repositories:

| Templates | Repository |
|---|---|
| Common project and item templates | [dotnet/sdk](https://github.com/dotnet/sdk) |
| ASP.NET and Blazor templates | [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore) |
| ASP.NET Single Page Application templates | [dotnet/spa-templates](https://github.com/dotnet/spa-templates) |
| Aspire templates | [dotnet/aspire](https://github.com/dotnet/aspire) |
| WPF templates | [dotnet/wpf](https://github.com/dotnet/wpf) |
| Windows Forms templates | [dotnet/winforms](https://github.com/dotnet/winforms) |
| Test templates | [dotnet/test-templates](https://github.com/dotnet/test-templates) |
| MAUI templates | [dotnet/maui](https://github.com/dotnet/maui) |

Issues for template content should be opened in the corresponding repository. Suggestions for new templates should be opened in the closest repository from the list above. For example, if you have a suggestion for a new web template, please create an issue in [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore).

## How to Author Templates

The starting point tutorial on how to create new templates is available on [Microsoft Learn](https://learn.microsoft.com/dotnet/core/tutorials/cli-templates-create-project-template).

More advanced information can be found in the documentation in this folder:

- [Reference for template.json](Reference-for-template.json.md)
- [Conditional processing and comment syntax](Conditional-processing-and-comment-syntax.md)
- [Available symbols generators](Available-Symbols-Generators.md)
- [Post-action registry](Post-Action-Registry.md)
- [Constraints](Constraints.md)
- [Conditions](Conditions.md)
- [Binding and project context evaluation](Binding-and-project-context-evaluation.md)
- [Value forms](Value-Forms.md)
- [Using group identity](Using-Group-Identity.md)
- [Template samples](Samples/)

### Authoring Tools

In addition to the template engine implementation, there are various tools to help author templates. They are not shipped with the .NET SDK but are available on NuGet.org. More information can be found in the [Authoring Tools documentation](authoring-tools/Authoring-Tools.md).

## Contributing

We welcome contributions! You can contribute by:

- [Creating an issue](https://github.com/dotnet/sdk/issues/new/choose) in the dotnet/sdk repository
- Contributing a PR that fixes an issue or implements a new feature

See the [SDK contributing guide](../../CONTRIBUTING.md) for details on how to build, run, and debug.

For template engine specific documentation on contributing, see the [contributing guide](contributing/).

