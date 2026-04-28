# Authoring tools

## Overview

Besides the actual implementation of .NET Template Engine, the repo contains various tools that help to author the templates.
They are not shipped together with .NET SDK, but available on NuGet.org.

|Package name|Description|Documentation|Available since|
|---|---|---|---|
| [`Microsoft.TemplateEngine.Authoring.Tasks`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Authoring.Tasks) | MSBuild tasks for template authoring. These tasks are supposed to be added on template package build. The following tasks are available: <br/> - `Localize` task - creates the localization files for the templates on the package build. <br/> - `Validate` task ([planned](https://github.com/dotnet/templating/issues/2623)) - validates the templates for errors and warnings. | [Localization](Localization.md) | .NET SDK 7.0.200 |
| [`Microsoft.TemplateEngine.Authoring.CLI`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Authoring.CLI) | `dotnet CLI` [tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) with utilities for template authoring. Offers the following commands: <br/> - `localize` - creates or updates the localization files for the templates.  <br/> - `verify` - allows to test the templates and compare them with expected output (snapshot). <br/> - `validate` ([planned](https://github.com/dotnet/templating/issues/2623)) - validates the template(s) for errors and warnings  |[Localization](Localization.md) </br>[Template testing](Templates-Testing-Tooling.md#cli)| .NET SDK 7.0.200 |
| [`Microsoft.TemplateEngine.Authoring.TemplateVerifier`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Authoring.TemplateVerifier) | The class library containing [snapshot testing framework](Templates-Testing-Tooling.md) for the templates. Facilitates writing the tests for templates using Xunit test framework.|[Template testing](Templates-Testing-Tooling.md#api)| .NET SDK 7.0.200 |
| [`Microsoft.TemplateEngine.Authoring.Templates`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Authoring.Templates) | The template package containing the templates for template authoring: <br/> - `template.json` - a template for template.json configuration file for .NET template. <br/> - `	template-package` - a project for creating a template package containing .NET templates.  || .NET SDK 8.0.100 - preview 1 |

## Template Samples

We have created [dotnet template samples](https://github.com/dotnet/templating/tree/main/dotnet-template-samples), which shows how you can use the template engine to create new templates. The samples are setup to be stand alone for specific examples. Those templates are not published to NuGet.org.


## Notes

Prior to .NET SDK 7.0.2xx, `Microsoft.TemplateEngine.Authoring.Tasks` package was distributed as [`Microsoft.TemplateEngine.Tasks`](https://www.nuget.org/packages/Microsoft.TemplateEngine.Tasks). If you are using old package, please consider switching to new package.

Prior to .NET SDK 7.0.2xx, the `dotnet CLI` tool for localization was distributed as [`Microsoft.TemplateEngine.TemplateLocalizer`](https://www.nuget.org/packages/Microsoft.TemplateEngine.TemplateLocalizer) package. 
If you are using `Microsoft.TemplateEngine.TemplateLocalizer` tool, please consider switching to `Microsoft.TemplateEngine.Authoring.CLI` tool.