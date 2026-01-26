// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class NewUninstallCommandDefinition : Command
{
    public new const string Name = "uninstall";
    public const string LegacyName = "--uninstall";

    public readonly Argument<string[]> NameArgument = CreateNameArgument();

    public NewUninstallCommandDefinition(bool isLegacy)
        : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_Uninstall_Description)
    {
        Hidden = isLegacy;

        if (isLegacy)
        {
            Aliases.Add("-u");
        }

        Arguments.Add(NameArgument);
        this.AddNoLegacyUsageValidators();
    }

    public static Argument<string[]> CreateNameArgument() => new("package")
    {
        Description = CommandDefinitionStrings.Command_Uninstall_Argument_Package,
        Arity = new ArgumentArity(0, 99)
    };
}
