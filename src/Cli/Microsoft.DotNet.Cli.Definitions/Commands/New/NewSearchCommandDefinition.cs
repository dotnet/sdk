// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class NewSearchCommandDefinition : Command
{
    public new const string Name = "search";
    public const string LegacyName = "--search";

    public const bool HasSupportedPackageFilterOption = true;

    public readonly Argument<string> NameArgument = CreateNameArgument();

    public readonly Option<bool> IgnoreConstraintsOption = new("--ignore-constraints")
    {
        Description = CommandDefinitionStrings.ListCommand_Option_IgnoreConstraints,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<bool> ColumnsAllOption;
    public readonly Option<string[]> ColumnsOption;
    public readonly FilterOptions FilterOptions;

    public NewSearchCommandDefinition(NewCommandDefinition parent, bool isLegacy)
        : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_Search_Description)
    {
        Hidden = isLegacy;

        if (isLegacy)
        {
            ColumnsAllOption = parent.LegacyOptions.ColumnsAllOption;
            ColumnsOption = parent.LegacyOptions.ColumnsOption;
            FilterOptions = parent.LegacyOptions.FilterOptions;
        }
        else
        {
            ColumnsAllOption = SharedOptionsFactory.CreateColumnsAllOption();
            ColumnsOption = SharedOptionsFactory.CreateColumnsOption();
            FilterOptions = FilterOptions.CreateSupported(HasSupportedPackageFilterOption);
        }

        Arguments.Add(NameArgument);

        Options.AddRange(FilterOptions.AllOptions);

        Options.AddRange(
        [
            ColumnsAllOption,
            ColumnsOption,
        ]);

        this.AddNoLegacyUsageValidators(isLegacy ? [.. FilterOptions.AllNames, ColumnsAllOption.Name, ColumnsOption.Name, NewCommandDefinition.ShortNameArgumentName] : []);

        if (isLegacy)
        {
            this.AddShortNameArgumentValidator(NameArgument);
        }
    }

    public static Argument<string> CreateNameArgument() => new("template-name")
    {
        Description = CommandDefinitionStrings.Command_Search_Argument_Name,
        Arity = new ArgumentArity(0, 1)
    };
}
