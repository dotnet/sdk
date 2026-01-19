// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CA1810 // Initialize reference type static fields inline

using System.CommandLine;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.TemplateEngine.Cli;

internal class CommandDefinition(string name, string description) : Command(name, description)
{
    public static class New
    {
        public static readonly Option<string?> DebugCustomSettingsLocationOption = new("--debug:custom-hive")
        {
            Description = SymbolStrings.Option_Debug_CustomSettings,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugVirtualizeSettingsOption = new("--debug:ephemeral-hive", "--debug:virtual-hive")
        {
            Description = SymbolStrings.Option_Debug_VirtualSettings,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugAttachOption = new("--debug:attach")
        {
            Description = SymbolStrings.Option_Debug_Attach,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugReinitOption = new("--debug:reinit")
        {
            Description = SymbolStrings.Option_Debug_Reinit,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugRebuildCacheOption = new("--debug:rebuild-cache", "--debug:rebuildcache")
        {
            Description = SymbolStrings.Option_Debug_RebuildCache,
            Hidden = true,
            Recursive = true
        };

        public static readonly Option<bool> DebugShowConfigOption = new("--debug:show-config", "--debug:showconfig")
        {
            Description = SymbolStrings.Option_Debug_ShowConfig,
            Hidden = true,
            Recursive = true
        };

        public static readonly Argument<string> ShortNameArgument = new("template-short-name")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1),
            Hidden = true
        };

        public static readonly Argument<string[]> RemainingArguments = new("template-args")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_TemplateOptions,
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

        internal static readonly IEnumerable<Option> LegacyOptions;
        internal static readonly IEnumerable<Option> LegacyFilterOptions;

        private static readonly Lazy<CommandDefinition> s_lazyCommand = new(Create);

        static New()
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

        public static CommandDefinition Command => s_lazyCommand.Value;

        private static CommandDefinition Create()
        {
            var command = new CommandDefinition("new", SymbolStrings.Command_New_Description)
            {
                TreatUnmatchedTokensAsErrors = true,
                Options =
                {
                    DebugCustomSettingsLocationOption,
                    DebugVirtualizeSettingsOption,
                    DebugAttachOption,
                    DebugReinitOption,
                    DebugRebuildCacheOption,
                    DebugShowConfigOption,

                    SharedOptions.OutputOption,
                    SharedOptions.NameOption,
                    SharedOptions.DryRunOption,
                    SharedOptions.ForceOption,
                    SharedOptions.NoUpdateCheckOption,
                    SharedOptions.ProjectPathOption,
                },
                Arguments =
                {
                    ShortNameArgument,
                    RemainingArguments,
                },
                Subcommands =
                {
                    Instantiate.Command,
                    Install.Command,
                    Uninstall.Command,
                    Update.Command,
                    Search.Command,
                    List.Command,
                    Alias.Command,
                    Details.Command,

                    Install.LegacyCommand,
                    Uninstall.LegacyCommand,
                    Update.LegacyCheckCommand,
                    Update.LegacyApplyCommand,
                    Search.LegacyCommand,
                    List.LegacyCommand,
                    Alias.Add.LegacyCommand,
                    Alias.Show.LegacyCommand,
                }
            };

            command.Options.AddRange(LegacyOptions);
            return command;
        }
    }

    public static class Alias
    {
        public static readonly CommandDefinition Command = new("alias", SymbolStrings.Command_Alias_Description)
        {
            Hidden = true,
            Subcommands =
            {
                Add.Command,
                Show.Command,
            }
        };

        public static class Add
        {
            public static readonly CommandDefinition Command = new("add", SymbolStrings.Command_AliasAdd_Description)
            {
                Hidden = true,
            };

            public static readonly CommandDefinition LegacyCommand = new("--alias", SymbolStrings.Command_AliasAdd_Description)
            {
                Hidden = true,
                Aliases = { "-a" }
            };
        }

        public static class Show
        {
            public static readonly CommandDefinition Command = new("show", SymbolStrings.Command_AliasShow_Description)
            {
                Hidden = true,
            };

            public static readonly CommandDefinition LegacyCommand = new("--show-alias", SymbolStrings.Command_AliasShow_Description)
            {
                Hidden = true,
            };
        }
    }

    public static class Instantiate
    {
        public static readonly Argument<string> ShortNameArgument = new("template-short-name")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1)
        };

        public static readonly Argument<string[]> RemainingArguments = new("template-args")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999)
        };

        public static readonly CommandDefinition Command = new CommandDefinition("create", SymbolStrings.Command_Instantiate_Description)
        {
            Arguments =
            {
                ShortNameArgument,
                RemainingArguments
            },
            Options =
            {
                SharedOptions.OutputOption,
                SharedOptions.NameOption,
                SharedOptions.DryRunOption,
                SharedOptions.ForceOption,
                SharedOptions.NoUpdateCheckOption,
                SharedOptions.ProjectPathOption,
            },
            Validators =
            {
                symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.OutputOption),
                symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.NameOption),
                symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.DryRunOption),
                symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.ForceOption),
                symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.NoUpdateCheckOption),
                symbolResult => symbolResult.ValidateOptionUsage(SharedOptions.ProjectPathOption),
            }
        }.AddNoLegacyUsageValidators();
    }

    public static class Details
    {
        // Option disabled until https://github.com/dotnet/templating/issues/6811 is solved
        //internal static Option<string> VersionOption = new("-version", "--version")
        //{
        //    Description = LocalizableStrings.DetailsCommand_Option_Version,
        //    Arity = new ArgumentArity(1, 1)
        //};

        public static readonly Argument<string> NameArgument = new("package-identifier")
        {
            Description = LocalizableStrings.DetailsCommand_Argument_PackageIdentifier,
            Arity = new ArgumentArity(1, 1)
        };

        public static readonly Option<bool> InteractiveOption = SharedOptions.InteractiveOption;
        public static readonly Option<string[]> AddSourceOption = SharedOptions.AddSourceOption;

        public static readonly CommandDefinition Command = new("details", SymbolStrings.Command_Details_Description)
        {
            Arguments =
            {
                NameArgument
            },
            Options =
            {
                InteractiveOption,
                AddSourceOption,
            }
        };
    }

    public sealed class Install : CommandDefinition
    {
        public static readonly Argument<string[]> NameArgument = new("package")
        {
            Description = SymbolStrings.Command_Install_Argument_Package,
            Arity = new ArgumentArity(1, 99)
        };

        public static readonly Option<bool> ForceOption =
            SharedOptionsFactory.CreateForceOption().WithDescription(SymbolStrings.Option_Install_Force);

        public static readonly Install Command = new("install", isLegacy: false);

        public static readonly Install LegacyCommand = new("--install", isLegacy: true)
        {
            Aliases = { "-i" },
        };

        public Option<bool> InteractiveOption { get; }
        public Option<string[]> AddSourceOption { get; }

        public Install(string name, bool isLegacy)
            : base(name, SymbolStrings.Command_Install_Description)
        {
            Hidden = isLegacy;

            InteractiveOption = isLegacy ? New.InteractiveOption : SharedOptions.InteractiveOption;
            AddSourceOption = isLegacy ? New.AddSourceOption : SharedOptions.AddSourceOption;

            Arguments.Add(NameArgument);
            Options.Add(InteractiveOption);
            Options.Add(AddSourceOption);
            Options.Add(ForceOption);

            this.AddNoLegacyUsageValidators(isLegacy ? [InteractiveOption, AddSourceOption] : []);
        }
    }

    public sealed class Uninstall : CommandDefinition
    {
        public static readonly Argument<string[]> NameArgument = new("package")
        {
            Description = SymbolStrings.Command_Uninstall_Argument_Package,
            Arity = new ArgumentArity(0, 99)
        };

        public static readonly Uninstall Command = new("uninstall", isLegacy: false);

        public static readonly Uninstall LegacyCommand = new("--uninstall", isLegacy: true)
        {
            Aliases = { "-u" },
        };

        public Uninstall(string name, bool isLegacy)
            : base(name, SymbolStrings.Command_Uninstall_Description)
        {
            Hidden = isLegacy;
            Arguments.Add(NameArgument);
            this.AddNoLegacyUsageValidators();
        }
    }

    public sealed class List : CommandDefinition
    {
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
            Description = SymbolStrings.Command_List_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        public static readonly Option<bool> IgnoreConstraintsOption = new("--ignore-constraints")
        {
            Description = SymbolStrings.ListCommand_Option_IgnoreConstraints,
            Arity = ArgumentArity.Zero
        };

        public static readonly List Command = new("list", isLegacy: false);

        public static readonly List LegacyCommand = new List("--list", isLegacy: true)
        {
            Aliases = { "-l" },
        }.AddShortNameArgumentValidator(NameArgument);

        public Option<bool> ColumnsAllOption { get; }
        public Option<string[]> ColumnsOption { get; }

        public IEnumerable<Option> FilterOptions { get; }

        public List(string name, bool isLegacy)
            : base(name, SymbolStrings.Command_List_Description)
        {
            Hidden = isLegacy;
            ColumnsAllOption = isLegacy ? New.ColumnsAllOption : SharedOptions.ColumnsAllOption;
            ColumnsOption = isLegacy ? New.ColumnsOption : SharedOptions.ColumnsOption;
            FilterOptions = isLegacy ? New.LegacyFilterOptions : SupportedFilterOptions;

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

            this.AddNoLegacyUsageValidators(isLegacy ? [.. FilterOptions, ColumnsAllOption, ColumnsOption, New.ShortNameArgument] : []);
        }
    }

    public sealed class Search : CommandDefinition
    {
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
            Description = SymbolStrings.Command_Search_Argument_Name,
            Arity = new ArgumentArity(0, 1)
        };

        public static readonly Option<bool> IgnoreConstraintsOption = new("--ignore-constraints")
        {
            Description = SymbolStrings.ListCommand_Option_IgnoreConstraints,
            Arity = ArgumentArity.Zero
        };

        public static readonly Search Command = new("search", isLegacy: false);
        public static readonly Search LegacyCommand = new Search("--search", isLegacy: true).AddShortNameArgumentValidator(NameArgument);

        public Option<bool> ColumnsAllOption { get; }
        public Option<string[]> ColumnsOption { get; }

        public IEnumerable<Option> FilterOptions { get; }

        public Search(string name, bool isLegacy)
            : base(name, SymbolStrings.Command_Search_Description)
        {
            Hidden = isLegacy;
            ColumnsAllOption = isLegacy ? New.ColumnsAllOption : SharedOptions.ColumnsAllOption;
            ColumnsOption = isLegacy ? New.ColumnsOption : SharedOptions.ColumnsOption;
            FilterOptions = isLegacy ? New.LegacyFilterOptions : SupportedFilterOptions;

            Arguments.Add(NameArgument);

            Options.AddRange(FilterOptions);

            Options.AddRange(
            [
                ColumnsAllOption,
                ColumnsOption,
            ]);

            this.AddNoLegacyUsageValidators(isLegacy ? [.. FilterOptions, ColumnsAllOption, ColumnsOption, New.ShortNameArgument] : []);
        }
    }

    public sealed class Update : CommandDefinition
    {
        public static readonly Option<bool> CheckOnlyOption = new("--check-only", "--dry-run")
        {
            Description = SymbolStrings.Command_Update_Option_CheckOnly,
            Arity = ArgumentArity.Zero
        };

        public Option<bool> InteractiveOption { get; }
        public Option<string[]> AddSourceOption { get; }

        public Update(string name, string description, bool isLegacy)
            : base(name, description)
        {
            Hidden = isLegacy;

            Options.Add(InteractiveOption = isLegacy ? New.InteractiveOption : SharedOptions.InteractiveOption);
            Options.Add(AddSourceOption = isLegacy ? New.AddSourceOption : SharedOptions.AddSourceOption);

            this.AddNoLegacyUsageValidators(isLegacy ? [InteractiveOption, AddSourceOption] : []);
        }

        public static readonly Update Command = new("update", SymbolStrings.Command_Update_Description, isLegacy: false)
        {
            Options =
            {
                CheckOnlyOption,
            }
        };

        public static readonly Update LegacyApplyCommand = new("--update-apply", SymbolStrings.Command_Update_Description, isLegacy: true);
        public static readonly Update LegacyCheckCommand = new("--update-check", SymbolStrings.Command_Legacy_Update_Check_Description, isLegacy: true);
    }
}
