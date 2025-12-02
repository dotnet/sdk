<!-- Markdown used in this file is tweaked for GitHub rendering, it's possible that it renders differently in other tools (e.g. VS Code) -->

# Analyzer Configuration

All the analyzer NuGet packages produced in this repo support _.editorconfig based analyzer configuration_. End users can configure the behavior of specific CA rule(s) OR all configurable CA rules by specifying supported key-value pair options in an `.editorconfig` file. You can read more about `.editorconfig` format [here](https://editorconfig.org/).

## .editorconfig format

Analyzer configuration options from an `.editorconfig` file are parsed into _general_ and _specific_ configuration options. General configuration enables configuring the behavior of all CA rules for which the provided option is valid. Specific configuration enables configuring each CA rule ID or CA rules belonging to each rule category, such as `Naming`, `Design`, `Performance`, etc. or CA rules with a specific custom tag, such as `Dataflow`. Our options are _case-insensitive_. Below are the supported formats:

   1. General configuration option:
      1. `dotnet_code_quality.OptionName = OptionValue`
   2. Specific configuration option:
      1. `dotnet_code_quality.RuleId.OptionName = OptionValue`
      2. `dotnet_code_quality.RuleCategory.OptionName = OptionValue`
      3. `dotnet_code_quality.RuleCustomTag.OptionName = OptionValue`

For example, end users can configure the analyzed API surface for analyzers using the below `api_surface` option specification:

   1. General configuration option:
      1. `dotnet_code_quality.api_surface = public`
   2. Specific configuration option:
      1. `dotnet_code_quality.CA1040.api_surface = public`
      2. `dotnet_code_quality.Naming.api_surface = public`
      3. `dotnet_code_quality.Dataflow.api_surface = public`

## Enabling .editorconfig based configuration

### VS2019 16.3 and later + Analyzer package version 3.3.x and later

End users can enable `.editorconfig` based configuration for individual documents, folders, projects, solution or entire repo by creating an `.editorconfig` file with the options in the corresponding directory. This file can also contain `.editorconfig` based diagnostic severity configuration entries. See [here](https://learn.microsoft.com/visualstudio/code-quality/use-roslyn-analyzers#rule-severity) for more details.

### Prior to VS2019 16.3 or using an analyzer package version prior to 3.3.x

1. Per-project `.editorconfig` file: End users can enable `.editorconfig` based configuration for individual projects by just copying the `.editorconfig` file with the options to the project root directory.
2. Shared `.editorconfig` file: If you would like to share a common `.editorconfig` file between projects, say `<%PathToSharedEditorConfig%>\.editorconfig`, then you should add the following MSBuild property group and item group to a shared props file that is imported _before_ the FxCop analyzer props files (that come from the FxCop analyzer NuGet package reference):

```xml
  <PropertyGroup>
    <SkipDefaultEditorConfigAsAdditionalFile>true</SkipDefaultEditorConfigAsAdditionalFile>
  </PropertyGroup>
  <ItemGroup Condition="Exists('<%PathToSharedEditorConfig%>\.editorconfig')" >
    <AdditionalFiles Include="<%PathToSharedEditorConfig%>\.editorconfig" />
  </ItemGroup>
```

Note that this additional file based approach is also supported on VS2019 16.3 and later releases for backwards compatibility.

**The additional file based approach is no longer supported starting in Microsoft.CodeAnalysis.NetAnalyzers v5.0.4. It will be implicitly discovered (if the file is in the project's directory or any ancestor directory), or it should be converted into a 'globalconfig'. See [Configuration files for code analysis rules](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/configuration-files).**

## Supported .editorconfig options

This section documents the list of supported `.editorconfig` key-value options for CA rules.

### Analyzed API surface

Option Name: `api_surface`

Configurable Rules:
[CA1000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1000),
[CA1002](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1002),
[CA1003](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1003),
[CA1005](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1005),
[CA1008](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1008),
[CA1010](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1010),
[CA1012](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1012),
[CA1021](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1021),
[CA1024](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1024),
[CA1027](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1027),
[CA1028](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1028),
[CA1030](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1030),
[CA1036](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1036),
[CA1040](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1040),
[CA1041](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1041),
[CA1043](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1043),
[CA1044](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1044),
[CA1045](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1045),
[CA1046](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1046),
[CA1047](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1047),
[CA1051](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1051),
[CA1052](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1052),
[CA1054](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1054),
[CA1055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1055),
[CA1056](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1056),
[CA1058](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1058),
[CA1062](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1062),
[CA1063](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1063),
[CA1068](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1068),
[CA1070](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1070),
[CA1700](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1700),
[CA1707](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1707),
[CA1708](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1708),
[CA1710](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1710),
[CA1711](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1711),
[CA1714](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1714),
[CA1715](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1715),
[CA1716](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1716),
[CA1717](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1717),
[CA1720](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1720),
[CA1721](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1721),
[CA1725](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1725),
[CA1801](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1801),
[CA1802](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1802),
[CA1815](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1815),
[CA1819](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1819),
[CA1822](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1822),
[CA1859](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1859),
[CA2208](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2208),
[CA2217](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2217),
[CA2225](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2225),
[CA2226](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2226),
[CA2231](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2231),
[CA2234](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2234)

Option Values:

| Option Value | Summary |
| --- | --- |
| `public` | Analyzes public APIs that are externally visible outside the assembly. |
| `internal` or `friend` | Analyzes internal APIs that are visible within the assembly and to assemblies with [InternalsVisibleToAttribute](https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute) access. |
| `private` | Analyzes private APIs that are only visible within the containing type. |
| `all` | Analyzes all APIs, regardless of the symbol visibility. |

Default Value: `public`, except as listed below

  1. CA1859: default value is `private`.

Example: `dotnet_code_quality.api_surface = all`

Users can also provide a comma separated list of above option values. For example, `dotnet_code_quality.api_surface = private, internal` configures analysis of the entire non-public API surface.

### Analyzed output kinds

Option Name: `output_kind`

Configurable Rules:
[CA1515](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1515),
[CA2007](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2007)

Option Values: One or more fields of enum [Microsoft.CodeAnalysis.CompilationOptions.OutputKind](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.outputkind) as a comma separated list.

Default Value: _All output kinds_

Example: `dotnet_code_quality.CA2007.output_kind = ConsoleApplication, DynamicallyLinkedLibrary`

### Required modifiers for analyzed APIs

Option Name: `required_modifiers`

Configurable Rules: [CA1802](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1802)

Option Values: Comma separated listed of one or more modifier values from the below table. Note that not all values are applicable for every configurable rule.

| Option Value | Summary |
| --- | --- |
| `none` | No modifier requirement. |
| `static` or `Shared` | Must be declared as `static` (`Shared` in Visual Basic). |
| `const` | Must be declared as `const`. |
| `readonly` | Must be declared as `readonly`. |
| `abstract` | Must be declared as `abstract`. |
| `virtual` | Must be declared as `virtual`. |
| `override` | Must be declared as `override`. |
| `sealed` | Must be declared as `sealed`. |
| `extern` | Must be declared as `extern`. |
| `async` | Must be declared as `async`. |

Default Value: Depends on each configurable rule:

   1. CA1802: default value is `static`. Set the value to `none` to allow flagging instance fields.

Example: `dotnet_code_quality.CA1802.required_modifiers = none`.

### Async void methods

Option Name: `exclude_async_void_methods`

Configurable Rules: [CA2007](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2007)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA2007.exclude_async_void_methods = true`

### Single letter type parameters

Option Name: `exclude_single_letter_type_parameters`

Configurable Rules: [CA1715](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1715)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = true`

### Exclude extension method 'this' parameter

Option Name: `exclude_extension_method_this_parameter`

Configurable Rules: [CA1062](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1062)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA1062.exclude_extension_method_this_parameter = true`

### Null check validation methods

Option Name: `null_check_validation_methods`

Configurable Rules: [CA1062](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1062)

Option Values: Names of null check validation methods (separated by `|`) that validate arguments passed to the method are non-null.
Allowed method name formats:

  1. Method name only (includes all methods with the name, regardless of the containing type or namespace).
  2. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format)
     with an optional `M:` prefix.

Default Value: _None_

Examples:

| Option Value | Summary |
| --- | --- |
| `dotnet_code_quality.null_check_validation_methods = Validate` | Matches all methods named `Validate` in the compilation. |
| `dotnet_code_quality.null_check_validation_methods = Validate1\|Validate2` | Matches all methods named either `Validate1` or `Validate2` in the compilation. |
| `dotnet_code_quality.null_check_validation_methods = NS.MyType.Validate(ParamType)` | Matches specific method `Validate` with given fully qualified signature. |
| `dotnet_code_quality.null_check_validation_methods = NS1.MyType1.Validate1(ParamType)\|NS2.MyType2.Validate2(ParamType)` | Matches specific methods `Validate1` and `Validate2` with respective fully qualified signature. |

### Additional string formatting methods

Option Name: `additional_string_formatting_methods`

Configurable Rules: [CA2241](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2241)

Option Values: Names of additional string formatting methods (separated by `|`).
Allowed method name formats:

  1. Method name only (includes all methods with the name, regardless of the containing type or namespace).
  2. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format)
     with an optional `M:` prefix.

Default Value: _None_

Examples:

| Option Value | Summary |
| --- | --- |
| `dotnet_code_quality.additional_string_formatting_methods = MyFormat` | Matches all methods named `MyFormat` in the compilation. |
| `dotnet_code_quality.additional_string_formatting_methods = MyFormat1\|MyFormat2` | Matches all methods named either `MyFormat1` or `MyFormat2` in the compilation. |
| `dotnet_code_quality.additional_string_formatting_methods = NS.MyType.MyFormat(ParamType)` | Matches specific method `MyFormat` with given fully qualified signature. |
| `dotnet_code_quality.additional_string_formatting_methods = NS1.MyType1.MyFormat1(ParamType)\|NS2.MyType2.MyFormat2(ParamType)` | Matches specific methods `MyFormat1` and `MyFormat2` with respective fully qualified signature. |

Option Name: `try_determine_additional_string_formatting_methods_automatically`

Boolean option to enable heuristically detecting of additional string formatting methods
A method is considered a string formatting method if it has a `string format` parameter followed by a `params object[]` parameter.

Configurable Rules: [CA2241](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2241)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.try_determine_additional_string_formatting_methods_automatically = true`

### Excluded symbols

Configurable Rules:
[CA1001](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1001),
[CA1054](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1054),
[CA1055](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1055),
[CA1056](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1056),
[CA1062](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1062),
[CA1068](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1068),
[CA1303](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1303),
[CA1304](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1304),
[CA1508](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1508),
[CA2000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2000),
[CA2100](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2100),
[CA2301](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2301),
[CA2302](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2302),
[CA2311](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2311),
[CA2312](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2312),
[CA2321](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2321),
[CA2322](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2322),
[CA2327](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2327),
[CA2328](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2328),
[CA2329](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2329),
[CA2330](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2330),
[CA3001](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3001),
[CA3002](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3002),
[CA3003](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3003),
[CA3004](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3004),
[CA3005](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3005),
[CA3006](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3006),
[CA3007](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3007),
[CA3008](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3008),
[CA3009](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3009),
[CA3010](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3010),
[CA3011](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3011),
[CA3012](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca3012),
[CA5361](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5361),
[CA5376](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5376),
[CA5377](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5377),
[CA5378](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5378),
[CA5380](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5380),
[CA5381](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5381),
[CA5382](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5382),
[CA5383](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5383),
[CA5384](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5384),
[CA5387](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5387),
[CA5388](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5388),
[CA5389](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5389),
[CA5390](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5390)

#### Excluded symbol names

Option Name: `excluded_symbol_names`

Option Values: Names of symbols (separated by `|`) that are excluded for analysis.
Allowed symbol name formats:

  1. Symbol name (includes all symbols with the name, regardless of the containing type or namespace).
  2. Symbol name ending with a wildcard symbol (includes all symbols whose name starts with the given name, regardless of the containing type or namespace).
  3. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format).
    Note that each symbol name requires a symbol kind prefix, such as `M:` prefix for methods, `T:` prefix for types, `N:` prefix for namespaces, etc.
  4. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format) and ending with the wildcard symbol.
    Note that each symbol name requires a symbol kind prefix, such as `M:` prefix for methods, `T:` prefix for types, `N:` prefix for namespaces, etc.
  5. `.ctor` for constructors and `.cctor` for static constructors.

Default Value: _None_

Examples:

| Option Value | Summary |
| --- | --- |
| `dotnet_code_quality.excluded_symbol_names = Validate` | Matches all symbols named `Validate` in the compilation. |
| `dotnet_code_quality.excluded_symbol_names = Validate1\|Validate2` | Matches all symbols named either `Validate1` or `Validate2` in the compilation. |
| `dotnet_code_quality.excluded_symbol_names = M:NS.MyType.Validate(ParamType)` | Matches specific method `Validate` with given fully qualified signature. |
| `dotnet_code_quality.excluded_symbol_names = M:NS1.MyType1.Validate1(ParamType)\|M:NS2.MyType2.Validate2(ParamType)` | Matches specific methods `Validate1` and `Validate2` with respective fully qualified signature. |
| `dotnet_code_quality.excluded_symbol_names = My*` | Matches all symbols whose name starts with `My`. |
| `dotnet_code_quality.excluded_symbol_names = T:NS.My*` | Matches all type symbols whose name starts with `My` in the namespace `NS`. |
| `dotnet_code_quality.excluded_symbol_names = N:My*` | Matches all symbols whose containing namespace starts with `My`. |

Additionally, all the dataflow analysis based rules can be configured with a single entry `dotnet_code_quality.dataflow.excluded_symbol_names = ...`

#### Excluded type names with derived types

Option Name: `excluded_type_names_with_derived_types`

Option Values: Names of types (separated by `|`), such that the type and all its derived types are excluded for analysis.
Allowed symbol name formats:

  1. Type name only (includes all types with the name, regardless of the containing type or namespace).
  2. Type name only ending with the wildcard symbol (includes all types whose name starts with the given name, regardless of the containing type or namespace).
  3. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format) with an optional `T:` prefix.
  4. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format) with an optional `T:` prefix and ending with the wildcard symbol.

Default Value: _None_

Examples:

| Option Value | Summary |
| --- | --- |
| `dotnet_code_quality.excluded_type_names_with_derived_types = MyType` | Matches all types named `MyType` and all of its derived types in the compilation. |
| `dotnet_code_quality.excluded_type_names_with_derived_types = MyType1\|MyType2` | Matches all types named either `MyType1` or `MyType2` and all of their derived types in the compilation. |
| `dotnet_code_quality.excluded_type_names_with_derived_types = T:NS.MyType` | Matches specific type `MyType` with given fully qualified name and all of its derived types. |
| `dotnet_code_quality.excluded_type_names_with_derived_types = T:NS1.MyType1\|M:NS2.MyType2` | Matches specific types `MyType1` and `MyType2` with respective fully qualified names and all of their derived types. |

### Unsafe DllImportSearchPath bits when using DefaultDllImportSearchPaths attribute

Option Name: `unsafe_DllImportSearchPath_bits`

Configurable Rules: [CA5393](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5393)

Option Values: Integer values of `System.Runtime.InteropServices.DllImportSearchPath`

Default Value: `770` (i.e. `AssemblyDirectory | UseDllDirectoryForDependencies | ApplicationDirectory`)

Example: `dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 770`

### Exclude ASP.NET Core MVC ControllerBase when considering CSRF

Option Name: `exclude_aspnet_core_mvc_controllerbase`

Configurable Rules: [CA5391](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca5391)

Option Values: `true` or `false`

Default Value: `true`

Example: `dotnet_code_quality.CA5391.exclude_aspnet_core_mvc_controllerbase = false`

### Disallowed symbol names

Option Name: `disallowed_symbol_names`

Configurable Rules: [CA1031](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1031)

Option Values: Names of symbols (separated by `|`) that are disallowed in the context of the analysis.
Allowed symbol name formats:

  1. Symbol name only (includes all symbols with the name, regardless of the containing type or namespace).
  2. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format).
     Note that each symbol name requires a symbol kind prefix, such as `M:` prefix for methods, `T:` prefix for types, `N:` prefix for namespaces, etc.
  3. `.ctor` for constructors and `.cctor` for static constructors.

Default Value: _None_

Examples:

| Option Value | Summary |
| --- | --- |
| `dotnet_code_quality.disallowed_symbol_names = Validate` | Matches all symbols named `Validate` in the compilation. |
| `dotnet_code_quality.disallowed_symbol_names = Validate1\|Validate2` | Matches all symbols named either `Validate1` or `Validate2` in the compilation. |
| `dotnet_code_quality.disallowed_symbol_names = M:NS.MyType.Validate(ParamType)` | Matches specific method `Validate` with given fully qualified signature. |
| `dotnet_code_quality.disallowed_symbol_names = M:NS1.MyType1.Validate1(ParamType)\|M:NS2.MyType2.Validate2(ParamType)` | Matches specific methods `Validate1` and `Validate2` with respective fully qualified signature. |

### Dataflow analysis

Configurable Rules:
[CA1062](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1062),
[CA1303](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1303),
[CA1508](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1508),
[CA2000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2000),
[CA2100](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2100),
[CA2213](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2213),
Taint analysis rules

#### Interprocedural analysis Kind

Option Name: `interprocedural_analysis_kind`

Option Values:

| Option Value | Summary |
| --- | --- |
| `None` | Skip interprocedural analysis for source method invocations. |
| `NonContextSensitive` | Performs non-context sensitive interprocedural analysis for all source method invocations. |
| `ContextSensitive` | Performs context sensitive interprocedural analysis for all source method invocations. |

Default Value: _Specific to each configurable rule_

Example: `dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive`

#### Maximum method call chain length to analyze for interprocedural dataflow analysis

Option Name: `max_interprocedural_method_call_chain`

Option Values: Unsigned integer

Default Value: `3`

Example: `dotnet_code_quality.max_interprocedural_method_call_chain = 5`

#### Maximum lambda or local function call chain length to analyze for interprocedural dataflow analysis

Option Name: `max_interprocedural_lambda_or_local_function_call_chain`

Option Values: Unsigned integer

Default Value: `3`

Example: `dotnet_code_quality.max_interprocedural_lambda_or_local_function_call_chain = 5`

#### Dispose analysis kind for IDisposable rules

Option Name: `dispose_analysis_kind`

Configurable Rules: [CA2000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2000)

Option Values:

| Option Value | Summary |
| --- | --- |
| `AllPaths` | Track and report missing dispose violations on all paths (non-exception and exception paths). Additionally, also flag use of non-recommended dispose patterns that may cause potential dispose leaks. |
| `AllPathsOnlyNotDisposed` | Track and report missing dispose violations on all paths (non-exception and exception paths). Do not flag use of non-recommended dispose patterns that may cause potential dispose leaks. |
| `NonExceptionPaths` | Track and report missing dispose violations only on non-exception program paths. Additionally, also flag use of non-recommended dispose patterns that may cause potential dispose leaks. |
| `NonExceptionPathsOnlyNotDisposed` | Track and report missing dispose violations only on non-exception program paths. Do not flag use of non-recommended dispose patterns that may cause potential dispose leaks. |

Default Value: `NonExceptionPaths`

Example: `dotnet_code_quality.dispose_analysis_kind = AllPaths`

#### Configure dispose ownership transfer for arguments passed to constructor invocation

Option Name: `dispose_ownership_transfer_at_constructor`

Configurable Rules: [CA2000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2000)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.dispose_ownership_transfer_at_constructor = true`

For example, consider the below code:

```csharp
using System;

class A : IDisposable
{
    public void Dispose()
    {
    }
}

class Test
{
    DisposableOwnerType M1()
    {
        // Dispose ownership for allocation 'new A()' is assumed to be transferred to the returned 'DisposableOwnerType' instance
        // only if 'dotnet_code_quality.dispose_ownership_transfer_at_constructor = true'.
        // Otherwise, current method 'M1' has the dispose ownership for 'new A()', and it fires a CA2000 as a dispose leak for the below code.
        return new DisposableOwnerType(new A());
    }
}
```

#### Configure dispose ownership transfer for disposable objects passed as arguments to method calls

Option Name: `dispose_ownership_transfer_at_method_call`

Configurable Rules: [CA2000](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2000)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.dispose_ownership_transfer_at_method_call = false`

For example, consider the below code:

```csharp
using System;

class Test
{
    void M1()
    {
        // Dispose ownership for allocation 'new A()' is assumed to be transferred to 'TransferDisposeOwnership' method
        // if 'dotnet_code_quality.dispose_ownership_transfer_at_method_call = true'.
        // Otherwise, current method 'M1' has the dispose ownership for 'new A()', and it fires a CA2000 as a dispose leak for the below code.
        TransferDisposeOwnership(new A());
    }
}
```

#### Points to analysis kind for DFA rules based on PointsToAnalysis

Option Name: `points_to_analysis_kind`

Configurable Rules: All DFA rules

Option Values:

| Option Value | Summary |
| --- | --- |
| `None` | PointsToAnalysis is disabled. |
| `PartialWithoutTrackingFieldsAndProperties` | Partial analysis that does not track PointsToData for fields and properties for improved performance. |
| `Complete` | Complete analysis that also tracks PointsToData for fields and properties. |

Default Value:  _Specific to each configurable rule_

Example: `dotnet_code_quality.points_to_analysis_kind = Complete`

#### Configure execution of Copy analysis (tracks value and reference copies)

Option Name: `copy_analysis`

Option Values: `true` or `false`

Default Value:  _Specific to each configurable rule_ (`true` for most rules)

Example: `dotnet_code_quality.copy_analysis = true`

#### Configure sufficient IterationCount when using weak KDF algorithm

Option Name: `sufficient_IterationCount_for_weak_KDF_algorithm`

Option Values: integral values

Default Value:  _Specific to each configurable rule_ (`100000` for most rules)

Example: `dotnet_code_quality.CA5387.sufficient_IterationCount_for_weak_KDF_algorithm = 100000`

### Do not prefix enum values with type name

Option Name: `enum_values_prefix_trigger`

Configurable Rules: [CA1712](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1712)

Option Values:

| Option Value | Summary |
| --- | --- |
| `AnyEnumValue` | The rule will be triggered if _any_ of the enum values starts with the enum type name. |
| `AllEnumValues` | The rule will be triggered if _all_ of the enum values start with the enum type name. |
| `Heuristic` | The rule will be triggered using the default FxCop heuristic (i.e. when at least 75% of the enum values start with the enum type name). |

Default Value: `Heuristic`.

Example: `dotnet_code_quality.CA1712.enum_values_prefix_trigger = AnyEnumValue`

### Exclude indirect base types

Option Name: `exclude_indirect_base_types`

Configurable Rules: [CA1710](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1710)

Option Values: `true` or `false`

Default Value: `true`

Example: `dotnet_code_quality.CA1710.exclude_indirect_base_types = true`

For example, consider the code below:

```csharp
// An issue is always raised on this type because the suffix should be 'Exception'.
public class MyBaseClass : Exception, IEnumerable
{
   // code omitted for simplicity
}

// If the option is enabled no issue is raised on 'MyClass'; otherwise an issue will
// suggest to add the 'Exception' suffix.
public class MyClass : MyBaseClass
{
   // code omitted for simplicity
}
```

### Additional required suffixes

Option Name: `additional_required_suffixes`

Configurable Rules: [CA1710](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1710)

Option Values: List (separated by `|`) of type names with their required suffix (separated by `->`).
Allowed type name formats:

  1. Type name only (includes all types with the name, regardless of the containing type or namespace).
  2. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format) with an optional `T:` prefix.

Default Value: _None_

Examples:

| Option Value | Summary |
| --- | --- |
| `dotnet_code_quality.CA1710.additional_required_suffixes = MyClass->Class` | All types inheriting from `MyClass` are expected to have the `Class` suffix. |
| `dotnet_code_quality.CA1710.additional_required_suffixes = MyClass->Class\|MyNamespace.IPath->Path` | All types inheriting from `MyClass` are expected to have the `Class` suffix AND all types implementing `MyNamespace.IPath` are expected to have the `Path` suffix. |
| `dotnet_code_quality.CA1710.additional_required_suffixes = T:System.Data.IDataReader->{}` | Allows to override built-in suffixes, in this case, all types implementing `IDataReader` are no longer expected to end in `Collection`. |

### Additional required generic interfaces

Option Name: `additional_required_generic_interfaces`

Configurable Rules: [CA1010](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1010)

Option Values: List (separated by `|`) of interface names with their required generic fully qualified interface (separated by `->`).
Allowed interface formats:

  1. Interface name only (includes all interfaces with the name, regardless of the containing type or namespace).
  2. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format) with an optional `T:` prefix.

Default Value: _None_

Examples:

| Option Value | Summary |
| --- | --- |
| ``dotnet_code_quality.CA1010.additional_required_generic_interfaces = ISomething->System.Collections.Generic.IEnumerable`1`` | All types implementing `ISomething` regardless of its namespace are expected to also implement ``System.Collections.Generic.IEnumerable\`1``. |
| ``dotnet_code_quality.CA1010.additional_required_generic_interfaces = T:System.Collections.IDictionary->T:System.Collections.Generic.IDictionary`2`` | All types implementing `System.Collections.IDictionary` are expected to also implement ``System.Collections.Generic.IDictionary`2``. |

### Inheritance excluded type or namespace names

Option Name: `additional_inheritance_excluded_symbol_names`

Configurable Rules: [CA1501](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1501)

Option Values: Names of types or namespaces (separated by `|`), such that the type or type's namespace does not count in the inheritance hierarchy tree.
Allowed symbol name formats:

  1. Type or namespace name (includes all types with the name, regardless of the containing type or namespace and all types whose namespace contains the name).
  2. Type or namespace name ending with a wildcard symbol (includes all types whose name starts with the given name, regardless of the containing type or namespace and all types whose namespace contains the name).
  3. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format) with an optional `T:` prefix for types or `N:` prefix for namespaces.
  4. Fully qualified type or namespace name with an optional `T:` prefix for type or `N:` prefix for namespace and ending with the wildcard symbol (includes all types whose fully qualified name starts with the given suffix).

Default Value: `N:System.*` (note that this value is always automatically added to the value provided)

Examples:

| Option Value | Summary |
| --- | --- |
| `dotnet_code_quality.CA1501.additional_inheritance_excluded_symbol_names = MyType` | Matches all types named `MyType` or whose containing namespace contains `MyType` and all types from the `System` namespace. |
| `dotnet_code_quality.CA1501.additional_inheritance_excluded_symbol_names = MyType1\|MyType2` | Matches all types named either `MyType1` or `MyType2` or whose containing namespace contains either `MyType1` or `MyType2` and all types from the `System` namespace. |
| `dotnet_code_quality.CA1501.additional_inheritance_excluded_symbol_names = T:NS.MyType` | Matches specific type `MyType` in the namespace `NS` and all types from the `System` namespace. |
| `dotnet_code_quality.CA1501.additional_inheritance_excluded_symbol_names = T:NS1.MyType1\|T:NS2.MyType2` | Matches specific types `MyType1` and `MyType2` with respective fully qualified names and all types from the `System` namespace. |
| `dotnet_code_quality.CA1501.additional_inheritance_excluded_symbol_names = N:NS` | Matches all types from the `NS` namespace and all types from the `System` namespace. |
| `dotnet_code_quality.CA1501.additional_inheritance_excluded_symbol_names = My*` | Matches all types whose name starts with `My` or whose containing namespace parts starts with `My` and all types from the `System` namespace. |
| `dotnet_code_quality.CA1501.additional_inheritance_excluded_symbol_names = T:NS.My*` | Matches all types whose name starts with `My` in the namespace `NS` and all types from the `System` namespace. |
| `dotnet_code_quality.CA1501.additional_inheritance_excluded_symbol_names = N:My*` | Matches all types whose containing namespace starts with `My` and all types from the `System` namespace. |

### Analyzed symbol kinds

Option Name: `analyzed_symbol_kinds`

Configurable Rules: [CA1716](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1716)

Option Values: One or more fields of enum [Microsoft.CodeAnalysis.SymbolKind](https://roslynsourceindex.azurewebsites.net/#Microsoft.CodeAnalysis/Symbols/SymbolKind.cs,30fd9c0834bef6ff) as a comma separated list.

Default Value: `Namespace, NamedType, Method, Property, Event, Parameter`

Example: `dotnet_code_quality.CA1716.analyzed_symbol_kinds = Namespace, Property`

### Use naming heuristic

Option Name: `use_naming_heuristic`

Configurable Rules: [CA1303](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1303)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA1303.use_naming_heuristic = true`

### Additional use results methods

Option Name: `additional_use_results_methods`

Configurable Rules: [CA1806](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1806)

Option Values: Names of additional methods (separated by `|`).
Allowed method name formats:

  1. Method name only (includes all methods with the name, regardless of the containing type or namespace).
  2. Fully qualified names in the [symbol's documentation ID format](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format)
     with an optional `M:` prefix.

Default Value: _None_

Examples:

| Option Value | Summary |
| --- | --- |
| `dotnet_code_quality.CA1806.additional_use_results_methods = MyMethod` | Matches all methods named `MyMethod` in the compilation. |
| `dotnet_code_quality.CA1806.additional_use_results_methods = MyMethod1\|MyMethod2` | Matches all methods named either `MyMethod1` or `MyMethod2` in the compilation. |
| `dotnet_code_quality.CA1806.additional_use_results_methods = M:NS.MyType.MyMethod(ParamType)` | Matches specific method `MyMethod` with given fully qualified signature. |
| `dotnet_code_quality.CA1806.additional_use_results_methods = M:NS1.MyType1.MyMethod1(ParamType)\|M:NS2.MyType2.MyMethod2(ParamType)` | Matches specific methods `MyMethod1` and `MyMethod2` with respective fully qualified signature. |

### Allowed suffixes

Option Name: `allowed_suffixes`

Configurable Rules: [CA1711](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1711)

Option Values: List (separated by `|`) of allowed suffixes

Example: `dotnet_code_quality.CA1711.allowed_suffixes = Flag|Flags`

### Enable platform compatibility analyzer for TFMs <= net5.0

Option Name: `enable_platform_analyzer_on_pre_net5_target`

Configurable Rules: [CA1416](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1416)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.enable_platform_analyzer_on_pre_net5_target = true` or `dotnet_code_quality.CA1416.enable_platform_analyzer_on_pre_net5_target = true`

### Exclude structs

Option Name: `exclude_structs`

Configurable Rules: [CA1051](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1051)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA1051.exclude_structs = true`

### Exclude FirstOrDefault and LastOrDefault methods

Option Name: `exclude_ordefault_methods`

Configurable Rules: [CA1826](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1826)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA1826.exclude_ordefault_methods = true`

### Additional enum `None` names

Option Name: `additional_enum_none_names`

Configurable Rules: [CA1008](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/CA1008)

Option Values: Names of additional enum None names (separated by `|`).

Default Value: empty

Example: `dotnet_code_quality.CA1008.additional_enum_none_names = Never` or `dotnet_code_quality.CA1008.additional_enum_none_names = Never|Nothing`

### Enumeration methods

Option Name: `enumeration_methods`

Configurable Rules: [CA1851](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/CA1851)

Option Values: [Fully qualified names](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format) of additional methods enumerating all parameters with IEnumerable type (separated by `|`).

Default Value: empty

Example: `dotnet_code_quality.CA1851.enumeration_methods = M:NS.Cls.SomeMethod(System.Collections.Generic.IEnumerable{System.Int32})` or `dotnet_code_quality.CA1851.enumeration_methods = M:NS.Cls.SomeMethod*` or `M:NS.Cls.SomeMethod``1(System.Collections.Generic.IEnumerable{System.Int32}) | M:NS.Cls.OtherMethod*`

### Linq Chain methods

Option Name: `linq_chain_methods`

Configurable Rules: [CA1851](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/CA1851)

Option Values: [Fully qualified names](https://github.com/dotnet/csharplang/blob/main/spec/documentation-comments.md#id-string-format) of additional methods accepting IEnumerable parameters and return a new IEnumerable type instance(separated by `|`). By default, the IEnumerable type parameters of the Linq Chain method are considered not enumerated. This behavior could be overridden by combining using the `enumeration_methods` option.

Default Value: empty

This option is used to include customized methods like [Select](https://learn.microsoft.com/dotnet/api/system.linq.enumerable.select?view=net-6.0) into the analysis scope.
Consider the example:

```csharp
public static MyClass
{
  public static IEnumerable<int> MyNoEnumerationIncrementSelect(this IEnumerable<int> source)
  {
      return source.Select(i => i + 1);
  } 
}
```

In this example the options could be: `dotnet_code_quality.CA1851.linq_chain_methods = M:MyClass.MyNoEnumerationIncrementSelect*`.
If the method enumerates the parameter, this option can be used with `enumeration_methods` for example:

```csharp
public static MyClass
{
  public static IEnumerable<int> MyEnumerationLinqChain(this IEnumerable<int> source)
  {
      return source.ElementAt(10).Concat(source);
  } 
}
```

In this example the options would be `dotnet_code_quality.CA1851.linq_chain_methods = M:MyClass.MyEnumerationLinqChain*` and `dotnet_code_quality.CA1851.enumeration_methods = M:MyClass.MyEnumerationLinqChain*`.

### Assume method enumerates parameters

Option Name: `assume_method_enumerates_parameters`

Configurable Rules: [CA1851](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/CA1851)

Option Values: `true` or `false`
If set to true, all IEnumerable type parameters would be assumed enumerated by the method invocation. This option does not affect methods specified in `linq_chain_methods`.

Default Value: `false`

Example: `dotnet_code_quality.CA1851.assume_method_enumerates_parameters = true`

### Proceed with analysis even if InternalsVisibleTo is present

Option Name: `ignore_internalsvisibleto`

Configurable Rules: [CA1812](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/CA1812), [CA1852](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/CA1852)

Option Values: `true` or `false`
Certain diagnostics are normally disabled if the assembly being analyzed used the InternalsVisibleTo attribute to
expose the assembly's internal symbols. This option lets you override this behavior and proceed with analysis
regardless of whether the attribute is present.

Default Value: `false`

Example: `dotnet_code_quality.CA1852.ignore_internalsvisibleto = false`
