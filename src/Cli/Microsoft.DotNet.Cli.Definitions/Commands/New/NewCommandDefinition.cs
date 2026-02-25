// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.New;

public sealed class NewCommandDefinition : Command
{
    public new const string Name = "new";
    public const VerbosityOptions DefaultVerbosity = VerbosityOptions.normal;

    private const string Link = "https://aka.ms/dotnet-new";

    public readonly Option<bool> DisableSdkTemplatesOption = new Option<bool>("--debug:disable-sdk-templates")
    {
        DefaultValueFactory = static _ => false,
        Description = CommandDefinitionStrings.DisableSdkTemplates_OptionDescription,
        Recursive = true
    }.Hide();

    public readonly Option<bool> DisableProjectContextEvaluationOption = new Option<bool>(
        "--debug:disable-project-context")
    {
        DefaultValueFactory = static _ => false,
        Description = CommandDefinitionStrings.DisableProjectContextEval_OptionDescription,
        Recursive = true
    }.Hide();

    public readonly Option<VerbosityOptions> VerbosityOption = new("--verbosity", "-v")
    {
        DefaultValueFactory = _ => DefaultVerbosity,
        Description = CommandDefinitionStrings.Verbosity_OptionDescription,
        HelpName = CommandDefinitionStrings.LevelArgumentName,
        Recursive = true
    };

    public readonly Option<bool> DiagnosticOption =
        CommonOptions
            .CreateDiagnosticsOption(recursive: true)
            .WithDescription(CommandDefinitionStrings.Diagnostics_OptionDescription);

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

    public readonly NewCreateCommandDefinition InstantiateCommand = new();
    public readonly NewInstallCommandDefinition InstallCommand;
    public readonly NewUninstallCommandDefinition UninstallCommand = new(isLegacy: false);
    public readonly NewUpdateCommandDefinition UpdateCommand;
    public readonly NewSearchCommandDefinition SearchCommand;
    public readonly NewListCommandDefinition ListCommand;
    public readonly NewAliasCommandDefinition AliasCommand = new();
    public readonly NewDetailsCommandDefinition DetailsCommand = new();
    public readonly NewInstallCommandDefinition LegacyInstallCommand;
    public readonly NewUninstallCommandDefinition LegacyUninstallCommand = new(isLegacy: true);
    public readonly NewUpdateApplyLegacyCommandDefinition LegacyUpdateApplyCommand;
    public readonly NewUpdateCheckLegacyCommandDefinition LegacyUpdateCheckCommand;
    public readonly NewSearchCommandDefinition LegacySearchCommand;
    public readonly NewListCommandDefinition LegacyListCommand;
    public readonly NewAliasAddCommandDefinition LegacyAddAliasCommand = new(isLegacy: true);
    public readonly NewAliasShowCommandDefinition LegacyShowAliasCommand = new(isLegacy: true);

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

        Options.Add(DisableSdkTemplatesOption);
        Options.Add(DisableProjectContextEvaluationOption);
        Options.Add(VerbosityOption);
        Options.Add(DiagnosticOption);

        Arguments.Add(ShortNameArgument);
        Arguments.Add(RemainingArguments);

        InstallCommand = new(this, isLegacy: false);
        UpdateCommand = new(this);
        SearchCommand = new(this, isLegacy: false);
        ListCommand = new(this, isLegacy: false);

        LegacyUpdateApplyCommand = new(this);
        LegacyUpdateCheckCommand = new(this);
        LegacyInstallCommand = new(this, isLegacy: true);
        LegacyListCommand = new(this, isLegacy: true);
        LegacySearchCommand = new(this, isLegacy: true);

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
