# Analyzer Configuration

Starting with version `2.6.3`, all the analyzer NuGet packages produced in this repo, including the FxCop Analyzers NuGet package, support _.editorconfig based analyzer configuration_. End users can configure the behavior of specific CA rule(s) OR all configurable CA rules by specifying supported key-value pair options in an `.editorconfig` file. You can read more about `.editorconfig` format [here](https://editorconfig.org/).

## .editorconfig format
Analyzer configuration options from an .editorconfig file are parsed into _general_ and _specific_ configuration options. General configuration enables configuring the behavior of all CA rules for which the provided option is valid. Specific configuration enables configuring each CA rule ID or CA rules belonging to each rule category, such as 'Naming', 'Design', 'Performance', etc. Our options are _case-insensitive_. Below are the supported formats:
   1. General configuration option:
      1. `dotnet_code_quality.OptionName = OptionValue`
   2. Specific configuration option:
      1. `dotnet_code_quality.RuleId.OptionName = OptionValue`
      2. `dotnet_code_quality.RuleCategory.OptionName = OptionValue`

For example, end users can configure the analyzed API surface for analyzers using the below `api_surface` option specification:
   1. General configuration option:
      1. `dotnet_code_quality.api_surface = public`
   2. Specific configuration option:
      1. `dotnet_code_quality.CA1040.api_surface = public`
      2. `dotnet_code_quality.Naming.api_surface = public`

## Enabling .editorconfig based configuration for a project
End users can enable .editorconfig based configuration for individual projects by just copying the .editorconfig file with the options to the project root directory. In future, we plan to support hierarchical directory based configuration with an .editorconfig file at the solution directory, repo root directory or even individual document directories.

## Supported .editorconfig options
This section documents the list of supported .editorconfig key-value options for CA rules.

### Analyzed API surface
Option Name:  `api_surface`
Option Values:
| Option Value | Summary |
| --- | --- |
| `public` | Analyzes public APIs that are externally visible outside the assembly. |
| `internal` | Analyzes internal APIs that are visible within the assembly and to assemblies with [InternalsVisibleToAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute) access. |
| `private` | Analyzes private APIs that are only visible within the containing type. |
| `all` | Analyzes all APIs, regardless of the symbol visibility. |

Users can also provide a comma separated list of above option values. For example, `private, internal` configures analysis of the entire non-public API surface.