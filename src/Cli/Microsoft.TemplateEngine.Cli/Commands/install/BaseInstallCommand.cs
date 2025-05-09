// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseInstallCommand : BaseCommand<InstallCommandArgs>
    {
        internal BaseInstallCommand(
            NewCommand parentCommand,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string commandName)
            : base(hostBuilder, commandName, SymbolStrings.Command_Install_Description)
        {
            ParentCommand = parentCommand;
            Arguments.Add(PackageIdentifierArgument);
            Options.Add(InteractiveOption);
            Options.Add(AddSourceOption);
            Options.Add(ForceOption);
        }

        internal static Argument<ITemplateIdentifierArgument[]> PackageIdentifierArgument { get; } = new("package")
        {
            Description = SymbolStrings.Command_Install_Argument_Package,
            Arity = ArgumentArity.OneOrMore,
            CustomParser = ParseTemplateIdentifierArguments
        };

        private static ITemplateIdentifierArgument[]? ParseTemplateIdentifierArguments(ArgumentResult result)
        {
            var templateIdentifierArguments = new List<ITemplateIdentifierArgument>();
            foreach (var value in result.Tokens.Select(t => t.Value))
            {
                if (File.Exists(value))
                {
                    templateIdentifierArguments.Add(new FileBasedTemplateIdentifier(new FileInfo(value)));
                }
                else if (Directory.Exists(value) || Directory.Exists(Path.GetDirectoryName(value)))
                {
                    var environmentSettings = new EngineEnvironmentSettings(new Edge.DefaultTemplateEngineHost("dotnet", "1.0.0"), false, null, null, null, null);
                    // expand the value pattern to allow for globbing, etc:
                    foreach (var path in InstallRequestPathResolution.ExpandMaskedPath(value, environmentSettings))
                    {
                        templateIdentifierArguments.Add(new FileBasedTemplateIdentifier(new FileInfo(path)));
                    }
                }
                else if (value.Contains("::") && ParsePackageIdentityWithVersionSeparator(value, "::") is PackageIdentity packageIdentity)
                {
                    Console.WriteLine(string.Format(LocalizableStrings.Colon_Separator_Deprecated, packageIdentity.Id, packageIdentity.Version is not null ? packageIdentity.Version : string.Empty));
                    templateIdentifierArguments.Add(new PackageIdentifier(packageIdentity));
                }
                else if (ParsePackageIdentityWithVersionSeparator(value, "@") is PackageIdentity packageIdentityWithAt)
                {
                    templateIdentifierArguments.Add(new PackageIdentifier(packageIdentityWithAt));
                }
            }

            if (templateIdentifierArguments.Count == 0) // we have an arity of One or More, so code is expecting that we error before we call the command...
            {
                result.AddError("No packages were found for installation. Please provide a valid package identity or path to a template package.");
                return null;
            }

            return templateIdentifierArguments.ToArray();
        }

        private static PackageIdentity? ParsePackageIdentityWithVersionSeparator(string packageIdentity, string versionSeparator)
        {
            if (string.IsNullOrEmpty(packageIdentity))
            {
                return null;
            }

            string[] splitPackageIdentity = packageIdentity.Split(versionSeparator);
            var (packageId, versionString) = (splitPackageIdentity.ElementAtOrDefault(0), splitPackageIdentity.ElementAtOrDefault(1));

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentException("package identity cannot be null or empty", nameof(packageId));
            }

            if (string.IsNullOrEmpty(versionString))
            {
                return new PackageIdentity(packageId, null);
            }

            if (!NuGetVersion.TryParse(versionString, out var version))
            {
                throw new ArgumentException($"Version {versionString} is not a valid Semantic Version");
            }

            return new PackageIdentity(packageId, new NuGetVersion(version));
        }

#pragma warning disable SA1201 // Elements should appear in the correct order
        internal static Option<bool> ForceOption { get; } = SharedOptionsFactory.CreateForceOption().WithDescription(SymbolStrings.Option_Install_Force);
#pragma warning restore SA1201 // Elements should appear in the correct order

        internal virtual Option<bool> InteractiveOption { get; } = SharedOptions.InteractiveOption;

        internal virtual Option<string[]> AddSourceOption { get; } = SharedOptionsFactory.CreateAddSourceOption();

        protected NewCommand ParentCommand { get; }

        protected override Task<NewCommandStatus> ExecuteAsync(
            InstallCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            TemplatePackageCoordinator templatePackageCoordinator = new(environmentSettings, templatePackageManager);
            return templatePackageCoordinator.EnterInstallFlowAsync(args, cancellationToken);
        }

        protected override InstallCommandArgs ParseContext(ParseResult parseResult)
        {
            return new InstallCommandArgs(this, parseResult);
        }
    }
}
