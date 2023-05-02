﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyUninstallCommand : BaseUninstallCommand
    {
        public LegacyUninstallCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(hostBuilder, "--uninstall")
        {
            this.IsHidden = true;
            this.AddAlias("-u");

            parentCommand.AddNoLegacyUsageValidators(this);
        }

        protected override Task<NewCommandStatus> ExecuteAsync(UninstallCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, InvocationContext context)
        {
            PrintDeprecationMessage<LegacyUninstallCommand, UninstallCommand>(args.ParseResult);
            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, context);
        }
    }
}
