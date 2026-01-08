// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.Remove;

internal sealed class PackageRemoveCommandDefinition : PackageRemoveCommandDefinitionBase
{
    public new const string Name = "remove";

    public PackageRemoveCommandDefinition()
        : base(Name)
    {
        Options.Add(ProjectOption);
        Options.Add(FileOption);
    }
}

internal abstract class PackageRemoveCommandDefinitionBase : Command
{
    public readonly Argument<string[]> CmdPackageArgument = new(CommandDefinitionStrings.CmdPackage)
    {
        Description = CommandDefinitionStrings.PackageRemoveAppHelpText,
        Arity = ArgumentArity.OneOrMore,
    };

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption().ForwardIfEnabled("--interactive");
    public readonly Option<string?> ProjectOption = PackageCommandDefinition.CreateProjectOption();
    public readonly Option<string?> FileOption = PackageCommandDefinition.CreateFileOption();

    public PackageRemoveCommandDefinitionBase(string name)
        : base(name, CommandDefinitionStrings.PackageRemoveAppFullName)
    {
        Arguments.Add(CmdPackageArgument);
        Options.Add(InteractiveOption);
    }
}
