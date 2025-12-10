// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.BuildServer.Shutdown;

internal static class BuildServerShutdownCommandDefinition
{
    public const string Name = "shutdown";

    public static readonly Option<bool> MSBuildOption = new("--msbuild")
    {
        Description = CliCommandStrings.MSBuildOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> VbcsOption = new("--vbcscompiler")
    {
        Description = CliCommandStrings.VBCSCompilerOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option<bool> RazorOption = new("--razor")
    {
        Description = CliCommandStrings.RazorOptionDescription,
        Arity = ArgumentArity.Zero
    };

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.BuildServerShutdownCommandDescription);

        command.Options.Add(MSBuildOption);
        command.Options.Add(VbcsOption);
        command.Options.Add(RazorOption);

        return command;
    }
}
