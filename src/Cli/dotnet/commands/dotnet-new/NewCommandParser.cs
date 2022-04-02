// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Tools.New;

namespace Microsoft.DotNet.Cli
{
    internal static class NewCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-new";

        public static readonly Argument Argument = new Argument<IEnumerable<string>>() { Arity = ArgumentArity.ZeroOrMore };

        private static readonly Option<bool> _disableSdkTemplates = new Option<bool>("--debug:disable-sdk-templates", () => false, "If present, prevents templates bundled in the SDK from being presented").Hide();

        internal static readonly System.CommandLine.Command Command = GetCommand();

        public static System.CommandLine.Command GetCommand()
        {
            var getLogger = (ParseResult parseResult) => {
                var sessionId = Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

                // senderCount: 0 to disable sender.
                // When senders in different process running at the same
                // time they will read from the same global queue and cause
                // sending duplicated events. Disable sender to reduce it.
                var telemetry = new Microsoft.DotNet.Cli.Telemetry.Telemetry(new FirstTimeUseNoticeSentinel(),
                                            sessionId,
                                            senderCount: 0);
                var logger = new TelemetryLogger(null);

                if (telemetry.Enabled)
                {
                    logger = new TelemetryLogger((name, props, measures) =>
                    {
                        if (telemetry.Enabled)
                        {
                            telemetry.TrackEvent($"template/{name}", props, measures);
                        }
                    });
                }
                return logger;
            };

            var callbacks = new Microsoft.TemplateEngine.Cli.NewCommandCallbacks()
            {
                RestoreProject = RestoreProject
            };

            var getEngineHost = (ParseResult parseResult) => {
                var disableSdkTemplates = parseResult.GetValueForOption(_disableSdkTemplates);
                return CreateHost(disableSdkTemplates);
            };

            var command = Microsoft.TemplateEngine.Cli.NewCommandFactory.Create(CommandName, getEngineHost, getLogger, callbacks);

            // adding this option lets us look for its bound value during binding in a typed way
            command.AddGlobalOption(_disableSdkTemplates);
            return command;
        }

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("new", DocsLink);

            command.AddArgument(Argument);
            command.AddOption(ListOption);
            command.AddOption(NameOption);
            command.AddOption(OutputOption);
            command.AddOption(InstallOption);
            command.AddOption(UninstallOption);
            command.AddOption(InteractiveOption);
            command.AddOption(NuGetSourceOption);
            command.AddOption(TypeOption);
            command.AddOption(DryRunOption);
            command.AddOption(ForceOption);
            command.AddOption(LanguageOption);
            command.AddOption(UpdateCheckOption);
            command.AddOption(UpdateApplyOption);
            command.AddOption(ColumnsOption);

            command.SetHandler((ParseResult parseResult) => NewCommandShim.Run(parseResult.GetArguments()));

            return command;
        }
    }
}
