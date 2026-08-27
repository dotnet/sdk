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

    // Source-selection options: modern `dotnet new search` only. The legacy `--search`
    // syntax never supported feed selection, so these are not exposed on that branch.
    public readonly Option<string[]> SourceOption = SharedOptionsFactory.CreateSourceOption();
    public readonly Option<FileInfo> ConfigFileOption = SharedOptionsFactory.CreateConfigFileOption();
    // Disable multiple arguments per token so a single `--add-source` occurrence cannot greedily
    // consume the positional template-name argument (e.g. `--add-source <url> <name>`); this mirrors
    // the same fix already applied to the legacy option in LegacyOptions.CreateAddSourceOption().
    // Repeating `--add-source` multiple times is unaffected and remains supported.
    public readonly Option<string[]> AddSourceOption = SharedOptionsFactory.CreateAddSourceOption().DisableAllowMultipleArgumentsPerToken();
    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption();

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

        if (!isLegacy)
        {
            Options.AddRange(
            [
                SourceOption,
                ConfigFileOption,
                AddSourceOption,
                InteractiveOption,
            ]);
        }

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
