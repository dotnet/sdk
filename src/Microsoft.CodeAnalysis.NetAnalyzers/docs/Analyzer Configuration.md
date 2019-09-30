# Analyzer Configuration

Starting with version `2.6.3`, all the analyzer NuGet packages produced in this repo, including the FxCop Analyzers NuGet package, support _.editorconfig based analyzer configuration_. End users can configure the behavior of specific CA rule(s) OR all configurable CA rules by specifying supported key-value pair options in an `.editorconfig` file. You can read more about `.editorconfig` format [here](https://editorconfig.org/).

## .editorconfig format
Analyzer configuration options from an .editorconfig file are parsed into _general_ and _specific_ configuration options. General configuration enables configuring the behavior of all CA rules for which the provided option is valid. Specific configuration enables configuring each CA rule ID or CA rules belonging to each rule category, such as 'Naming', 'Design', 'Performance', etc. or CA rules with a specific custom tag, such as 'Dataflow'. Our options are _case-insensitive_. Below are the supported formats:
   1. General configuration option:
      1. `dotnet_code_quality.OptionName = OptionValue`
   2. Specific configuration option:
      1. `dotnet_code_quality.RuleId.OptionName = OptionValue`
      2. `dotnet_code_quality.RuleCategory.OptionName = OptionValue`
      2. `dotnet_code_quality.RuleCustomTag.OptionName = OptionValue`

For example, end users can configure the analyzed API surface for analyzers using the below `api_surface` option specification:
   1. General configuration option:
      1. `dotnet_code_quality.api_surface = public`
   2. Specific configuration option:
      1. `dotnet_code_quality.CA1040.api_surface = public`
      2. `dotnet_code_quality.Naming.api_surface = public`
      3. `dotnet_code_quality.Dataflow.api_surface = public`

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

Configurable Rules: [CA1000](https://docs.microsoft.com/visualstudio/code-quality/ca1000-do-not-declare-static-members-on-generic-types), [CA1003](https://docs.microsoft.com/visualstudio/code-quality/ca1003-use-generic-event-handler-instances), [CA1008](https://docs.microsoft.com/visualstudio/code-quality/ca1008-enums-should-have-zero-value), [CA1010](https://docs.microsoft.com/visualstudio/code-quality/ca1010-collections-should-implement-generic-interface), [CA1012](https://docs.microsoft.com/visualstudio/code-quality/ca1012-abstract-types-should-not-have-constructors), [CA1024](https://docs.microsoft.com/visualstudio/code-quality/ca1024-use-properties-where-appropriate), [CA1027](https://docs.microsoft.com/visualstudio/code-quality/ca1027-mark-enums-with-flagsattribute), [CA1028](https://docs.microsoft.com/visualstudio/code-quality/ca1028-enum-storage-should-be-int32), [CA1030](https://docs.microsoft.com/visualstudio/code-quality/ca1030-use-events-where-appropriate), [CA1036](https://docs.microsoft.com/visualstudio/code-quality/ca1036-override-methods-on-comparable-types), [CA1040](https://docs.microsoft.com/visualstudio/code-quality/ca1040-avoid-empty-interfaces), [CA1041](https://docs.microsoft.com/visualstudio/code-quality/ca1041-provide-obsoleteattribute-message), [CA1043](https://docs.microsoft.com/visualstudio/code-quality/ca1043-use-integral-or-string-argument-for-indexers), [CA1044](https://docs.microsoft.com/visualstudio/code-quality/ca1044-properties-should-not-be-write-only), [CA1051](https://docs.microsoft.com/visualstudio/code-quality/ca1051-do-not-declare-visible-instance-fields), [CA1052](https://docs.microsoft.com/visualstudio/code-quality/ca1052-static-holder-types-should-be-sealed), [CA1054](https://docs.microsoft.com/visualstudio/code-quality/ca1054-uri-parameters-should-not-be-strings), [CA1055](https://docs.microsoft.com/visualstudio/code-quality/ca1055-uri-return-values-should-not-be-strings), [CA1056](https://docs.microsoft.com/visualstudio/code-quality/ca1056-uri-properties-should-not-be-strings), [CA1058](https://docs.microsoft.com/visualstudio/code-quality/ca1058-types-should-not-extend-certain-base-types), [CA1063](https://docs.microsoft.com/visualstudio/code-quality/ca1063-implement-idisposable-correctly), [CA1708](https://docs.microsoft.com/visualstudio/code-quality/ca1708-identifiers-should-differ-by-more-than-case), [CA1710](https://docs.microsoft.com/visualstudio/code-quality/ca1710-identifiers-should-have-correct-suffix), [CA1711](https://docs.microsoft.com/visualstudio/code-quality/ca1711-identifiers-should-not-have-incorrect-suffix), [CA1714](https://docs.microsoft.com/visualstudio/code-quality/ca1714-flags-enums-should-have-plural-names), [CA1715](https://docs.microsoft.com/visualstudio/code-quality/ca1715-identifiers-should-have-correct-prefix), [CA1716](https://docs.microsoft.com/visualstudio/code-quality/ca1716-identifiers-should-not-match-keywords), [CA1717](https://docs.microsoft.com/visualstudio/code-quality/ca1717-only-flagsattribute-enums-should-have-plural-names), [CA1720](https://docs.microsoft.com/visualstudio/code-quality/ca1720-identifiers-should-not-contain-type-names), [CA1721](https://docs.microsoft.com/visualstudio/code-quality/ca1721-property-names-should-not-match-get-methods), [CA1725](https://docs.microsoft.com/visualstudio/code-quality/ca1725-parameter-names-should-match-base-declaration), [CA1801](https://docs.microsoft.com/visualstudio/code-quality/ca1801-review-unused-parameters), [CA1802](https://docs.microsoft.com/visualstudio/code-quality/ca1802-use-literals-where-appropriate), [CA1815](https://docs.microsoft.com/visualstudio/code-quality/ca1815-override-equals-and-operator-equals-on-value-types), [CA1819](https://docs.microsoft.com/visualstudio/code-quality/ca1819-properties-should-not-return-arrays), [CA2217](https://docs.microsoft.com/visualstudio/code-quality/ca2217-do-not-mark-enums-with-flagsattribute), [CA2225](https://docs.microsoft.com/visualstudio/code-quality/ca2225-operator-overloads-have-named-alternates), [CA2226](https://docs.microsoft.com/visualstudio/code-quality/ca2226-operators-should-have-symmetrical-overloads), [CA2231](https://docs.microsoft.com/visualstudio/code-quality/ca2231-overload-operator-equals-on-overriding-valuetype-equals), [CA2234](https://docs.microsoft.com/visualstudio/code-quality/ca2234-pass-system-uri-objects-instead-of-strings)

Option Values:

| Option Value | Summary |
| --- | --- |
| `public` | Analyzes public APIs that are externally visible outside the assembly. |
| `internal` or `friend` | Analyzes internal APIs that are visible within the assembly and to assemblies with [InternalsVisibleToAttribute](https://docs.microsoft.com/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute) access. |
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

### Required modifiers for analyzed APIs
Option Name: `required_modifiers`

Configurable Rules: [CA1802](https://docs.microsoft.com/visualstudio/code-quality/ca1802-use-literals-where-appropriate)

Option Values: Comma separated listed of one or more modifier values from the below table. Note that not all values are applicable for every configurable rule.

| Option Value | Summary |
| --- | --- |
| `none` | No modifier requirement. |
| `static` or `Shared` | Must be declared as 'static' ('Shared' in Visual Basic). |
| `const` | Must be declared as 'const'. |
| `readonly` | Must be declared as 'readonly'. |
| `abstract` | Must be declared as 'abstract'. |
| `virtual` | Must be declared as 'virtual'. |
| `override` | Must be declared as 'override'. |
| `sealed` | Must be declared as 'sealed'. |
| `extern` | Must be declared as 'extern'. |
| `async` | Must be declared as 'async'. |

Default Value: Depends on each configurable rule:
   1. CA1802: default value is 'static'. Set the value to 'none' to allow flagging instance fields.

Example: `dotnet_code_quality.CA1802.required_modifiers = none`.

### Async void methods
Option Name: `exclude_async_void_methods`

Configurable Rules: [CA2007](../src/Microsoft.CodeQuality.Analyzers/Microsoft.CodeQuality.Analyzers.md#ca2007-do-not-directly-await-a-task)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA2007.exclude_async_void_methods = true`

### Single letter type parameters
Option Name: `exclude_single_letter_type_parameters`

Configurable Rules: [CA1715](https://docs.microsoft.com/visualstudio/code-quality/ca1715-identifiers-should-have-correct-prefix)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA1715.exclude_single_letter_type_parameters = true`

### Exclude extension method 'this' parameter
Option Name: `exclude_extension_method_this_parameter`

Configurable Rules: [CA1062](https://docs.microsoft.com/visualstudio/code-quality/ca1062-validate-arguments-of-public-methods)

Option Values: `true` or `false`

Default Value: `false`

Example: `dotnet_code_quality.CA1062.exclude_extension_method_this_parameter = true`

### Null check validation methods
Option Name: `null_check_validation_methods`

Configurable Rules: [CA1062](https://docs.microsoft.com/visualstudio/code-quality/ca1062-validate-arguments-of-public-methods)

Option Values: Names of null check validation methods (separated by '|') that validate arguments passed to the method are non-null for CA1062 (https://docs.microsoft.com/visualstudio/code-quality/ca1062-validate-arguments-of-public-methods).
Allowed method name formats:
  1. Method name only (includes all methods with the name, regardless of the containing type or namespace)
  2. Fully qualified names in the symbol's documentation ID format: https://github.com/dotnet/csharplang/blob/master/spec/documentation-comments.md#id-string-format
     with an optional "M:" prefix.

Default Value: None

Examples:

| Option Value | Summary |
| --- | --- |
|`dotnet_code_quality.null_check_validation_methods = Validate` | Matches all methods named 'Validate' in the compilation
|`dotnet_code_quality.null_check_validation_methods = Validate1\|Validate2` | Matches all methods named either 'Validate1' or 'Validate2' in the compilation
|`dotnet_code_quality.null_check_validation_methods = NS.MyType.Validate(ParamType)` | Matches specific method 'Validate' with given fully qualified signature
|`dotnet_code_quality.null_check_validation_methods = NS1.MyType1.Validate1(ParamType)\|NS2.MyType2.Validate2(ParamType)` | Matches specific methods 'Validate1' and 'Validate2' with respective fully qualified signature
 
### Additional string formatting methods
Option Name: `additional_string_formatting_methods`

Configurable Rules: [CA2241](https://docs.microsoft.com/visualstudio/code-quality/ca2241-provide-correct-arguments-to-formatting-methods)

Option Values: Names of additional string formatting methods (separated by '|') for CA2241.
Allowed method name formats:
  1. Method name only (includes all methods with the name, regardless of the containing type or namespace)
  2. Fully qualified names in the symbol's documentation ID format: https://github.com/dotnet/csharplang/blob/master/spec/documentation-comments.md#id-string-format
     with an optional "M:" prefix.

Default Value: None

Examples:

| Option Value | Summary |
| --- | --- |
|`dotnet_code_quality.additional_string_formatting_methods = MyFormat` | Matches all methods named 'MyFormat' in the compilation
|`dotnet_code_quality.additional_string_formatting_methods = MyFormat1\|MyFormat2` | Matches all methods named either 'MyFormat1' or 'MyFormat2' in the compilation
|`dotnet_code_quality.additional_string_formatting_methods = NS.MyType.MyFormat(ParamType)` | Matches specific method 'MyFormat' with given fully qualified signature
|`dotnet_code_quality.additional_string_formatting_methods = NS1.MyType1.MyFormat1(ParamType)\|NS2.MyType2.MyFormat2(ParamType)` | Matches specific methods 'MyFormat1' and 'MyFormat2' with respective fully qualified signature
 
### Excluded symbol names
Option Name: `excluded_symbol_names`

Configurable Rules: [CA1303](https://docs.microsoft.com/visualstudio/code-quality/ca1303-do-not-pass-literals-as-localized-parameters), [CA1062](https://docs.microsoft.com/visualstudio/code-quality/ca1062-validate-arguments-of-public-methods), CA1508, [CA2000](https://docs.microsoft.com/visualstudio/code-quality/ca2000-dispose-objects-before-losing-scope), [CA2100](https://docs.microsoft.com/visualstudio/code-quality/ca2100-review-sql-queries-for-security-vulnerabilities), [CA2301](https://docs.microsoft.com/visualstudio/code-quality/ca2301-do-not-call-binaryformatter-deserialize-without-first-setting-binaryformatter-binder), [CA2302](https://docs.microsoft.com/visualstudio/code-quality/ca2302-ensure-binaryformatter-binder-is-set-before-calling-binaryformatter-deserialize), [CA2311](https://docs.microsoft.com/visualstudio/code-quality/ca2311-do-not-deserialize-without-first-setting-netdatacontractserializer-binder), [CA2312](https://docs.microsoft.com/visualstudio/code-quality/ca2312-ensure-netdatacontractserializer-binder-is-set-before-deserializing), [CA2321](https://docs.microsoft.com/visualstudio/code-quality/ca2321), [CA2322](https://docs.microsoft.com/visualstudio/code-quality/ca2322), CA2327, CA2328, CA2329, CA2330, [CA3001](https://docs.microsoft.com/visualstudio/code-quality/ca3001-review-code-for-sql-injection-vulnerabilities), [CA3002](https://docs.microsoft.com/visualstudio/code-quality/ca3002-review-code-for-xss-vulnerabilities), [CA3003](https://docs.microsoft.com/visualstudio/code-quality/ca3003-review-code-for-file-path-injection-vulnerabilities), [CA3004](https://docs.microsoft.com/visualstudio/code-quality/ca3004-review-code-for-information-disclosure-vulnerabilities), [CA3005](https://docs.microsoft.com/visualstudio/code-quality/ca3005-review-code-for-ldap-injection-vulnerabilities), [CA3006](https://docs.microsoft.com/visualstudio/code-quality/ca3006-review-code-for-process-command-injection-vulnerabilities), [CA3007](https://docs.microsoft.com/visualstudio/code-quality/ca3007-review-code-for-open-redirect-vulnerabilities), [CA3008](https://docs.microsoft.com/visualstudio/code-quality/ca3008-review-code-for-xpath-injection-vulnerabilities), [CA3009](https://docs.microsoft.com/visualstudio/code-quality/ca3009-review-code-for-xml-injection-vulnerabilities), [CA3010](https://docs.microsoft.com/visualstudio/code-quality/ca3010-review-code-for-xaml-injection-vulnerabilities), [CA3011](https://docs.microsoft.com/visualstudio/code-quality/ca3011-review-code-for-dll-injection-vulnerabilities), [CA3012](https://docs.microsoft.com/visualstudio/code-quality/ca3012-review-code-for-regex-injection-vulnerabilities), CA5361, CA5376, CA5377, CA5378, CA5380, CA5381, CA5382, CA5383, CA5384, CA5387, CA5388, CA5389, CA5390

Option Values: Names of symbols (separated by '|') that are excluded for analysis.
Allowed symbol name formats:
  1. Symbol name only (includes all symbols with the name, regardless of the containing type or namespace)
  2. Fully qualified names in the symbol's documentation ID format: https://github.com/dotnet/csharplang/blob/master/spec/documentation-comments.md#id-string-format.
     Note that each symbol name requires a symbol kind prefix, such as "M:" prefix for methods, "T:" prefix for types, "N:" prefix for namespaces, etc.
  3. `.ctor` for constructors and `.cctor` for static constructors

Default Value: None

Examples:

| Option Value | Summary |
| --- | --- |
|`dotnet_code_quality.excluded_symbol_names = Validate` | Matches all symbols named 'Validate' in the compilation
|`dotnet_code_quality.excluded_symbol_names = Validate1\|Validate2` | Matches all symbols named either 'Validate1' or 'Validate2' in the compilation
|`dotnet_code_quality.excluded_symbol_names = M:NS.MyType.Validate(ParamType)` | Matches specific method 'Validate' with given fully qualified signature
|`dotnet_code_quality.excluded_symbol_names = M:NS1.MyType1.Validate1(ParamType)\|M:NS2.MyType2.Validate2(ParamType)` | Matches specific methods 'Validate1' and 'Validate2' with respective fully qualified signature

Additionally, all the dataflow analysis based rules can be configured with a single entry `dotnet_code_quality.dataflow.excluded_symbol_names = ...`

### Excluded type names with derived types
Option Name: `excluded_type_names_with_derived_types`

Configurable Rules: [CA1303](https://docs.microsoft.com/visualstudio/code-quality/ca1303-do-not-pass-literals-as-localized-parameters)

Option Values: Names of types (separated by '|'), such that the type and all its derived types are excluded for analysis.
Allowed symbol name formats:
  1. Type name only (includes all types with the name, regardless of the containing type or namespace)
  2. Fully qualified names in the symbol's documentation ID format: https://github.com/dotnet/csharplang/blob/master/spec/documentation-comments.md#id-string-format with an optional "T:" prefix.

Default Value: None

Examples:

| Option Value | Summary |
| --- | --- |
|`dotnet_code_quality.excluded_type_names_with_derived_types = MyType` | Matches all types named 'MyType' and all of its derived types in the compilation
|`dotnet_code_quality.excluded_type_names_with_derived_types = MyType1\|MyType2` | Matches all types named either 'MyType1' or 'MyType2' and all of their derived types in the compilation
|`dotnet_code_quality.excluded_type_names_with_derived_types = M:NS.MyType` | Matches specific type 'MyType' with given fully qualified name and all of its derived types
|`dotnet_code_quality.excluded_type_names_with_derived_types = M:NS1.MyType1\|M:NS2.MyType2` | Matches specific types 'MyType1' and 'MyType2' with respective fully qualified names and all of their derived types

### Unsafe DllImportSearchPath bits when using DefaultDllImportSearchPaths attribute
Option Name: `unsafe_DllImportSearchPath_bits`

Configurable Rules: CA5393

Option Values: Integer values of System.Runtime.InteropServices.DllImportSearchPath

Default Value: '770', which is AssemblyDirectory | UseDllDirectoryForDependencies | ApplicationDirectory

Example: `dotnet_code_quality.CA5393.unsafe_DllImportSearchPath_bits = 770`

### Exclude ASP.NET Core MVC ControllerBase when considering CSRF
Option Name: `exclude_aspnet_core_mvc_controllerbase`

Configurable Rules: CA5391

Option Values: Boolean values

Default Value: `true`

Example: `dotnet_code_quality.CA5391.exclude_aspnet_core_mvc_controllerbase = false`
 
### Disallowed symbol names
Option Name: `disallowed_symbol_names`

Configurable Rules: [CA1031](https://docs.microsoft.com/visualstudio/code-quality/ca1031-do-not-catch-general-exception-types)

Option Values: Names of symbols (separated by '|') that are disallowed in the context of the analysis.
Allowed symbol name formats:
  1. Symbol name only (includes all symbols with the name, regardless of the containing type or namespace)
  2. Fully qualified names in the symbol's documentation ID format: https://github.com/dotnet/csharplang/blob/master/spec/documentation-comments.md#id-string-format.
     Note that each symbol name requires a symbol kind prefix, such as "M:" prefix for methods, "T:" prefix for types, "N:" prefix for namespaces, etc.
  3. `.ctor` for constructors and `.cctor` for static constructors

Default Value: None

Examples:

| Option Value | Summary |
| --- | --- |
|`dotnet_code_quality.disallowed_symbol_names = Validate` | Matches all symbols named 'Validate' in the compilation
|`dotnet_code_quality.disallowed_symbol_names = Validate1\|Validate2` | Matches all symbols named either 'Validate1' or 'Validate2' in the compilation
|`dotnet_code_quality.disallowed_symbol_names = M:NS.MyType.Validate(ParamType)` | Matches specific method 'Validate' with given fully qualified signature
|`dotnet_code_quality.disallowed_symbol_names = M:NS1.MyType1.Validate1(ParamType)\|M:NS2.MyType2.Validate2(ParamType)` | Matches specific methods 'Validate1' and 'Validate2' with respective fully qualified signature

### Dataflow analysis

Configurable Rules: [CA1062](https://docs.microsoft.com/visualstudio/code-quality/ca1062-validate-arguments-of-public-methods), [CA1303](https://docs.microsoft.com/visualstudio/code-quality/ca1303-do-not-pass-literals-as-localized-parameters), [CA1508](../src/Microsoft.CodeQuality.Analyzers/Microsoft.CodeQuality.Analyzers.md#ca1508-avoid-dead-conditional-code), [CA2000](https://docs.microsoft.com/visualstudio/code-quality/ca2000-dispose-objects-before-losing-scope), [CA2100](https://docs.microsoft.com/visualstudio/code-quality/ca2100-review-sql-queries-for-security-vulnerabilities), [CA2213](https://docs.microsoft.com/visualstudio/code-quality/ca2213-disposable-fields-should-be-disposed), Taint analysis rules

#### Interprocedural analysis Kind
Option Name: `interprocedural_analysis_kind`

Option Values:

| Option Value | Summary |
| --- | --- |
| `None` | Skip interprocedural analysis for source method invocations. |
| `NonContextSensitive` | Performs non-context sensitive interprocedural analysis for all source method invocations. |
| `ContextSensitive` | Performs context sensitive interprocedural analysis for all source method invocations. |

Default Value: Specific to each configurable rule.

Example: `dotnet_code_quality.interprocedural_analysis_kind = ContextSensitive`

#### Maximum method call chain length to analyze for interprocedural dataflow analysis
Option Name: `max_interprocedural_method_call_chain`

Option Values: Unsigned integer

Default Value: 3

Example: `dotnet_code_quality.max_interprocedural_method_call_chain = 5`

#### Maximum lambda or local function call chain length to analyze for interprocedural dataflow analysis
Option Name: `max_interprocedural_lambda_or_local_function_call_chain`

Option Values: Unsigned integer

Default Value: 10

Example: `dotnet_code_quality.max_interprocedural_lambda_or_local_function_call_chain = 5`

#### Dispose analysis kind for IDisposable rules
Option Name: `dispose_analysis_kind`

Configurable Rules: [CA2000](https://docs.microsoft.com/visualstudio/code-quality/ca2000-dispose-objects-before-losing-scope)

Option Values:

| Option Value | Summary |
| --- | --- |
| `AllPaths` | Track and report missing dispose violations on all paths (non-exception and exception paths). Additionally, also flag use of non-recommended dispose patterns that may cause potential dispose leaks. |
| `AllPathsOnlyNotDisposed` | Track and report missing dispose violations on all paths (non-exception and exception paths). Do not flag use of non-recommended dispose patterns that may cause potential dispose leaks. |
| `NonExceptionPaths` | Track and report missing dispose violations only on non-exception program paths. Additionally, also flag use of non-recommended dispose patterns that may cause potential dispose leaks. |
| `NonExceptionPathsOnlyNotDisposed` | Track and report missing dispose violations only on non-exception program paths. Do not flag use of non-recommended dispose patterns that may cause potential dispose leaks. |

Default Value: `NonExceptionPaths`.

Example: `dotnet_code_quality.dispose_analysis_kind = AllPaths`

#### Configure dispose ownership transfer for arguments passed to constructor invocation
Option Name: `dispose_ownership_transfer_at_constructor`

Configurable Rules: [CA2000](https://docs.microsoft.com/visualstudio/code-quality/ca2000-dispose-objects-before-losing-scope)

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

Configurable Rules: [CA2000](https://docs.microsoft.com/visualstudio/code-quality/ca2000-dispose-objects-before-losing-scope)

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

#### Configure execution of Copy analysis (tracks value and reference copies)
Option Name: `copy_analysis`

Option Values: `true` or `false`

Default Value: Specific to each configurable rule ('true' by default for most rules)

Example: `dotnet_code_quality.copy_analysis = true`

#### Configure sufficient IterationCount when using weak KDF algorithm
Option Name: `sufficient_IterationCount_for_weak_KDF_algorithm`

Option Values: integral values

Default Value: Specific to each configurable rule ('100000' by default for most rules)

Example: `dotnet_code_quality.CA5387.sufficient_IterationCount_for_weak_KDF_algorithm = 100000`
