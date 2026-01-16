// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli
{
    public sealed class NewCommandDefinition : Command
    {
        public new const string Name = "new";
        private const string Link = "https://aka.ms/dotnet-new";

        public readonly Option<string?> DebugCustomSettingsLocationOption = new("--debug:custom-hive")
        {
            Description = CommandDefinitionStrings.Option_Debug_CustomSettings,
            Hidden = true,
            Recursive = true
        };

        public readonly Option<bool> DebugVirtualizeSettingsOption = new("--debug:ephemeral-hive", "--debug:virtual-hive")
        {
            Description = CommandDefinitionStrings.Option_Debug_VirtualSettings,
            Hidden = true,
            Recursive = true
        };

        public readonly Option<bool> DebugAttachOption = new("--debug:attach")
        {
            Description = CommandDefinitionStrings.Option_Debug_Attach,
            Hidden = true,
            Recursive = true
        };

        public readonly Option<bool> DebugReinitOption = new("--debug:reinit")
        {
            Description = CommandDefinitionStrings.Option_Debug_Reinit,
            Hidden = true,
            Recursive = true
        };

        public readonly Option<bool> DebugRebuildCacheOption = CreateDebugRebuildCacheOption();

        public readonly Option<bool> DebugShowConfigOption = new("--debug:show-config", "--debug:showconfig")
        {
            Description = CommandDefinitionStrings.Option_Debug_ShowConfig,
            Hidden = true,
            Recursive = true
        };

        public const string ShortNameArgumentName = "template-short-name";

        public readonly Argument<string> ShortNameArgument = CreateShortNameArgument();

        public const string RemainingArgumentsName = "template-args";

        public readonly Argument<string[]> RemainingArguments = new(RemainingArgumentsName)
        {
            Description = CommandDefinitionStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999),
            Hidden = true
        };

        public readonly InstantiateOptions InstantiateOptions = new();

        public readonly LegacyOptions LegacyOptions = new();
        public readonly FilterOptions LegacyFilterOptions = FilterOptions.CreateLegacy();

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
        public readonly AliasAddCommandDefinition LegacyAddAliasCommand = new(isLegacy: true);
        public readonly AliasShowCommandDefinition LegacyShowAliasCommand = new(isLegacy: true);

        public NewCommandDefinition()
            : base(Name, CommandDefinitionStrings.Command_New_Description)
        {
            this.DocsLink = Link;
            TreatUnmatchedTokensAsErrors = true;

            Options.Add(DebugCustomSettingsLocationOption);
            Options.Add(DebugVirtualizeSettingsOption);
            Options.Add(DebugAttachOption);
            Options.Add(DebugReinitOption);
            Options.Add(DebugRebuildCacheOption);
            Options.Add(DebugShowConfigOption);

            Options.AddRange(InstantiateOptions.AllOptions);
            Options.AddRange(LegacyOptions.AllOptions);
            Options.AddRange(LegacyFilterOptions.AllOptions);

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

        public static Option<bool> CreateDebugRebuildCacheOption() => new("--debug:rebuild-cache", "--debug:rebuildcache")
        {
            Description = CommandDefinitionStrings.Option_Debug_RebuildCache,
            Hidden = true,
            Recursive = true
        };

        public static Argument<string> CreateShortNameArgument() => new(ShortNameArgumentName)
        {
            Description = CommandDefinitionStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1),
            Hidden = true
        };
    }

    public abstract class AliasCommandDefinitionBase(string name, string description)
        : Command(name, description)
    {
    }

    public sealed class AliasCommandDefinition : AliasCommandDefinitionBase
    {
        public new const string Name = "alias";

        public readonly AliasAddCommandDefinition AddCommand = new(isLegacy: false);
        public readonly AliasShowCommandDefinition ShowCommand = new(isLegacy: false);

        public AliasCommandDefinition()
            : base(Name, CommandDefinitionStrings.Command_Alias_Description)
        {
            Hidden = true;
            Subcommands.Add(AddCommand);
            Subcommands.Add(ShowCommand);
        }
    }

    public sealed class AliasAddCommandDefinition : AliasCommandDefinitionBase
    {
        public new const string Name = "add";
        public const string LegacyName = "--alias";

        public AliasAddCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_AliasAdd_Description)
        {
            Hidden = true;

            if (isLegacy)
            {
                Aliases.Add("-a");
            }
        }
    }

    public sealed class AliasShowCommandDefinition : AliasCommandDefinitionBase
    {
        public new const string Name = "show";
        public const string LegacyName = "--show-alias";

        public AliasShowCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_AliasShow_Description)
        {
            Hidden = true;
        }
    }

    public sealed class InstantiateCommandDefinition : Command
    {
        public new const string Name = "create";

        public readonly Argument<string> ShortNameArgument = CreateShortNameArgument();

        public readonly Argument<string[]> RemainingArguments = new("template-args")
        {
            Description = CommandDefinitionStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999)
        };

        public readonly InstantiateOptions InstantiateOptions = new();

        public InstantiateCommandDefinition()
            : base(Name, CommandDefinitionStrings.Command_Instantiate_Description)
        {
            Arguments.Add(ShortNameArgument);
            Arguments.Add(RemainingArguments);

            Options.AddRange(InstantiateOptions.AllOptions);

            foreach (var option in InstantiateOptions.AllOptions)
            {
                Validators.Add(symbolResult => symbolResult.ValidateOptionUsage(option.Name));
            }

            this.AddNoLegacyUsageValidators();
        }

        public static Argument<string> CreateShortNameArgument() => new("template-short-name")
        {
            Description = CommandDefinitionStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1)
        };
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

        public readonly Argument<string> NameArgument = new("package-identifier")
        {
            Description = CommandDefinitionStrings.DetailsCommand_Argument_PackageIdentifier,
            Arity = new ArgumentArity(1, 1)
        };

        public readonly Option<bool> InteractiveOption = SharedOptionsFactory.CreateInteractiveOption();
        public readonly Option<string[]> AddSourceOption = SharedOptionsFactory.CreateAddSourceOption();

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

        public readonly Argument<string[]> NameArgument = CreateNameArgument();

        public readonly Option<bool> ForceOption = CreateForceOption();
        public readonly Option<bool> InteractiveOption;
        public readonly Option<string[]> AddSourceOption;

        public InstallCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_Install_Description)
        {
            Hidden = isLegacy;

            if (isLegacy)
            {
                Aliases.Add("-i");
            }

            InteractiveOption = isLegacy ? LegacyOptions.CreateInteractiveOption() : SharedOptionsFactory.CreateInteractiveOption();
            AddSourceOption = isLegacy ? LegacyOptions.CreateAddSourceOption() : SharedOptionsFactory.CreateAddSourceOption();

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

    public sealed class UninstallCommandDefinition : Command
    {
        public new const string Name = "uninstall";
        public const string LegacyName = "--uninstall";

        public readonly Argument<string[]> NameArgument = CreateNameArgument();

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

        public static Argument<string[]> CreateNameArgument() => new("package")
        {
            Description = CommandDefinitionStrings.Command_Uninstall_Argument_Package,
            Arity = new ArgumentArity(0, 99)
        };
    }

    public sealed class ListCommandDefinition : Command
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
        public readonly Option<FileInfo> ProjectPathOption = SharedOptionsFactory.CreateProjectPathOption();

        public readonly Option<bool> ColumnsAllOption;
        public readonly Option<string[]> ColumnsOption;
        public readonly FilterOptions FilterOptions;

        public ListCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_List_Description)
        {
            Hidden = isLegacy;

            if (isLegacy)
            {
                Aliases.Add("-l");
            }

            ColumnsAllOption = isLegacy ? LegacyOptions.CreateColumnsAllOption() : SharedOptionsFactory.CreateColumnsAllOption();
            ColumnsOption = isLegacy ? LegacyOptions.CreateColumnsOption() : SharedOptionsFactory.CreateColumnsOption();
            FilterOptions = isLegacy ? FilterOptions.CreateLegacy() : FilterOptions.CreateSupported(HasSupportedPackageFilterOption);

            Arguments.Add(NameArgument);

            Options.AddRange(FilterOptions.AllOptions);

            Options.Add(IgnoreConstraintsOption);
            Options.Add(OutputOption);
            Options.Add(ProjectPathOption);
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

    public sealed class SearchCommandDefinition : Command
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

        public SearchCommandDefinition(bool isLegacy)
            : base(isLegacy ? LegacyName : Name, CommandDefinitionStrings.Command_Search_Description)
        {
            Hidden = isLegacy;
            ColumnsAllOption = isLegacy ? LegacyOptions.CreateColumnsAllOption() : SharedOptionsFactory.CreateColumnsAllOption();
            ColumnsOption = isLegacy ? LegacyOptions.CreateColumnsOption() : SharedOptionsFactory.CreateColumnsOption();
            FilterOptions = isLegacy ? FilterOptions.CreateLegacy() : FilterOptions.CreateSupported(HasSupportedPackageFilterOption);

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

    public abstract class UpdateCommandDefinitionBase : Command
    {
        public readonly Option<bool> InteractiveOption;
        public readonly Option<string[]> AddSourceOption;

        public readonly Option<bool> CheckOnlyOption = CreateCheckOnlyOption();

        protected UpdateCommandDefinitionBase(string name, string description, bool isLegacy)
            : base(name, description)
        {
            Hidden = isLegacy;

            if (!isLegacy)
            {
                Options.Add(CheckOnlyOption);
            }

            Options.Add(InteractiveOption = isLegacy ? LegacyOptions.CreateInteractiveOption() : SharedOptionsFactory.CreateInteractiveOption());
            Options.Add(AddSourceOption = isLegacy ? LegacyOptions.CreateAddSourceOption() : SharedOptionsFactory.CreateAddSourceOption());

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

    public sealed class UpdateCommandDefinition()
        : UpdateCommandDefinitionBase(Name, CommandDefinitionStrings.Command_Update_Description, isLegacy: false)
    {
        public new const string Name = "update";

        public override bool GetCheckOnlyValue(ParseResult result)
            => result.GetValue(CheckOnlyOption);
    }

    public sealed class LegacyUpdateApplyCommandDefinition()
        : UpdateCommandDefinitionBase(Name, CommandDefinitionStrings.Command_Update_Description, isLegacy: true)
    {
        public new const string Name = "--update-apply";

        public override bool GetCheckOnlyValue(ParseResult result)
            => false;
    }

    public sealed class LegacyUpdateCheckCommandDefinition()
        : UpdateCommandDefinitionBase(Name, CommandDefinitionStrings.Command_Legacy_Update_Check_Description, isLegacy: true)
    {
        public new const string Name = "--update-check";

        public override bool GetCheckOnlyValue(ParseResult result)
            => true;
    }
}
