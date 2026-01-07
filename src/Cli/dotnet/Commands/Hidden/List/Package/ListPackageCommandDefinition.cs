// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package.List;

namespace Microsoft.DotNet.Cli.Commands.Hidden.List.Package;

internal static class ListPackageCommandDefinition
{
    public const string Name = "package";

    public static Command Create()
    {
        Command command = new(Name, CliCommandStrings.PackageListAppFullName);

        command.Options.Add(PackageListCommandDefinition.VerbosityOption);
        command.Options.Add(PackageListCommandDefinition.OutdatedOption);
        command.Options.Add(PackageListCommandDefinition.DeprecatedOption);
        command.Options.Add(PackageListCommandDefinition.VulnerableOption);
        command.Options.Add(PackageListCommandDefinition.FrameworkOption);
        command.Options.Add(PackageListCommandDefinition.TransitiveOption);
        command.Options.Add(PackageListCommandDefinition.PrereleaseOption);
        command.Options.Add(PackageListCommandDefinition.HighestPatchOption);
        command.Options.Add(PackageListCommandDefinition.HighestMinorOption);
        command.Options.Add(PackageListCommandDefinition.ConfigOption);
        command.Options.Add(PackageListCommandDefinition.SourceOption);
        command.Options.Add(PackageListCommandDefinition.InteractiveOption);
        command.Options.Add(PackageListCommandDefinition.FormatOption);
        command.Options.Add(PackageListCommandDefinition.OutputVersionOption);
        command.Options.Add(PackageListCommandDefinition.NoRestore);

        return command;
    }
}
