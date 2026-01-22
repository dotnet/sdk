// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvCommandParser
{
    internal static readonly IEnvShellProvider[] SupportedShells =
    [
        new BashEnvShellProvider(),
        new ZshEnvShellProvider(),
        new PowerShellEnvShellProvider()
    ];

    public static readonly Option<string> ShellOption = new("--shell", "-s")
    {
        Description = $"The shell for which to generate the environment script (supported: {string.Join(", ", SupportedShells.Select(s => s.ArgumentName))})",
        Arity = ArgumentArity.ExactlyOne
    };

    public static readonly Option<string?> DotnetInstallPathOption = new("--dotnet-install-path", "-d")
    {
        Description = "The path to the .NET installation directory. If not specified, uses the default user install path.",
        Arity = ArgumentArity.ZeroOrOne
    };

    private static readonly Command EnvCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return EnvCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("env", "Generates a shell script that configures the environment for .NET");

        command.Options.Add(ShellOption);
        command.Options.Add(DotnetInstallPathOption);

        command.SetAction(parseResult => new EnvCommand(parseResult).Execute());

        return command;
    }
}
