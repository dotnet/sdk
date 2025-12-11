// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.Add;

namespace Microsoft.DotNet.Cli.Commands.Hidden.Add.Package;

internal static class AddPackageCommandDefinition
{
    public const string Name = "package";

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.PackageAddAppFullName);
        PackageAddCommandDefinition.AddOptionsAndArguments(command);
        return command;
    }
}
