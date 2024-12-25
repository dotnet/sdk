// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Shells;

public class FishShellProvider : IShellProvider
{
    public string ArgumentName => "fish";

    private static readonly string _dynamicCompletionScript =
        """
        # fish parameter completion for the dotnet CLI
        # add the following to your config.fish to enable completions

        complete -f -c dotnet -a "(dotnet complete (commandline -cp))"
        """;

    public string GenerateCompletions(System.CommandLine.CliCommand command) => _dynamicCompletionScript;
}
