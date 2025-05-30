// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Semver;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.DNVM;

namespace Microsoft.DotNet.Cli.Commands.DNVM;

internal static class InstallCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-install";

    public static readonly Option<SemVersion> SdkVersionOption =
        new("--sdk-version", "The version of the SDK to install") { IsRequired = true };

    public static readonly Option<bool> ForceOption =
        new("--force", "Force installation even if the SDK is already installed");

    public static readonly Option<string> SdkDirOption =
        new("--sdk-dir", "The directory to install the SDK into");

    public static readonly Option<bool> VerboseOption =
        new("--verbose", "Enable verbose logging");

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        var installCommand = new DocumentedCommand("install", DocsLink)
        {
            SdkVersionOption,
            ForceOption,
            SdkDirOption,
            VerboseOption
        };

        installCommand.SetAction((parseResult) => InstallCommand.Run(parseResult));

        return installCommand;
    }
}