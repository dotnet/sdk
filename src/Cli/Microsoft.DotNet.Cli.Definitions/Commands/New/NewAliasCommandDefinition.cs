// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New
{
    public abstract class NewAliasCommandDefinitionBase(string name, string description)
        : Command(name, description)
    {
    }

    public sealed class NewAliasCommandDefinition : NewAliasCommandDefinitionBase
    {
        public new const string Name = "alias";

        public readonly NewAliasAddCommandDefinition AddCommand = new(isLegacy: false);
        public readonly NewAliasShowCommandDefinition ShowCommand = new(isLegacy: false);

        public NewAliasCommandDefinition()
            : base(Name, CommandDefinitionStrings.Command_Alias_Description)
        {
            Hidden = true;
            Subcommands.Add(AddCommand);
            Subcommands.Add(ShowCommand);
        }
    }

    public sealed class NewAliasAddCommandDefinition : NewAliasCommandDefinitionBase
    {
        public new const string Name = "add";
        public const string LegacyName = "--alias";

        public NewAliasAddCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_AliasAdd_Description)
        {
            Hidden = true;

            if (isLegacy)
            {
                Aliases.Add("-a");
            }
        }
    }

    public sealed class NewAliasShowCommandDefinition : NewAliasCommandDefinitionBase
    {
        public new const string Name = "show";
        public const string LegacyName = "--show-alias";

        public NewAliasShowCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_AliasShow_Description)
        {
            Hidden = true;
        }
    }
}
