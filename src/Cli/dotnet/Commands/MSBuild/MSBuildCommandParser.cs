// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

internal static partial class MSBuildCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-msbuild";

    public static readonly Argument<string[]> Arguments = new("arguments");
    public static readonly Option<string[]?> TargetOption = CommonOptions.MSBuildTargetOption();

    public static Command CreateCommandDefinition()
    {
        var command = new Command("msbuild", CliCommandStrings.BuildAppFullName)
        {
            Arguments = { Arguments },
            DocsLink = DocsLink,
        };

        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(TargetOption);

        return command;
    }
}
