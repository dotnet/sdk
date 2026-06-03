// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

internal static class PrintEnvScriptCommandParser
{
    public static readonly Option<IEnvShellProvider?> ShellOption = CommonOptions.ShellOption;

    public static readonly Option<string?> DotnetInstallPathOption = new("--dotnet-install-path", "-d")
    {
        Description = "The path to the .NET installation directory. If not specified, uses the default user install path.",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Option<bool> DotnetupOnlyOption = new("--dotnetup-only")
    {
        Description = "Only add dotnetup to PATH. Do not set DOTNET_ROOT or add the .NET install path.",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Command s_printEnvScriptCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return s_printEnvScriptCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("print-env-script", "Generates a shell script that configures the environment for .NET");

        command.Options.Add(ShellOption);
        command.Options.Add(DotnetInstallPathOption);
        command.Options.Add(DotnetupOnlyOption);

        command.SetAction(parseResult => new PrintEnvScriptCommand(parseResult).Execute());

        return command;
    }
}
