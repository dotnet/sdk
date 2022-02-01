// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Format;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.Tools.MSBuild;
using Microsoft.DotNet.Tools.New;
using Microsoft.DotNet.Tools.NuGet;

namespace Microsoft.DotNet.Cli
{
    public static class Parser
    {
        public static readonly RootCommand RootCommand = new ();

        internal static Dictionary<Option, Dictionary<System.CommandLine.Command, string>> HelpDescriptionCustomizations = new ();

        public static readonly System.CommandLine.Command InstallSuccessCommand = InternalReportinstallsuccessCommandParser.GetCommand();

        // Subcommands
        public static readonly System.CommandLine.Command[] Subcommands = new []
        {
            AddCommandParser.GetCommand(),
            BuildCommandParser.GetCommand(),
            BuildServerCommandParser.GetCommand(),
            CleanCommandParser.GetCommand(),
            FormatCommandParser.GetCommand(),
            CompleteCommandParser.GetCommand(),
            FsiCommandParser.GetCommand(),
            ListCommandParser.GetCommand(),
            MSBuildCommandParser.GetCommand(),
            NewCommandParser.GetCommand(),
            NuGetCommandParser.GetCommand(),
            PackCommandParser.GetCommand(),
            ParseCommandParser.GetCommand(),
            PublishCommandParser.GetCommand(),
            RemoveCommandParser.GetCommand(),
            RestoreCommandParser.GetCommand(),
            RunCommandParser.GetCommand(),
            SlnCommandParser.GetCommand(),
            StoreCommandParser.GetCommand(),
            TestCommandParser.GetCommand(),
            ToolCommandParser.GetCommand(),
            VSTestCommandParser.GetCommand(),
            HelpCommandParser.GetCommand(),
            SdkCommandParser.GetCommand(),
            InstallSuccessCommand,
            WorkloadCommandParser.GetCommand()
        };

        // Options
        public static readonly Option<bool> DiagOption = new Option<bool>(new[] { "-d", "--diagnostics" });

        public static readonly Option<bool> VersionOption = new Option<bool>("--version");

        public static readonly Option<bool> InfoOption = new Option<bool>("--info");

        public static readonly Option<bool> ListSdksOption = new Option<bool>("--list-sdks");

        public static readonly Option<bool> ListRuntimesOption = new Option<bool>("--list-runtimes");

        // Argument
        public static readonly Argument<string> DotnetSubCommand = new Argument<string>() { Arity = ArgumentArity.ExactlyOne, IsHidden = true };

        private static System.CommandLine.Command ConfigureCommandLine(System.CommandLine.Command rootCommand)
        {
            // Add subcommands
            foreach (var subcommand in Subcommands)
            {
                rootCommand.AddCommand(subcommand);
            }

            // Add options
            rootCommand.AddOption(DiagOption);
            rootCommand.AddOption(VersionOption);
            rootCommand.AddOption(InfoOption);
            rootCommand.AddOption(ListSdksOption);
            rootCommand.AddOption(ListRuntimesOption);

            // Add argument
            rootCommand.AddArgument(DotnetSubCommand);

            return rootCommand;
        }

        private static CommandLineBuilder DisablePosixBinding(this CommandLineBuilder builder)
        {
            builder.EnablePosixBundling = false;
            return builder;
        }

        public static System.CommandLine.Command GetBuiltInCommand(string commandName)
        {
            return Subcommands
                .FirstOrDefault(c => c.Name.Equals(commandName, StringComparison.OrdinalIgnoreCase));
        }

        public static System.CommandLine.Parsing.Parser Instance { get; } = new CommandLineBuilder(ConfigureCommandLine(RootCommand))
            .UseExceptionHandler(ExceptionHandler)
            .UseHelp()
            .UseHelpBuilder(context => DotnetHelpBuilder.Instance.Value)
            .UseMissingCommandErrorReporting()
            .UseParseErrorReporting(1)
            .UseLocalizationResources(new CommandLineValidationMessages())
            .UseParseDirective()
            .UseSuggestDirective()
            .DisablePosixBinding()
            .EnableLegacyDoubleDashBehavior()
            .Build();

        class HelpResult : IInvocationResult
        {
            public void Apply(InvocationContext context)
            {
                TextWriter output = context.Console.Out.CreateTextWriter();
                var helpBuilder = (HelpBuilder)context.BindingContext.GetService(typeof(HelpBuilder));
                var console = context.Console as SystemConsole;
                var width = (console?.IsOutputRedirected ?? false) ? int.MaxValue : Console.WindowWidth;
                var helpContext = new HelpContext(helpBuilder, context.ParseResult.CommandResult.Command, output, context.ParseResult);
                helpBuilder.Write(helpContext);
            }
        }

        private static CommandLineBuilder UseMissingCommandErrorReporting(this CommandLineBuilder builder) =>
            builder.AddMiddleware(
                (ctx, next) => {
                    if (ctx.ParseResult.CommandResult.Command.Handler is {} handler) {
                        return next.Invoke(ctx);
                    }
                    // TODO:CH - use S.CL Console from the ctx here. This is blocked by wrapping the reporter in S.CL's 
                    // IConsole, since we want to maintain the text styling present in AnsiConsole
                    Reporter.Error.WriteLine(Tools.CommonLocalizableStrings.RequiredCommandNotPassed.Red());
                    ctx.InvocationResult = new HelpResult();
                    ctx.ExitCode = 1;
                    return System.Threading.Tasks.Task.CompletedTask;
                }
            , MiddlewareOrder.ErrorReporting);

        private static void ExceptionHandler(Exception exception, InvocationContext context)
        {
            if (exception is TargetInvocationException)
            {
                exception = exception.InnerException;
            }

            if (exception is Utils.GracefulException)
            {
                context.Console.Error.WriteLine(exception.Message);
            }
            else if (exception is CommandParsingException)
            {
                context.Console.Error.WriteLine(exception.Message);
            }
            else if (exception.ShouldBeDisplayedAsError())
            {
                Reporter.Error.WriteLine(CommandContext.IsVerbose()
                        ? exception.ToString().Red().Bold()
                        : exception.Message.Red().Bold());
            }
            else if (!exception.ShouldBeDisplayedAsError())
            {
                // If telemetry object has not been initialized yet. It cannot be collected
                TelemetryEventEntry.SendFiltered(exception);
                // TODO:CH - use S.CL Console from the ctx here. This is blocked by wrapping the reporter in S.CL's 
                // IConsole, since we want to maintain the text styling present in AnsiConsole
                Reporter.Error.WriteLine(exception.ToString().Red().Bold());
                context.ExitCode = 1;
                // explicitly do not fall through to show help for these errors
                return;
            }
            else 
            {
                context.Console.Error.Write("Unhandled exception: ");
                context.Console.Error.WriteLine(exception.ToString());
            }

            context.InvocationResult = new HelpResult();
            context.ExitCode = 1;
        }

        internal class CommandLineConsole : IConsole
        {
            public IStandardStreamWriter Out => StandardStreamWriter.Create(Console.Out);

            public bool IsOutputRedirected => Console.IsOutputRedirected;

            public IStandardStreamWriter Error => StandardStreamWriter.Create(Console.Error);

            public bool IsErrorRedirected => Console.IsErrorRedirected;

            public bool IsInputRedirected => Console.IsInputRedirected;
        }

        internal class DotnetHelpBuilder : HelpBuilder
        {
            private DotnetHelpBuilder(int maxWidth = int.MaxValue) : base(LocalizationResources.Instance, maxWidth) { }

            public static Lazy<HelpBuilder> Instance = new Lazy<HelpBuilder>(() => {
                int windowWidth;
                try
                {
                    windowWidth = Console.WindowWidth;
                }
                catch
                {
                    windowWidth = int.MaxValue;
                }

                DotnetHelpBuilder dotnetHelpBuilder = new DotnetHelpBuilder(windowWidth);
                dotnetHelpBuilder.CustomizeSymbol(FormatCommandCommon.DiagnosticsOption, defaultValue: Tools.Format.LocalizableStrings.whichever_ids_are_listed_in_the_editorconfig_file);
                dotnetHelpBuilder.CustomizeSymbol(FormatCommandCommon.IncludeOption, defaultValue: Tools.Format.LocalizableStrings.all_files_in_the_solution_or_project);
                dotnetHelpBuilder.CustomizeSymbol(FormatCommandCommon.ExcludeOption, defaultValue: Tools.Format.LocalizableStrings.none);

                SetHelpCustomizations(dotnetHelpBuilder);

                return dotnetHelpBuilder;
            });

            private static void SetHelpCustomizations(HelpBuilder builder)
            {
                foreach (var option in HelpDescriptionCustomizations.Keys)
                {
                    Func<HelpContext, string> descriptionCallback = (HelpContext context) =>
                    {
                        foreach (var (command, helpText) in HelpDescriptionCustomizations[option])
                        {
                            if (context.ParseResult.CommandResult.Command.Equals(command))
                            {
                                return helpText;
                            }
                        }
                        return null;
                    };
                    builder.CustomizeSymbol(option, secondColumnText: descriptionCallback);
                }
            }

            public override void Write(HelpContext context)
            {
                var command = context.Command;
                var helpArgs = new string[] { "--help" };
                if (command.Equals(RootCommand))
                {
                    Console.Out.WriteLine(HelpUsageText.UsageText);
                }
                else if (command.Name.Equals(NuGetCommandParser.GetCommand().Name))
                {
                    NuGetCommand.Run(helpArgs);
                }
                else if (command.Name.Equals(MSBuildCommandParser.GetCommand().Name))
                {
                    new MSBuildForwardingApp(helpArgs).Execute();
                }
                else if (command.Name.Equals(NewCommandParser.GetCommand().Name))
                {
                    NewCommandShim.Run(context.ParseResult.GetArguments());
                }
                else if (command.Name.Equals(VSTestCommandParser.GetCommand().Name))
                {
                    new VSTestForwardingApp(helpArgs).Execute();
                }
                else
                {
                    if (command.Name.Equals(ListProjectToProjectReferencesCommandParser.GetCommand().Name))
                    {
                        ListCommandParser.SlnOrProjectArgument.Name = CommonLocalizableStrings.ProjectArgumentName;
                        ListCommandParser.SlnOrProjectArgument.Description = CommonLocalizableStrings.ProjectArgumentDescription;
                    }
                    else if (command.Name.Equals(AddPackageParser.GetCommand().Name) || command.Name.Equals(AddCommandParser.GetCommand().Name))
                    {
                        // Don't show package completions in help
                        AddPackageParser.CmdPackageArgument.Completions.Clear();
                    }

                    base.Write(context);
                }
            }
        }
    }
}
