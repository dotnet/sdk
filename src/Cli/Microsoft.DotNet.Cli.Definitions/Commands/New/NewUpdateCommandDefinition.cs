// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public abstract class NewUpdateCommandDefinitionBase : Command
{
    public readonly Option<bool> InteractiveOption;
    public readonly Option<string[]> AddSourceOption;

    protected NewUpdateCommandDefinitionBase(NewCommandDefinition parent, string name, string description, bool isLegacy)
        : base(name, description)
    {
        Hidden = isLegacy;

        if (isLegacy)
        {
            InteractiveOption = parent.LegacyOptions.InteractiveOption;
            AddSourceOption = parent.LegacyOptions.AddSourceOption;
        }
        else
        {
            InteractiveOption = SharedOptionsFactory.CreateInteractiveOption();
            AddSourceOption = SharedOptionsFactory.CreateAddSourceOption();
        }

        Options.Add(InteractiveOption);
        Options.Add(AddSourceOption);

        this.AddNoLegacyUsageValidators(isLegacy ? [InteractiveOption.Name, AddSourceOption.Name] : []);
    }

    public abstract bool GetCheckOnlyValue(ParseResult result);

    public static Option<bool> CreateCheckOnlyOption()
        => new("--check-only", "--dry-run")
        {
            Description = CommandDefinitionStrings.Command_Update_Option_CheckOnly,
            Arity = ArgumentArity.Zero
        };
}

public sealed class NewUpdateCommandDefinition : NewUpdateCommandDefinitionBase
{
    public new const string Name = "update";

    public readonly Option<bool> CheckOnlyOption = CreateCheckOnlyOption();

    public NewUpdateCommandDefinition(NewCommandDefinition parent)
        : base(parent, Name, CommandDefinitionStrings.Command_Update_Description, isLegacy: false)
    {
        Options.Add(CheckOnlyOption);
    }

    public override bool GetCheckOnlyValue(ParseResult result)
        => result.GetValue(CheckOnlyOption);
}

public sealed class NewUpdateApplyLegacyCommandDefinition(NewCommandDefinition parent)
    : NewUpdateCommandDefinitionBase(parent, Name, CommandDefinitionStrings.Command_Update_Description, isLegacy: true)
{
    public new const string Name = "--update-apply";

    public override bool GetCheckOnlyValue(ParseResult result)
        => false;
}

public sealed class NewUpdateCheckLegacyCommandDefinition(NewCommandDefinition parent)
    : NewUpdateCommandDefinitionBase(parent, Name, CommandDefinitionStrings.Command_Legacy_Update_Check_Description, isLegacy: true)
{
    public new const string Name = "--update-check";

    public override bool GetCheckOnlyValue(ParseResult result)
        => true;
}
