﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyUpdateCheckCommand : BaseUpdateCommand
    {
        public LegacyUpdateCheckCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder,
            NewCommandCallbacks callbacks)
            : base(parentCommand, hostBuilder, telemetryLoggerBuilder, callbacks, "--update-check", SymbolStrings.Command_Update_Description)
        {
            this.IsHidden = true;
            parentCommand.AddNoLegacyUsageValidators(this, except: new Option[] { InteractiveOption, AddSourceOption });
        }

        internal override Option<bool> InteractiveOption => ParentCommand.InteractiveOption;

        internal override Option<string[]> AddSourceOption => ParentCommand.AddSourceOption;

        protected override Task<NewCommandStatus> ExecuteAsync(UpdateCommandArgs args, IEngineEnvironmentSettings environmentSettings, ITelemetryLogger telemetryLogger, InvocationContext context)
        {
            PrintDeprecationMessage<LegacyUpdateCheckCommand, UpdateCommand>(args.ParseResult, additionalOption: UpdateCommand.CheckOnlyOption);

            return base.ExecuteAsync(args, environmentSettings, telemetryLogger, context);
        }
    }
}
