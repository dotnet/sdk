// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using Command = System.CommandLine.Command;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseCommand<TDefinition>(Func<ParseResult, ITemplateEngineHost> hostBuilder, TDefinition definition)
        : Command(definition.Name, definition.Description)
        where TDefinition : Command
    {
        public readonly TDefinition Definition = definition;

        protected static readonly Dictionary<string, Func<Func<ParseResult, ITemplateEngineHost>, Command, Command>> SubcommandFactories = new()
        {
            { NewAliasCommandDefinition.Name, (hostBuilder, definition) => new AliasCommand(hostBuilder, (NewAliasCommandDefinition)definition) },
            { NewAliasAddCommandDefinition.Name, (hostBuilder, definition) => new AliasAddCommand(hostBuilder, (NewAliasAddCommandDefinition)definition) },
            { NewAliasAddCommandDefinition.LegacyName, (hostBuilder, definition) => new AliasAddCommand(hostBuilder, (NewAliasAddCommandDefinition)definition) },
            { NewAliasShowCommandDefinition.Name, (hostBuilder, definition) => new AliasShowCommand(hostBuilder, (NewAliasShowCommandDefinition)definition) },
            { NewAliasShowCommandDefinition.LegacyName, (hostBuilder, definition) => new AliasShowCommand(hostBuilder, (NewAliasShowCommandDefinition)definition) },
            { NewCreateCommandDefinition.Name, (hostBuilder, definition) => new InstantiateCommand(hostBuilder, (NewCreateCommandDefinition)definition) },
            { NewDetailsCommandDefinition.Name, (hostBuilder, definition) => new DetailsCommand(hostBuilder, (NewDetailsCommandDefinition)definition) },
            { NewInstallCommandDefinition.Name, (hostBuilder, definition) => new InstallCommand(hostBuilder, (NewInstallCommandDefinition)definition) },
            { NewInstallCommandDefinition.LegacyName, (hostBuilder, definition) => new LegacyInstallCommand(hostBuilder, (NewInstallCommandDefinition)definition) },
            { NewUninstallCommandDefinition.Name, (hostBuilder, definition) => new UninstallCommand(hostBuilder, (NewUninstallCommandDefinition)definition) },
            { NewUninstallCommandDefinition.LegacyName, (hostBuilder, definition) => new LegacyUninstallCommand(hostBuilder, (NewUninstallCommandDefinition)definition) },
            { NewListCommandDefinition.Name, (hostBuilder, definition) => new ListCommand(hostBuilder, (NewListCommandDefinition)definition) },
            { NewListCommandDefinition.LegacyName, (hostBuilder, definition) => new LegacyListCommand(hostBuilder, (NewListCommandDefinition)definition) },
            { NewSearchCommandDefinition.Name, (hostBuilder, definition) => new SearchCommand(hostBuilder, (NewSearchCommandDefinition)definition) },
            { NewSearchCommandDefinition.LegacyName, (hostBuilder, definition) => new LegacySearchCommand(hostBuilder, (NewSearchCommandDefinition)definition) },
            { NewUpdateCommandDefinition.Name, (hostBuilder, definition) => new UpdateCommand(hostBuilder, (NewUpdateCommandDefinition)definition) },
            { NewUpdateApplyLegacyCommandDefinition.Name, (hostBuilder, definition) => new LegacyUpdateApplyCommand(hostBuilder, (NewUpdateApplyLegacyCommandDefinition)definition) },
            { NewUpdateCheckLegacyCommandDefinition.Name, (hostBuilder, definition) => new LegacyUpdateCheckCommand(hostBuilder, (NewUpdateCheckLegacyCommandDefinition)definition) },
        };

        protected internal virtual IEnumerable<CompletionItem> GetCompletions(CompletionContext context, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager)
        {
#pragma warning disable SA1100 // Do not prefix calls with base unless local implementation exists
            return base.GetCompletions(context);
#pragma warning restore SA1100 // Do not prefix calls with base unless local implementation exists
        }

        protected IEngineEnvironmentSettings CreateEnvironmentSettings(GlobalArgs args, ParseResult parseResult)
        {
            ITemplateEngineHost host = hostBuilder(parseResult);
            IEnvironment environment = new CliEnvironment();

            return new EngineEnvironmentSettings(
                host,
                virtualizeSettings: args.DebugVirtualizeSettings,
                environment: environment,
                pathInfo: new CliPathInfo(host, environment, args.DebugCustomSettingsLocation));
        }
    }

    internal abstract class BaseCommand<TArgs, TDefinition> : BaseCommand<TDefinition>
        where TDefinition : Command
        where TArgs : GlobalArgs
    {
        internal BaseCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, TDefinition definition)
            : base(hostBuilder, definition)
        {
            this.DocsLink = definition.DocsLink;
            Hidden = definition.Hidden;
            TreatUnmatchedTokensAsErrors = definition.TreatUnmatchedTokensAsErrors;

            Aliases.AddRange(definition.Aliases);
            Options.AddRange(definition.Options);
            Arguments.AddRange(definition.Arguments);
            Validators.AddRange(definition.Validators);

            foreach (var subcommandDef in definition.Subcommands)
            {
                Add(SubcommandFactories[subcommandDef.Name](hostBuilder, subcommandDef));
            }

            Action = new CommandAction(this);
        }

        public override IEnumerable<CompletionItem> GetCompletions(CompletionContext context)
        {
            if (context.ParseResult == null)
            {
                return base.GetCompletions(context);
            }
            var args = new GlobalArgs(context.ParseResult);
            using IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args, context.ParseResult);
            using TemplatePackageManager templatePackageManager = new(environmentSettings);
            return GetCompletions(context, environmentSettings, templatePackageManager).ToList();
        }

        /// <summary>
        /// Checks if the template with same short name as used command alias exists, and if so prints the example on how to run the template using dotnet new create.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="TemplatePackageManager.GetTemplatesAsync(CancellationToken)"/>, however this should not take long as templates normally at least once
        /// are queried before and results are cached.
        /// Alternatively we can think of caching template groups early in <see cref="BaseCommand{TArgs}"/> later on.
        /// </remarks>
        protected internal static async Task CheckTemplatesWithSubCommandName(
            TArgs args,
            TemplatePackageManager templatePackageManager,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ITemplateInfo> availableTemplates = await templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            string usedCommandAlias = args.ParseResult.CommandResult.IdentifierToken.Value;
            if (!availableTemplates.Any(t => t.ShortNameList.Any(sn => string.Equals(sn, usedCommandAlias, StringComparison.OrdinalIgnoreCase))))
            {
                return;
            }

            Reporter.Output.WriteLine(LocalizableStrings.Commands_TemplateShortNameCommandConflict_Info, usedCommandAlias);

            var example = Example.For<InstantiateCommand>(args.ParseResult).WithArguments(usedCommandAlias);

            Reporter.Output.WriteCommand(example);
            Reporter.Output.WriteLine();
        }

        protected static void PrintDeprecationMessage<TDeprecatedCommand, TNewCommand>(ParseResult parseResult, Func<TNewCommand, Option>? additionalNewOptionSelector = null)
            where TDeprecatedCommand : Command
            where TNewCommand : Command
        {
            var newCommandExample = Example.For<TNewCommand>(parseResult);
            if (additionalNewOptionSelector != null)
            {
                newCommandExample = newCommandExample.WithOption(additionalNewOptionSelector);
            }

            Reporter.Output.WriteLine(string.Format(
             LocalizableStrings.Commands_Warning_DeprecatedCommand,
             Example.For<TDeprecatedCommand>(parseResult),
             newCommandExample).Yellow());

            Reporter.Output.WriteLine(LocalizableStrings.Commands_Warning_DeprecatedCommand_Info.Yellow());
            Reporter.Output.WriteCommand(Example.For<TNewCommand>(parseResult).WithHelpOption().ToString().Yellow());
            Reporter.Output.WriteLine();
        }

        protected abstract Task<NewCommandStatus> ExecuteAsync(TArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, ParseResult parseResult, CancellationToken cancellationToken);

        protected abstract TArgs ParseContext(ParseResult parseResult);

        private static async Task HandleGlobalOptionsAsync(
            TArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            CancellationToken cancellationToken)
        {
            HandleDebugAttach(args);
            HandleDebugReinit(args, environmentSettings);
            await HandleDebugRebuildCacheAsync(args, templatePackageManager, cancellationToken).ConfigureAwait(false);
            HandleDebugShowConfig(args, environmentSettings);
        }

        private static void HandleDebugAttach(TArgs args)
        {
            if (!args.DebugAttach)
            {
                return;
            }
            Reporter.Output.WriteLine("Attach to the process and press any key");
            Console.ReadLine();
        }

        private static void HandleDebugReinit(TArgs args, IEngineEnvironmentSettings environmentSettings)
        {
            if (!args.DebugReinit)
            {
                return;
            }
            environmentSettings.Host.FileSystem.DirectoryDelete(environmentSettings.Paths.HostVersionSettingsDir, true);
            environmentSettings.Host.FileSystem.CreateDirectory(environmentSettings.Paths.HostVersionSettingsDir);
        }

        private static Task HandleDebugRebuildCacheAsync(TArgs args, TemplatePackageManager templatePackageManager, CancellationToken cancellationToken)
        {
            if (!args.DebugRebuildCache)
            {
                return Task.CompletedTask;
            }
            return templatePackageManager.RebuildTemplateCacheAsync(cancellationToken);
        }

        private static void HandleDebugShowConfig(TArgs args, IEngineEnvironmentSettings environmentSettings)
        {
            if (!args.DebugShowConfig)
            {
                return;
            }

            Reporter.Output.WriteLine(LocalizableStrings.CurrentConfiguration);
            Reporter.Output.WriteLine(" ");

            TabularOutput<IMountPointFactory> mountPointsFormatter =
                    TabularOutput.TabularOutput
                        .For(
                            new TabularOutputSettings(environmentSettings.Environment),
                            environmentSettings.Components.OfType<IMountPointFactory>())
                        .DefineColumn(mp => mp.Id.ToString(), LocalizableStrings.MountPointFactories, showAlways: true)
                        .DefineColumn(mp => mp.GetType().FullName ?? string.Empty, LocalizableStrings.Type, showAlways: true)
                        .DefineColumn(mp => mp.GetType().GetTypeInfo().Assembly.FullName ?? string.Empty, LocalizableStrings.Assembly, showAlways: true);
            Reporter.Output.WriteLine(mountPointsFormatter.Layout());
            Reporter.Output.WriteLine();

            TabularOutput<IGenerator> generatorsFormatter =
              TabularOutput.TabularOutput
                  .For(
                      new TabularOutputSettings(environmentSettings.Environment),
                      environmentSettings.Components.OfType<IGenerator>())
                  .DefineColumn(g => g.Id.ToString(), LocalizableStrings.Generators, showAlways: true)
                  .DefineColumn(g => g.GetType().FullName ?? string.Empty, LocalizableStrings.Type, showAlways: true)
                  .DefineColumn(g => g.GetType().GetTypeInfo().Assembly.FullName ?? string.Empty, LocalizableStrings.Assembly, showAlways: true);
            Reporter.Output.WriteLine(generatorsFormatter.Layout());
            Reporter.Output.WriteLine();
        }

        private sealed class CommandAction : AsynchronousCommandLineAction
        {
            private readonly BaseCommand<TArgs, TDefinition> _command;

            public CommandAction(BaseCommand<TArgs, TDefinition> command) => _command = command;

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
            {
                TArgs args = _command.ParseContext(parseResult);
                using IEngineEnvironmentSettings environmentSettings = _command.CreateEnvironmentSettings(args, parseResult);
                using TemplatePackageManager templatePackageManager = new(environmentSettings);

                NewCommandStatus returnCode;

                try
                {
                    using (Timing.Over(environmentSettings.Host.Logger, "Execute"))
                    {
                        await HandleGlobalOptionsAsync(args, environmentSettings, templatePackageManager, cancellationToken).ConfigureAwait(false);
                        returnCode = await _command.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    AggregateException? ax = ex as AggregateException;

                    while (ax != null && ax.InnerExceptions.Count == 1 && ax.InnerException is not null)
                    {
                        ex = ax.InnerException;
                        ax = ex as AggregateException;
                    }

                    Reporter.Error.WriteLine(ex.Message.Bold().Red());

                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                        ax = ex as AggregateException;

                        while (ax != null && ax.InnerExceptions.Count == 1 && ax.InnerException is not null)
                        {
                            ex = ax.InnerException;
                            ax = ex as AggregateException;
                        }

                        Reporter.Error.WriteLine(ex.Message.Bold().Red());
                    }

                    if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                    {
                        Reporter.Error.WriteLine(ex.StackTrace.Bold().Red());
                    }
                    returnCode = NewCommandStatus.Unexpected;
                }

                if (returnCode != NewCommandStatus.Success)
                {
                    Reporter.Error.WriteLine();
                    Reporter.Error.WriteLine(LocalizableStrings.BaseCommand_ExitCodeHelp, (int)returnCode);
                }

                return (int)returnCode;
            }
        }
    }
}
