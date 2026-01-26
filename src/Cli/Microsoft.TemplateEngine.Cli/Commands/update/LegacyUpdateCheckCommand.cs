// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class LegacyUpdateCheckCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, NewUpdateCheckLegacyCommandDefinition definition)
        : BaseUpdateCommand<NewUpdateCheckLegacyCommandDefinition>(hostBuilder, definition)
    {
        protected override Task<NewCommandStatus> ExecuteAsync(UpdateCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, ParseResult parseResult, CancellationToken cancellationToken)
        {
            PrintDeprecationMessage<LegacyUpdateCheckCommand, UpdateCommand>(args.ParseResult, additionalNewOptionSelector: c => c.Definition.CheckOnlyOption);
            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken);
        }
    }
}
