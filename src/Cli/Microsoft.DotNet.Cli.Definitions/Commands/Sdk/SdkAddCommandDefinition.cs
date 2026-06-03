// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Package;

namespace Microsoft.DotNet.Cli.Commands.Sdk.Add;

public class SdkAddCommandDefinition() : SdkAddCommandDefinitionBase(Name)
{
    public new const string Name = "add";

    public override Argument<string>? GetProjectOrFileArgument()
        => null;
}

public abstract class SdkAddCommandDefinitionBase : Command
{
    public static Option<string> CreateVersionOption() => new Option<string>("--version", "-v")
    {
        Description = CommandDefinitionStrings.CmdVersionDescription,
        HelpName = CommandDefinitionStrings.CmdVersion,
    };

    public static Option<bool> CreateNoRestoreOption() => new("--no-restore", "-n")
    {
        Description = CommandDefinitionStrings.SdkAddCmdNoRestoreDescription,
        Arity = ArgumentArity.Zero
    };

    public static Option<bool> CreateInteractiveOption() => CommonOptions.CreateInteractiveOption().ForwardIfEnabled("--interactive");

    public readonly Argument<SdkReferenceIdentity> SdkIdArgument = CommonArguments.CreateRequiredSdkReferenceIdentityArgument();
    public readonly Option<string> VersionOption = CreateVersionOption();
    public readonly Option<bool> NoRestoreOption = CreateNoRestoreOption();
    public readonly Option<bool> InteractiveOption = CreateInteractiveOption();
    public readonly Option<string?> ProjectOption = PackageCommandDefinition.CreateProjectOption();
    public readonly Option<string?> FileOption = PackageCommandDefinition.CreateFileOption();

    public SdkAddCommandDefinitionBase(string name)
        : base(name, CommandDefinitionStrings.SdkAddAppFullName)
    {
        VersionOption.Validators.Add(result =>
        {
            if (result.Parent?.GetValue(SdkIdArgument).HasVersion == true)
            {
                result.AddError(CommandDefinitionStrings.ValidationFailedDuplicateVersion);
            }
        });

        Arguments.Add(SdkIdArgument);

        Options.Add(VersionOption);
        Options.Add(NoRestoreOption);
        Options.Add(InteractiveOption);
        Options.Add(ProjectOption);
        Options.Add(FileOption);
    }

    public abstract Argument<string>? GetProjectOrFileArgument();
}
