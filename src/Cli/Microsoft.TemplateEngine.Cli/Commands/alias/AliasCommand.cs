﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Invocation;
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
            IsHidden = true;
            this.Add(new AliasAddCommand(hostBuilder));
            this.Add(new AliasShowCommand(hostBuilder));
        }

        protected override Task<NewCommandStatus> ExecuteAsync(
            AliasCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context) => throw new NotImplementedException();

        protected override AliasCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
