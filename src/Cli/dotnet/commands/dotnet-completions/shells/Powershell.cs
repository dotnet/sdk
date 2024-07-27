// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Completions.Shells;

public class PowershellShellProvider : IShellProvider
{
    public static string PowerShell => "pwsh";

    public string ArgumentName => PowershellShellProvider.PowerShell;

    private static readonly string _dynamicCompletionScript =
        """
        # PowerShell parameter completion shim for the dotnet CLI
        Register-ArgumentCompleter -Native -CommandName dotnet -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)
                dotnet complete --position $cursorPosition "$commandAst" | ForEach-Object {
                    [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                }
        }
        """;

    public string GenerateCompletions(System.CommandLine.CliCommand command) => _dynamicCompletionScript;
}
