// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package;
using Microsoft.DotNet.Cli.Commands.Package.Add;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;

internal static class AddPackageCommandDefinition
{
    public const string Name = "package";

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.PackageAddAppFullName);

        command.Arguments.Add(PackageAddCommandDefinition.CmdPackageArgument);
        command.Options.Add(PackageAddCommandDefinition.VersionOption);
        command.Options.Add(PackageAddCommandDefinition.FrameworkOption);
        command.Options.Add(PackageAddCommandDefinition.NoRestoreOption);
        command.Options.Add(PackageAddCommandDefinition.SourceOption);
        command.Options.Add(PackageAddCommandDefinition.PackageDirOption);
        command.Options.Add(PackageAddCommandDefinition.InteractiveOption);
        command.Options.Add(PackageAddCommandDefinition.PrereleaseOption);
        command.Options.Add(PackageCommandDefinition.ProjectOption);
        command.Options.Add(PackageCommandDefinition.FileOption);

        return command;
    }
}
