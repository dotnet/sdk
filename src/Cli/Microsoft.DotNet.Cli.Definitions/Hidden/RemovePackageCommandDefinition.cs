// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Remove;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Remove.Package;

internal static class RemovePackageCommandDefinition
{
    public const string Name = "package";

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.PackageRemoveAppFullName);

        command.Arguments.Add(PackageRemoveCommandDefinition.CmdPackageArgument);
        command.Options.Add(PackageRemoveCommandDefinition.InteractiveOption);

        return command;
    }
}
