﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseUninstallCommand : BaseCommand<UninstallCommandArgs>
    {
        internal BaseUninstallCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName)
            : base(hostBuilder, commandName, SymbolStrings.Command_Uninstall_Description)
        {
            this.AddArgument(NameArgument);
        }

        internal static Argument<string[]> NameArgument { get; } = new("package")
        {
            Description = SymbolStrings.Command_Uninstall_Argument_Package,
            Arity = new ArgumentArity(0, 99)
        };

        protected override Task<NewCommandStatus> ExecuteAsync(
            UninstallCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context)
        {
            TemplatePackageCoordinator templatePackageCoordinator = new TemplatePackageCoordinator(
                environmentSettings,
                templatePackageManager);

            return templatePackageCoordinator.EnterUninstallFlowAsync(args, context.GetCancellationToken());
        }

        protected override UninstallCommandArgs ParseContext(ParseResult parseResult)
        {
            return new UninstallCommandArgs(this, parseResult);
        }
    }
}
