; Unshipped resource-generator diagnostics
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules
Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CLIRESX0001 | Microsoft.DotNet.Cli.Resources | Warning | Resource entry is not a string
CLIRESX0002 | Microsoft.DotNet.Cli.Resources | Error | Resource generator option is not supported
CLIRESX0003 | Microsoft.DotNet.Cli.Resources | Error | Resource file cannot be generated
CLIRESX0004 | Microsoft.DotNet.Cli.Resources | Error | Resource member name conflicts with another generated member
CLIRESX0005 | Microsoft.DotNet.Cli.Resources | Error | Multiple resource files generate the same class