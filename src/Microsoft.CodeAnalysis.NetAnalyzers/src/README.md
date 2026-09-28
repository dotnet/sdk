# Microsoft.CodeAnalysis.NetAnalyzers

Contains all **the .NET code analysis rules (CAxxxx)** that are built into the .NET SDK starting .NET5 release. The documentation for CA rules can be found at [learn.microsoft.com/visualstudio/code-quality/code-analysis-for-managed-code-warnings](https://learn.microsoft.com/visualstudio/code-quality/code-analysis-for-managed-code-warnings).

You do not need to manually install this NuGet package to your project if you are using .NET5 SDK or later. These analyzers are enabled by default for projects targeting .NET5 or later. For projects targeting earlier .NET frameworks, you can enable them in your MSBuild project file by setting one of the following properties:

1. *EnableNETAnalyzers*

   ```xml
   <PropertyGroup>
     <EnableNETAnalyzers>true</EnableNETAnalyzers>
   </PropertyGroup>
   ```

2. *AnalysisLevel*

   ```xml
   <PropertyGroup>
     <AnalysisLevel>latest</AnalysisLevel>
   </PropertyGroup>
   ```
