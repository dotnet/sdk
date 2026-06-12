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
        # Add the following content to your config.nu file:

        let dotnet_completer = {|spans|
            dotnet complete ($spans | str join " ") | lines
        }

        # If you are using other completers, add the dotnet completer. Otherwise, just set external.completer to $dotnet_completer.
        # (see https://nushell.sh/cookbook/external_completers.html#multiple-completer for more info)
        let multiple_completers = {|spans|
            match $spans.0 {
                # Add the dotnet completer
                "dotnet" => $dotnet_completer
                # your other completers...
            } | do $in $spans
        }

        $env.config.completions.external.enable = true
        $env.config.completions.external.completer = $multiple_completers
        """;

    public string GenerateCompletions(System.CommandLine.Command command) => _dynamicCompletionScript;
}
