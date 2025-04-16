﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class DetailsCommand : BaseCommand<DetailsCommandArgs>
    {
        internal DetailsCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(hostBuilder, "details", SymbolStrings.Command_Details_Description)
        {
            Arguments.Add(NameArgument);
            Options.Add(InteractiveOption);
            Options.Add(AddSourceOption);
        }

        internal static Argument<string> NameArgument { get; } = new("package-identifier")
        {
            Description = LocalizableStrings.DetailsCommand_Argument_PackageIdentifier,
            Arity = new ArgumentArity(1, 1)
        };

        // Option disabled until https://github.com/dotnet/templating/issues/6811 is solved
        //internal static Option<string> VersionOption { get; } = new("-version", "--version")
        //{
        //    Description = LocalizableStrings.DetailsCommand_Option_Version,
        //    Arity = new ArgumentArity(1, 1)
        //};

        internal virtual Option<bool> InteractiveOption { get; } = SharedOptions.InteractiveOption;

        internal virtual Option<string[]> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption();

        protected override async Task<NewCommandStatus> ExecuteAsync(
            DetailsCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            var templatePackageCoordinator = new TemplatePackageCoordinator(environmentSettings, templatePackageManager);

            NewCommandStatus status = await templatePackageCoordinator.DisplayTemplatePackageMetadata(
                args.NameCriteria,
                args.VersionCriteria,
                args.Interactive,
                args.AdditionalSources,
                new NugetApiManager(),
                cancellationToken).ConfigureAwait(false);

            await CheckTemplatesWithSubCommandName(args, templatePackageManager, cancellationToken).ConfigureAwait(false);
            return status;
        }

        protected override DetailsCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}
