// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli
{
    public sealed class NewCommandDefinition : Command
    {
        public new const string Name = "new";

        public static readonly Option<string?> DebugCustomSettingsLocationOption = new("--debug:custom-hive")
        {
            Description = CommandDefinitionStrings.Option_Debug_CustomSettings,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugVirtualizeSettingsOption = new("--debug:ephemeral-hive", "--debug:virtual-hive")
        {
            Description = CommandDefinitionStrings.Option_Debug_VirtualSettings,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugAttachOption = new("--debug:attach")
        {
            Description = CommandDefinitionStrings.Option_Debug_Attach,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugReinitOption = new("--debug:reinit")
        {
            Description = CommandDefinitionStrings.Option_Debug_Reinit,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugRebuildCacheOption = new("--debug:rebuild-cache", "--debug:rebuildcache")
        {
            Description = CommandDefinitionStrings.Option_Debug_RebuildCache,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugShowConfigOption = new("--debug:show-config", "--debug:showconfig")
        {
            Description = CommandDefinitionStrings.Option_Debug_ShowConfig,
            Hidden = true,
            Recursive = true
        };

        public static readonly Argument<string> ShortNameArgument = new("template-short-name")
        {
            Description = CommandDefinitionStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1),
            Hidden = true
        };

        public static readonly Argument<string[]> RemainingArguments = new("template-args")
        {
            Description = CommandDefinitionStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999),
            Hidden = true
        };

        public static readonly Option<bool> InteractiveOption = SharedOptionsFactory.CreateInteractiveOption().AsHidden();

        public static readonly Option<string[]> AddSourceOption = SharedOptionsFactory.CreateAddSourceOption().AsHidden().DisableAllowMultipleArgumentsPerToken();

        public static readonly Option<bool> ColumnsAllOption = SharedOptionsFactory.CreateColumnsAllOption().AsHidden();

        public static readonly Option<string[]> ColumnsOption = SharedOptionsFactory.CreateColumnsOption().AsHidden().DisableAllowMultipleArgumentsPerToken();

        public static IReadOnlyList<Option> PassByOptions { get; } =
        [
            SharedOptions.ForceOption,
            SharedOptions.NameOption,
            SharedOptions.DryRunOption,
            SharedOptions.NoUpdateCheckOption
        ];

        public static readonly IEnumerable<Option> LegacyOptions;
        internal static readonly IEnumerable<Option> LegacyFilterOptions;

        static NewCommandDefinition()
        {
            LegacyFilterOptions =
            [
                SharedOptionsFactory.CreateAuthorOption().AsHidden(),
                SharedOptionsFactory.CreateBaselineOption().AsHidden(),
                SharedOptionsFactory.CreateLanguageOption().AsHidden(),
                SharedOptionsFactory.CreateTypeOption().AsHidden(),
                SharedOptionsFactory.CreateTagOption().AsHidden(),
                SharedOptionsFactory.CreatePackageOption().AsHidden()
            ];

            LegacyOptions =
            [
                InteractiveOption,
                AddSourceOption,
                ColumnsAllOption,
                ColumnsOption,
                .. LegacyFilterOptions
            ];
        }

        public readonly InstantiateCommandDefinition InstantiateCommand = new();
        public readonly InstallCommandDefinition InstallCommand = new(isLegacy: false);
        public readonly UninstallCommandDefinition UninstallCommand = new(isLegacy: false);
        public readonly UpdateCommandDefinition UpdateCommand = new();
        public readonly SearchCommandDefinition SearchCommand = new(isLegacy: false);
        public readonly ListCommandDefinition ListCommand = new(isLegacy: false);
        public readonly AliasCommandDefinition AliasCommand = new();
        public readonly DetailsCommandDefinition DetailsCommand = new();
        public readonly InstallCommandDefinition LegacyInstallCommand = new(isLegacy: true);
        public readonly UninstallCommandDefinition LegacyUninstallCommand = new(isLegacy: true);
        public readonly LegacyUpdateApplyCommandDefinition LegacyUpdateApplyCommand = new();
        public readonly LegacyUpdateCheckCommandDefinition LegacyUpdateCheckCommand = new();
        public readonly SearchCommandDefinition LegacySearchCommand = new(isLegacy: true);
        public readonly ListCommandDefinition LegacyListCommand = new(isLegacy: true);
        public readonly AddCommandDefinition LegacyAddAliasCommand = new(isLegacy: true);
        public readonly ShowCommandDefinition LegacyShowAliasCommand = new(isLegacy: true);

        public NewCommandDefinition()
            : base(Name, CommandDefinitionStrings.Command_New_Description)
        {
            TreatUnmatchedTokensAsErrors = true;

            Options.Add(DebugCustomSettingsLocationOption);
            Options.Add(DebugVirtualizeSettingsOption);
            Options.Add(DebugAttachOption);
            Options.Add(DebugReinitOption);
            Options.Add(DebugRebuildCacheOption);
            Options.Add(DebugShowConfigOption);
                
            Options.Add(SharedOptions.OutputOption);
            Options.Add(SharedOptions.NameOption);
            Options.Add(SharedOptions.DryRunOption);
            Options.Add(SharedOptions.ForceOption);
            Options.Add(SharedOptions.NoUpdateCheckOption);
            Options.Add(SharedOptions.ProjectPathOption);

            Options.AddRange(LegacyOptions);

            Arguments.Add(ShortNameArgument);
            Arguments.Add(RemainingArguments);

            Subcommands.Add(InstantiateCommand);
            Subcommands.Add(InstallCommand);
            Subcommands.Add(UninstallCommand);
            Subcommands.Add(UpdateCommand);
            Subcommands.Add(SearchCommand);
            Subcommands.Add(ListCommand);
            Subcommands.Add(AliasCommand);
            Subcommands.Add(DetailsCommand);
            Subcommands.Add(LegacyInstallCommand);
            Subcommands.Add(LegacyUninstallCommand);
            Subcommands.Add(LegacyUpdateApplyCommand);
            Subcommands.Add(LegacyUpdateCheckCommand);
            Subcommands.Add(LegacySearchCommand);
            Subcommands.Add(LegacyListCommand);
            Subcommands.Add(LegacyAddAliasCommand);
            Subcommands.Add(LegacyShowAliasCommand);
        }
    }

    public abstract class AliasCommandDefinitionBase(string name, string description)
        : Command(name, description)
    {
    }

    public sealed class AliasCommandDefinition : AliasCommandDefinitionBase
    {
        public new const string Name = "alias";

        public readonly AddCommandDefinition AddCommand = new(isLegacy: false);
        public readonly ShowCommandDefinition ShowCommand = new(isLegacy: false);

        public AliasCommandDefinition()
            : base(Name, CommandDefinitionStrings.Command_Alias_Description)
        {
            Hidden = true;
            Subcommands.Add(AddCommand);
            Subcommands.Add(ShowCommand);
        }
    }

    public sealed class AddCommandDefinition : AliasCommandDefinitionBase
    {
        public new const string Name = "add";
        public const string LegacyName = "--alias";

        public AddCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_AliasAdd_Description)
        {
            Hidden = true;

            if (isLegacy)
            {
                Aliases.Add("-a");
            }
        }
    }

    public sealed class ShowCommandDefinition : AliasCommandDefinitionBase
    {
        public new const string Name = "show";
        public const string LegacyName = "--show-alias";

        public ShowCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_AliasShow_Description)
        {
            Hidden = true;
        }
    }

    public sealed class InstantiateCommandDefinition : Command
    {
        public new const string Name = "create";

        public static readonly Argument<string> ShortNameArgument = new("template-short-name")
        {
            Description = CommandDefinitionStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1)
        };

        public static readonly Argument<string[]> RemainingArguments = new("template-args")
        {
            Description = CommandDefinitionStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999)
        };

        public InstantiateCommandDefinition()
            : base(Name, CommandDefinitionStrings.Command_Instantiate_Description)
        {
            Arguments.Add(ShortNameArgument);
            Arguments.Add(RemainingArguments);

            Options.Add(SharedOptions.OutputOption);
            Options.Add(SharedOptions.NameOption);
            Options.Add(SharedOptions.DryRunOption);
            Options.Add(SharedOptions.ForceOption);
            Options.Add(SharedOptions.NoUpdateCheckOption);
            Options.Add(SharedOptions.ProjectPathOption);

            Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.OutputOption));
            Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.NameOption));
            Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.DryRunOption));
            Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.ForceOption));
            Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.NoUpdateCheckOption));
            Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.ProjectPathOption));

            this.AddNoLegacyUsageValidators();
        }
    }

    public sealed class DetailsCommandDefinition : Command
    {
        public new const string Name = "details";

        // Option disabled until https://github.com/dotnet/templating/issues/6811 is solved
        //internal static Option<string> VersionOption = new("-version", "--version")
        //{
        //    Description = CommandDefinitionStrings.DetailsCommand_Option_Version,
        //    Arity = new ArgumentArity(1, 1)
        //};

        public static readonly Argument<string> NameArgument = new("package-identifier")
        {
            Description = CommandDefinitionStrings.DetailsCommand_Argument_PackageIdentifier,
            Arity = new ArgumentArity(1, 1)
        };

        public static readonly Option<bool> InteractiveOption = SharedOptions.InteractiveOption;
        public static readonly Option<string[]> AddSourceOption = SharedOptions.AddSourceOption;

        public DetailsCommandDefinition()
            : base(Name, CommandDefinitionStrings.Command_Details_Description)
        {
            Arguments.Add(NameArgument);
            Options.Add(InteractiveOption);
            Options.Add(AddSourceOption);
        }
    }

    public sealed class InstallCommandDefinition : Command
    {
        public new const string Name = "install";
        public const string LegacyName = "--install";

        public static readonly Argument<string[]> NameArgument = new("package")
        {
            Description = CommandDefinitionStrings.Command_Install_Argument_Package,
            Arity = new ArgumentArity(1, 99)
        };

        public static readonly Option<bool> ForceOption =
            SharedOptionsFactory.CreateForceOption().WithDescription(CommandDefinitionStrings.Option_Install_Force);

        public Option<bool> InteractiveOption { get; }
        public Option<string[]> AddSourceOption { get; }

        public InstallCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_Install_Description)
        {
            Hidden = isLegacy;

            if (isLegacy)
            {
                Aliases.Add("-i");
            }

            InteractiveOption = isLegacy ? NewCommandDefinition.InteractiveOption : SharedOptions.InteractiveOption;
            AddSourceOption = isLegacy ? NewCommandDefinition.AddSourceOption : SharedOptions.AddSourceOption;

            Arguments.Add(NameArgument);
            Options.Add(InteractiveOption);
            Options.Add(AddSourceOption);
            Options.Add(ForceOption);

            this.AddNoLegacyUsageValidators(isLegacy ? [InteractiveOption, AddSourceOption] : []);
        }
    }

    public sealed class UninstallCommandDefinition : Command
    {
        public new const string Name = "uninstall";
        public const string LegacyName = "--uninstall";

        public static readonly Argument<string[]> NameArgument = new("package")
        {
            Description = CommandDefinitionStrings.Command_Uninstall_Argument_Package,
            Arity = new ArgumentArity(0, 99)
        };

        public UninstallCommandDefinition(bool isLegacy)
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
    }

    public sealed class ListCommandDefinition : Command
    {
        public new const string Name = "list";
        public const string LegacyName = "--list";

        public static readonly IEnumerable<Option> SupportedFilterOptions =
        [
            SharedOptions.AuthorOption,
            SharedOptions.BaselineOption,
            SharedOptions.LanguageOption,
            SharedOptions.TypeOption,
            SharedOptions.TagOption,
        ];

        public static readonly Argument<string> NameArgument = new("template-name")
        {
            Description = CommandDefinitionStrings.Command_List_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        public static readonly Option<bool> IgnoreConstraintsOption = new("--ignore-constraints")
        {
            Description = CommandDefinitionStrings.ListCommand_Option_IgnoreConstraints,
            Arity = ArgumentArity.Zero
        }; 

        public Option<bool> ColumnsAllOption { get; }
        public Option<string[]> ColumnsOption { get; }

        public IEnumerable<Option> FilterOptions { get; }

        public ListCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_List_Description)
        {
            Hidden = isLegacy;

            if (isLegacy)
            {
                Aliases.Add("-l");
            }

            ColumnsAllOption = isLegacy ? NewCommandDefinition.ColumnsAllOption : SharedOptions.ColumnsAllOption;
            ColumnsOption = isLegacy ? NewCommandDefinition.ColumnsOption : SharedOptions.ColumnsOption;
            FilterOptions = isLegacy ? NewCommandDefinition.LegacyFilterOptions : SupportedFilterOptions;

            Arguments.Add(NameArgument);

            Options.AddRange(FilterOptions);

            Options.AddRange(
            [
                IgnoreConstraintsOption,
                SharedOptions.OutputOption,
                SharedOptions.ProjectPathOption,
                ColumnsAllOption,
                ColumnsOption,
            ]);

            this.AddNoLegacyUsageValidators(isLegacy ? [.. FilterOptions, ColumnsAllOption, ColumnsOption, NewCommandDefinition.ShortNameArgument] : []);

            if (isLegacy)
            {
                this.AddShortNameArgumentValidator(NameArgument);
            }
        }
    }

    public sealed class SearchCommandDefinition : Command
    {
        public new const string Name = "search";
        public const string LegacyName = "--search";

        public static readonly IEnumerable<Option> SupportedFilterOptions =
        [
            SharedOptions.AuthorOption,
            SharedOptions.BaselineOption,
            SharedOptions.LanguageOption,
            SharedOptions.TypeOption,
            SharedOptions.TagOption,
            SharedOptions.PackageOption
        ];

        public static readonly Argument<string> NameArgument = new("template-name")
        {
            Description = CommandDefinitionStrings.Command_Search_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        public static readonly Option<bool> IgnoreConstraintsOption = new("--ignore-constraints")
        {
            Description = CommandDefinitionStrings.ListCommand_Option_IgnoreConstraints,
            Arity = ArgumentArity.Zero
        };

        public Option<bool> ColumnsAllOption { get; }
        public Option<string[]> ColumnsOption { get; }

        public IEnumerable<Option> FilterOptions { get; }

        public SearchCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_Search_Description)
        {
            Hidden = isLegacy;
            ColumnsAllOption = isLegacy ? NewCommandDefinition.ColumnsAllOption : SharedOptions.ColumnsAllOption;
            ColumnsOption = isLegacy ? NewCommandDefinition.ColumnsOption : SharedOptions.ColumnsOption;
            FilterOptions = isLegacy ? NewCommandDefinition.LegacyFilterOptions : SupportedFilterOptions;

            Arguments.Add(NameArgument);

            Options.AddRange(FilterOptions);

            Options.AddRange(
            [
                ColumnsAllOption,
                ColumnsOption,
            ]);

            this.AddNoLegacyUsageValidators(isLegacy ? [.. FilterOptions, ColumnsAllOption, ColumnsOption, NewCommandDefinition.ShortNameArgument] : []);

            if (isLegacy)
            {
                this.AddShortNameArgumentValidator(NameArgument);
            }
        }
    }

    public abstract class UpdateCommandDefinitionBase : Command
    {
        public Option<bool> InteractiveOption { get; }
        public Option<string[]> AddSourceOption { get; }

        public static readonly Option<bool> CheckOnlyOption = new("--check-only", "--dry-run")
        {
            Description = CommandDefinitionStrings.Command_Update_Option_CheckOnly,
            Arity = ArgumentArity.Zero
        };

        protected UpdateCommandDefinitionBase(string name, string description, bool isLegacy)
            : base(name, description)
        {
            Hidden = isLegacy;

            if (!isLegacy)
            {
                Options.Add(CheckOnlyOption);
            }

            Options.Add(InteractiveOption = isLegacy ? NewCommandDefinition.InteractiveOption : SharedOptions.InteractiveOption);
            Options.Add(AddSourceOption = isLegacy ? NewCommandDefinition.AddSourceOption : SharedOptions.AddSourceOption);

            this.AddNoLegacyUsageValidators(isLegacy ? [InteractiveOption, AddSourceOption] : []);
        }
    }

    public sealed class UpdateCommandDefinition()
        : UpdateCommandDefinitionBase(Name, CommandDefinitionStrings.Command_Update_Description, isLegacy: false)
    {
        public new const string Name = "update";
    }

    public sealed class LegacyUpdateApplyCommandDefinition()
        : UpdateCommandDefinitionBase(Name, CommandDefinitionStrings.Command_Update_Description, isLegacy: true)
    {
        public new const string Name = "--update-apply";
    }

    public sealed class LegacyUpdateCheckCommandDefinition()
        : UpdateCommandDefinitionBase(Name, CommandDefinitionStrings.Command_Legacy_Update_Check_Description, isLegacy: true)
    {
        public new const string Name = "--update-check";
    }
}
