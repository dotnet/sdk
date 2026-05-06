# 3rd Party Analyzers

## How to add analyzers to a project

3rd party analyzers are discovered from the `<PackageReferences>` specified in the workspace project files.

*Example:*

Add the StyleCop analyzer package to a simple console project file.

```diff
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp3.1</TargetFramework>
  </PropertyGroup>

+ <ItemGroup>
+    <PackageReference Include="StyleCop.Analyzers" Version="1.1.118" />
+  </ItemGroup>

</Project>
```

## How to configure analyzer severity

The options specified in .editorconfig files are recognized by the pattern `dotnet_diagnostic.<diagnostic-id>.severity = <value>`. `<diagnostic-id>` represents the diagnostic ID matched by the compiler, case-insensitively, to be configured. `<value>` must be one of the following: error, warn, info, hidden, suppress. Please read the [Code Analysis documentation](https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-options#severity-level) for more details.

*Example:*

Configure the StyleCop analyzer so that empty comments are errors.

```ini
[*.{cs,vb}]

# The C# comment does not contain any comment text.
dotnet_diagnostic.SA1120.severity = error
```