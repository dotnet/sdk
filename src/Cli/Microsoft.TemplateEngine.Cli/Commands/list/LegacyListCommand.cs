// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.New;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class LegacyListCommand(Func<ParseResult, ITemplateEngineHost> hostBuilder, NewListCommandDefinition definition)
        : BaseListCommand(hostBuilder, definition)
    {
        protected override Task<NewCommandStatus> ExecuteAsync(ListCommandArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, ParseResult parseResult, CancellationToken cancellationToken)
        {
            // JSON output must contain only the JSON document, so the deprecation notice is suppressed.
            if (args.Format != NewListOutputFormat.json)
            {
                PrintDeprecationMessage<LegacyListCommand, ListCommand>(args.ParseResult);
            }

            return base.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken);
        }
    }
}
