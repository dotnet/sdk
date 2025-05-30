// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Semver;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.DNVM;

namespace Microsoft.DotNet.Cli.Commands.DNVM;

internal static class UninstallCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-dnvm";

    public static readonly Argument<SemVersion> SdkVersionArgument =
        new("sdk-version", "The version of the SDK to uninstall");

    public static readonly Option<string> SdkDirOption =
        new("--sdk-dir", "Uninstall the SDK from the given directory");

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var uninstallCommand = new DocumentedCommand("uninstall", DocsLink)
        {
            SdkVersionArgument,
            SdkDirOption
        };

        uninstallCommand.SetAction((parseResult) => UninstallCommand.Run(parseResult));

        return uninstallCommand;
    }
}
