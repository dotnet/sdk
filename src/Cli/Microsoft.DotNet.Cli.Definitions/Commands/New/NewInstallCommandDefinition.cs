// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class NewInstallCommandDefinition : Command
{
    public new const string Name = "install";
    public const string LegacyName = "--install";

    public readonly Argument<string[]> NameArgument = CreateNameArgument();

    public readonly Option<bool> ForceOption = CreateForceOption();
    public readonly Option<bool> InteractiveOption;
    public readonly Option<string[]> AddSourceOption;

    public NewInstallCommandDefinition(NewCommandDefinition parent, bool isLegacy)
        : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_Install_Description)
    {
        Hidden = isLegacy;

        if (isLegacy)
        {
            Aliases.Add("-i");

            InteractiveOption = parent.LegacyOptions.InteractiveOption;
            AddSourceOption = parent.LegacyOptions.AddSourceOption;
        }
        else
        {
            InteractiveOption = SharedOptionsFactory.CreateInteractiveOption();
            AddSourceOption = SharedOptionsFactory.CreateAddSourceOption();
        }

        Arguments.Add(NameArgument);
        Options.Add(InteractiveOption);
        Options.Add(AddSourceOption);
        Options.Add(ForceOption);

        this.AddNoLegacyUsageValidators(isLegacy ? [InteractiveOption.Name, AddSourceOption.Name] : []);
    }

    public static Argument<string[]> CreateNameArgument() => new("package")
    {
        Description = CommandDefinitionStrings.Command_Install_Argument_Package,
        Arity = new ArgumentArity(1, 99)
    };

    public static Option<bool> CreateForceOption()
        => SharedOptionsFactory.CreateForceOption().WithDescription(CommandDefinitionStrings.Option_Install_Force);
}
