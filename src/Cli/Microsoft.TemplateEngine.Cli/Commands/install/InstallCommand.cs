// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class InstallCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, NewInstallCommandDefinition definition)
        : BaseInstallCommand(hostBuilder, definition)
    {
        protected override async Task<NewCommandStatus> ExecuteAsync(
            InstallCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            NewCommandStatus status = await base.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken).ConfigureAwait(false);
            await CheckTemplatesWithSubCommandName(args, templatePackageManager, cancellationToken).ConfigureAwait(false);
            return status;
        }
    }
}
