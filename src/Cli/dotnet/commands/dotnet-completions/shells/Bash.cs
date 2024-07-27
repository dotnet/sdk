// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Completions.Shells;

public class BashShellProvider : IShellProvider
{
    public string ArgumentName => "bash";

    private static readonly string _dynamicCompletionScript =
        """
        # bash parameter completion for the dotnet CLI
        function _dotnet_bash_complete()
        {
        local cur="${COMP_WORDS[COMP_CWORD]}" IFS=$'\n' # On Windows you may need to use use IFS=$'\r\n'
        local candidates

        read -d '' -ra candidates < <(dotnet complete --position "${COMP_POINT}" "${COMP_LINE}" 2>/dev/null)

        read -d '' -ra COMPREPLY < <(compgen -W "${candidates[*]:-}" -- "$cur")
        }

        complete -f -F _dotnet_bash_complete dotnet
        """;

    public string GenerateCompletions(System.CommandLine.CliCommand command) => _dynamicCompletionScript;
}
