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
1. Per-project .editorconfig file: End users can enable .editorconfig based configuration for individual projects by just copying the .editorconfig file with the options to the project root directory. In future, we plan to support hierarchical directory based configuration with an .editorconfig file at the solution directory, repo root directory or even individual document directories.
2. Shared .editorconfig file: If you would like to share a common .editorconfig file between projects, say `<%PathToSharedEditorConfig%>\.editorconfig`, then you should add the following MSBuild property group and item group to a shared props file that is imported _before_ the FxCop analyzer props files (that come from the FxCop analyzer NuGet package reference):
```
  <PropertyGroup>
    <SkipDefaultEditorConfigAsAdditionalFile>true</SkipDefaultEditorConfigAsAdditionalFile>
  </PropertyGroup>
  <ItemGroup Condition="Exists('<%PathToSharedEditorConfig%>\.editorconfig')" >
    <AdditionalFiles Include="<%PathToSharedEditorConfig%>\.editorconfig" />
  </ItemGroup>
```
Note that this is a temporary workaround that is needed until the dotnet compilers and project system start understanding and respecting .editorconfig files.

## Supported .editorconfig options
This section documents the list of supported .editorconfig key-value options for CA rules.

### Analyzed API surface
Option Name: `api_surface`

Configurable Rules: [CA1000](https://docs.microsoft.com/visualstudio/code-quality/ca1000-do-not-declare-static-members-on-generic-types), [CA1003](https://docs.microsoft.com/visualstudio/code-quality/ca1003-use-generic-event-handler-instances), [CA1008](https://docs.microsoft.com/visualstudio/code-quality/ca1008-enums-should-have-zero-value), [CA1010](https://docs.microsoft.com/visualstudio/code-quality/ca1010-collections-should-implement-generic-interface), [CA1012](https://docs.microsoft.com/visualstudio/code-quality/ca1012-abstract-types-should-not-have-constructors), [CA1024](https://docs.microsoft.com/visualstudio/code-quality/ca1024-use-properties-where-appropriate), [CA1027](https://docs.microsoft.com/visualstudio/code-quality/ca1027-mark-enums-with-flagsattribute), [CA1028](https://docs.microsoft.com/visualstudio/code-quality/ca1028-enum-storage-should-be-int32), [CA1030](https://docs.microsoft.com/visualstudio/code-quality/ca1030-use-events-where-appropriate), [CA1036](https://docs.microsoft.com/visualstudio/code-quality/ca1036-override-methods-on-comparable-types), [CA1040](https://docs.microsoft.com/visualstudio/code-quality/ca1040-avoid-empty-interfaces), [CA1041](https://docs.microsoft.com/visualstudio/code-quality/ca1041-provide-obsoleteattribute-message), [CA1043](https://docs.microsoft.com/visualstudio/code-quality/ca1043-use-integral-or-string-argument-for-indexers), [CA1044](https://docs.microsoft.com/visualstudio/code-quality/ca1044-properties-should-not-be-write-only), [CA1051](https://docs.microsoft.com/visualstudio/code-quality/ca1051-do-not-declare-visible-instance-fields), [CA1052](https://docs.microsoft.com/visualstudio/code-quality/ca1052-static-holder-types-should-be-sealed), [CA1054](https://docs.microsoft.com/visualstudio/code-quality/ca1054-uri-parameters-should-not-be-strings), [CA1055](https://docs.microsoft.com/visualstudio/code-quality/ca1055-uri-return-values-should-not-be-strings), [CA1056](https://docs.microsoft.com/visualstudio/code-quality/ca1056-uri-properties-should-not-be-strings), [CA1058](https://docs.microsoft.com/visualstudio/code-quality/ca1058-types-should-not-extend-certain-base-types), [CA1063](https://docs.microsoft.com/visualstudio/code-quality/ca1063-implement-idisposable-correctly), [CA1708](https://docs.microsoft.com/visualstudio/code-quality/ca1708-identifiers-should-differ-by-more-than-case), [CA1710](https://docs.microsoft.com/visualstudio/code-quality/ca1710-identifiers-should-have-correct-suffix), [CA1711](https://docs.microsoft.com/visualstudio/code-quality/ca1711-identifiers-should-not-have-incorrect-suffix), [CA1714](https://docs.microsoft.com/visualstudio/code-quality/ca1714-flags-enums-should-have-plural-names), [CA1715](https://docs.microsoft.com/visualstudio/code-quality/ca1715-identifiers-should-have-correct-prefix), [CA1716](https://docs.microsoft.com/visualstudio/code-quality/ca1716-identifiers-should-not-match-keywords), [CA1717](https://docs.microsoft.com/visualstudio/code-quality/ca1717-only-flagsattribute-enums-should-have-plural-names), [CA1720](https://docs.microsoft.com/visualstudio/code-quality/ca1720-identifiers-should-not-contain-type-names), [CA1721](https://docs.microsoft.com/visualstudio/code-quality/ca1721-property-names-should-not-match-get-methods), [CA1725](https://docs.microsoft.com/visualstudio/code-quality/ca1725-parameter-names-should-match-base-declaration), [CA1802](https://docs.microsoft.com/visualstudio/code-quality/ca1802-use-literals-where-appropriate), [CA1815](https://docs.microsoft.com/visualstudio/code-quality/ca1815-override-equals-and-operator-equals-on-value-types), [CA1819](https://docs.microsoft.com/visualstudio/code-quality/ca1819-properties-should-not-return-arrays), [CA2217](https://docs.microsoft.com/visualstudio/code-quality/ca2217-do-not-mark-enums-with-flagsattribute), [CA2225](https://docs.microsoft.com/visualstudio/code-quality/ca2225-operator-overloads-have-named-alternates), [CA2226](https://docs.microsoft.com/visualstudio/code-quality/ca2226-operators-should-have-symmetrical-overloads), [CA2231](https://docs.microsoft.com/visualstudio/code-quality/ca2231-overload-operator-equals-on-overriding-valuetype-equals), [CA2234](https://docs.microsoft.com/visualstudio/code-quality/ca2234-pass-system-uri-objects-instead-of-strings)

Option Values:

| Option Value | Summary |
| --- | --- |
| `public` | Analyzes public APIs that are externally visible outside the assembly. |
| `internal` or `friend` | Analyzes internal APIs that are visible within the assembly and to assemblies with [InternalsVisibleToAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute) access. |
| `private` | Analyzes private APIs that are only visible within the containing type. |
| `all` | Analyzes all APIs, regardless of the symbol visibility. |

Default Value: `public`

Example: `dotnet_code_quality.api_surface = all`

Users can also provide a comma separated list of above option values. For example, `dotnet_code_quality.api_surface = private, internal` configures analysis of the entire non-public API surface.

### Analyzed output kinds
Option Name: `output_kind`

Configurable Rules: [CA2007](../src/Microsoft.CodeQuality.Analyzers/Microsoft.CodeQuality.Analyzers.md#ca2007-do-not-directly-await-a-task)

Option Values: One or more fields of enum [Microsoft.CodeAnalysis.CompilationOptions.OutputKind](http://source.roslyn.io/#q=Microsoft.CodeAnalysis.OutputKind) as a comma separated list.

Default Value: All output kinds

Example: `dotnet_code_quality.CA2007.output_kind = ConsoleApplication, DynamicallyLinkedLibrary`

### Async void methods
Option Name: `skip_async_void_methods`

Configurable Rules: [CA2007](../src/Microsoft.CodeQuality.Analyzers/Microsoft.CodeQuality.Analyzers.md#ca2007-do-not-directly-await-a-task)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA2007.skip_async_void_methods = true`

### Single letter type parameters
Option Name: `allow_single_letter_type_parameters`

Configurable Rules: [CA1715](https://docs.microsoft.com/visualstudio/code-quality/ca1715-identifiers-should-have-correct-prefix)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA1715.allow_single_letter_type_parameters = true`
