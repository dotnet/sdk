// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine.StaticCompletions.Resources;

namespace System.CommandLine.StaticCompletions.Shells;

public class NushellShellProvider : IShellProvider
{
    public string ArgumentName => ShellNames.Nushell;

    public string Extension => "nu";

    public string HelpDescription => Strings.NuShellShellProvider_HelpDescription;

    // override the ToString method to return the argument name so that CLI help is cleaner for 'default' values
    public override string ToString() => ArgumentName;

    private static readonly string _dynamicCompletionScript =
        """
        def "nu-complete dotnet" [context: string] {
            ^dotnet complete $"($context)" | lines 
        }

        export extern "dotnet" [
            ...command: string@"nu-complete dotnet"
        ]
        """;

    public string GenerateCompletions(System.CommandLine.Command command) => _dynamicCompletionScript;
}
