// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

internal static class MSBuildCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-msbuild";

    public static readonly Argument<string[]> Arguments = new("arguments");
    public static readonly Option<string[]?> TargetOption = CommonOptions.MSBuildTargetOption();

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var command = new DocumentedCommand("msbuild", DocsLink, CliCommandStrings.BuildAppFullName)
        {
            Arguments
        };

        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(TargetOption);
        command.SetAction(MSBuildCommand.Run);

        return command;
    }
}
