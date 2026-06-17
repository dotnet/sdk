// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal static class EnvScriptCommandParser
{
    public static readonly Option<IEnvShellProvider?> ShellOption = CommonOptions.ShellOption;

    public static readonly Option<string?> DotnetInstallPathOption = new("--dotnet-install-path", "-d")
    {
        Description = "The path to the .NET installation directory. If not specified, uses the default user install path.",
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly Option<bool> DotnetOption = new("--dotnet")
    {
        Description = "Wire the managed dotnet into the environment (DOTNET_ROOT + dotnet on PATH).",
    };

    public static readonly Option<bool> DotnetupOption = new("--dotnetup")
    {
        Description = "Add the dotnetup directory to PATH.",
    };

    /// <summary>
    /// Hidden legacy alias for <c>--dotnetup</c>, kept so managed profile blocks written by
    /// older dotnetup versions (which call the hidden <c>print-env-script</c> command with
    /// <c>--dotnetup-only</c>) keep working through the compatibility window.
    /// </summary>
    public static readonly Option<bool> DotnetupOnlyOption = new("--dotnetup-only")
    {
        Description = "Only add dotnetup to PATH. Do not set DOTNET_ROOT or add the .NET install path.",
        Hidden = true,
    };

    /// <summary>
    /// Constructs a new command instance that emits the env script. Used by both the
    /// primary <c>env script</c> subcommand and the hidden top-level
    /// <c>print-env-script</c> alias (kept for one release for backwards compatibility).
    /// Each invocation returns a fresh <see cref="Command"/> because System.CommandLine
    /// does not allow the same instance to be attached to multiple parent commands.
    /// </summary>
    public static Command ConstructCommand(string name, bool hidden = false)
    {
        Command command = new(name, "Generates a shell script that configures the environment for .NET. With no selection flags it follows your configured env settings; pass --dotnet and/or --dotnetup to emit only those parts.")
        {
            Hidden = hidden,
        };

        command.Options.Add(ShellOption);
        command.Options.Add(DotnetInstallPathOption);
        command.Options.Add(DotnetOption);
        command.Options.Add(DotnetupOption);
        command.Options.Add(DotnetupOnlyOption);

        command.SetAction(parseResult => new EnvScriptCommand(parseResult).Execute());

        return command;
    }
}
