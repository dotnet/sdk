// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class AliasCommand : BaseCommand<AliasCommandArgs>
    {
        internal AliasCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(hostBuilder, "alias", SymbolStrings.Command_Alias_Description)
        {
            Hidden = true;
            this.Add(new AliasAddCommand(hostBuilder));
            this.Add(new AliasShowCommand(hostBuilder));
        }

        protected override Task<NewCommandStatus> ExecuteAsync(
            AliasCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken) => throw new NotImplementedException();

        protected override AliasCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
