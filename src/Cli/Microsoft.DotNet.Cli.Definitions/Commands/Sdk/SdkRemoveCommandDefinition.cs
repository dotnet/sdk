// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Remove;

internal sealed class SdkRemoveCommandDefinition() : SdkRemoveCommandDefinitionBase(Name)
{
    public new const string Name = "remove";

    public override Argument<string>? GetProjectOrFileArgument()
        => null;
}

internal abstract class SdkRemoveCommandDefinitionBase : Command
{
    public readonly Argument<string[]> SdkIdArgument = new(CommonArguments.SdkIdArgumentName)
    {
        Description = CommandDefinitionStrings.SdkRemoveAppHelpText,
        HelpName = CommandDefinitionStrings.CmdSdk,
        Arity = ArgumentArity.OneOrMore,
    };

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption().ForwardIfEnabled("--interactive");
    public readonly Option<string?> ProjectOption = PackageCommandDefinition.CreateProjectOption();
    public readonly Option<string?> FileOption = PackageCommandDefinition.CreateFileOption();

    public SdkRemoveCommandDefinitionBase(string name)
        : base(name, CommandDefinitionStrings.SdkRemoveAppFullName)
    {
        Arguments.Add(SdkIdArgument);
        Options.Add(InteractiveOption);
        Options.Add(ProjectOption);
        Options.Add(FileOption);
    }

    public abstract Argument<string>? GetProjectOrFileArgument();
}
