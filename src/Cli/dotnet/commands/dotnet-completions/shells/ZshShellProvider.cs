// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Completions.Shells;

public class ZshShellProvider : IShellProvider
{
    public string ArgumentName => "zsh";

    private static readonly string _dynamicCompletionScript =
        """
        # zsh parameter completion for the dotnet CLI
        # add this to your .zshrc file to enable completion

        _dotnet_zsh_complete()
        {
        local completions=("$(dotnet complete "$words")")

        # If the completion list is empty, just continue with filename selection
        if [ -z "$completions" ]
        then
            _arguments '*::arguments: _normal'
            return
        fi

        # This is not a variable assignment, don't remove spaces!
        _values = "${(ps:\n:)completions}"
        }

        compdef _dotnet_zsh_complete dotnet
        """;

    public string GenerateCompletions(System.CommandLine.CliCommand command) => _dynamicCompletionScript;
}
