// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class NewListCommandDefinition : Command
{
    public new const string Name = "list";
    public const string LegacyName = "--list";

    public const bool HasSupportedPackageFilterOption = false;

    public const string IgnoreConstraintsOptionName = "--ignore-constraints";

    public readonly Argument<string> NameArgument = CreateNameArgument();

    public readonly Option<bool> IgnoreConstraintsOption = new(IgnoreConstraintsOptionName)
    {
        Description = CommandDefinitionStrings.ListCommand_Option_IgnoreConstraints,
        Arity = ArgumentArity.Zero
    };

    public readonly Option<FileInfo> OutputOption = SharedOptionsFactory.CreateOutputOption();
    public readonly Option<FileInfo> ProjectOption = SharedOptionsFactory.CreateProjectOption();

    public readonly Option<bool> ColumnsAllOption;
    public readonly Option<string[]> ColumnsOption;
    public readonly FilterOptions FilterOptions;

    public NewListCommandDefinition(NewCommandDefinition parent, bool isLegacy)
        : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_List_Description)
    {
        Hidden = isLegacy;

        if (isLegacy)
        {
            Aliases.Add("-l");

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

        Options.Add(IgnoreConstraintsOption);
        Options.Add(OutputOption);
        Options.Add(ProjectOption);
        Options.Add(ColumnsAllOption);
        Options.Add(ColumnsOption);

        this.AddNoLegacyUsageValidators(isLegacy ? [.. FilterOptions.AllNames, ColumnsAllOption.Name, ColumnsOption.Name, NewCommandDefinition.ShortNameArgumentName] : []);

        if (isLegacy)
        {
            this.AddShortNameArgumentValidator(NameArgument);
        }
    }

    public static Argument<string> CreateNameArgument() => new("template-name")
    {
        Description = CommandDefinitionStrings.Command_List_Argument_Name,
        Arity = new ArgumentArity(0, 1)
    };
}
