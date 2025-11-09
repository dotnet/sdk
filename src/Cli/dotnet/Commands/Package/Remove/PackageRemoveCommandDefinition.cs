// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.Remove;

internal static class PackageRemoveCommandDefinition
{
    public const string Name = "remove";

    public static readonly Argument<string[]> CmdPackageArgument = new(CliCommandStrings.CmdPackage)
    {
        Description = CliCommandStrings.PackageRemoveAppHelpText,
        Arity = ArgumentArity.OneOrMore,
    };

    public static readonly Option<bool> InteractiveOption = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    public static readonly IEnumerable<Option> Options =
    [
        InteractiveOption,
        PackageCommandDefinition.ProjectOption,
        PackageCommandDefinition.FileOption
    ];

    public static Command Create()
    {
        var command = new Command(Name, CliCommandStrings.PackageRemoveAppFullName);

        command.Arguments.Add(CmdPackageArgument);
        command.Options.AddRange(Options);

        return command;
    }
}
