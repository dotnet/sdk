// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class BaseDetailsCommand : BaseCommand<DetailsCommandArgs>
    {
        private static NugetApiManager _nugetApiManager = new NugetApiManager();

        internal BaseDetailsCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName)
            : base(hostBuilder, commandName, SymbolStrings.Command_Details_Description)
        {
            AddArgument(NameArgument);
            AddOption(VersionOption);
        }

        internal static Argument<string> NameArgument { get; } = new("package-identifier")
        {
            Description = SymbolStrings.Command_Details_Argument_PackageIdentifier,
            Arity = new ArgumentArity(1, 1)
        };

        internal static Option<string> VersionOption { get; } = new Option<string>(new string[] { "-version", "--version" })
        {
            Description = SymbolStrings.Command_Details_Option_Version,
            Arity = new ArgumentArity(0, 1)
        };

        protected async override Task<NewCommandStatus> ExecuteAsync(
            DetailsCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            InvocationContext context)
        {
            var packageIdentity = args.NameCriteria ?? string.Empty;
            var templatePackageCoordinator = new TemplatePackageCoordinator(environmentSettings, templatePackageManager);

            return await templatePackageCoordinator.DisplayTemplatePackageMetadata(
                packageIdentity,
                args.VersionCriteria,
                _nugetApiManager,
                context.GetCancellationToken()).ConfigureAwait(false);
        }

        protected override DetailsCommandArgs ParseContext(ParseResult parseResult) => new DetailsCommandArgs(this, parseResult);
    }
}
