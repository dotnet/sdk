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

    private static readonly Command s_printEnvScriptCommand = ConstructCommand(name: "print-env-script", hidden: true);

    public static Command GetCommand()
    {
        return s_printEnvScriptCommand;
    }

    /// <summary>
    /// Constructs a new command instance that emits the env script. Used by both the
    /// top-level (now hidden) <c>print-env-script</c> command and the <c>env script</c>
    /// subcommand. Each invocation returns a fresh <see cref="Command"/> because
    /// System.CommandLine does not allow the same instance to be attached to multiple
    /// parent commands.
    /// </summary>
    internal static Command ConstructCommand(string name, bool hidden = false)
    {
        Command command = new(name, "Generates a shell script that configures the environment for .NET")
        {
            Hidden = hidden,
        };

        command.Options.Add(ShellOption);
        command.Options.Add(DotnetInstallPathOption);
        command.Options.Add(DotnetupOnlyOption);

        command.SetAction(parseResult => new PrintEnvScriptCommand(parseResult).Execute());

        return command;
    }
}
