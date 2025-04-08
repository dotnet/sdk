// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

internal static class MSBuildCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-msbuild";

    public static readonly CliArgument<string[]> Arguments = new("arguments");

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        var command = new DocumentedCommand("msbuild", DocsLink, CliCommandStrings.BuildAppFullName)
        {
            Arguments
        };

        command.Options.Add(CommonOptions.DisableBuildServersOption);

        command.SetAction(MSBuildCommand.Run);

        return command;
    }
}
