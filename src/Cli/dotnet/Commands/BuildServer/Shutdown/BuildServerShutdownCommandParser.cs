// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;

internal static class BuildServerShutdownCommandParser
{
    public static readonly CliOption<bool> MSBuildOption = new("--msbuild")
    {
        Description = CliCommandStrings.MSBuildOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> VbcsOption = new("--vbcscompiler")
    {
        Description = CliCommandStrings.VBCSCompilerOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly CliOption<bool> RazorOption = new("--razor")
    {
        Description = CliCommandStrings.RazorOptionDescription,
        Arity = ArgumentArity.Zero
    };

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("shutdown", CliCommandStrings.BuildServerShutdownCommandDescription);

        command.Options.Add(MSBuildOption);
        command.Options.Add(VbcsOption);
        command.Options.Add(RazorOption);

        command.SetAction((parseResult) => new BuildServerShutdownCommand(parseResult).Execute());

        return command;
    }
}
