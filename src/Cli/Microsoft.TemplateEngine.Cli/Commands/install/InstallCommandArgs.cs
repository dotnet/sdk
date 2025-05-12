// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using NuGet.Packaging.Core;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal interface ITemplateIdentifierArgument;

#pragma warning disable CS9113 // Parameter is unread.
    internal record FileBasedTemplateIdentifier(FileInfo File) : ITemplateIdentifierArgument;
#pragma warning restore CS9113 // Parameter is unread.

#pragma warning disable CS9113 // Parameter is unread.
    internal record PackageIdentifier(PackageIdentity Package) : ITemplateIdentifierArgument;
#pragma warning restore CS9113 // Parameter is unread.

    internal class InstallCommandArgs : GlobalArgs
    {
        public InstallCommandArgs(BaseInstallCommand installCommand, ParseResult parseResult) : base(installCommand, parseResult)
        {
            TemplatePackages = parseResult.GetValue(BaseInstallCommand.PackageIdentifierArgument)!;

            //workaround for --install source1 --install source2 case
            if (installCommand is LegacyInstallCommand)
            {
                TemplatePackages = TemplatePackages.Where(package =>
                {
                    if (package is PackageIdentifier packageIdentifier)
                    {
                        return packageIdentifier.Package.Id != installCommand.Name && !installCommand.Aliases.Contains(packageIdentifier.Package.Id);
                    }
                    return true;
                }).ToList();
            }

            if (!TemplatePackages.Any())
            {
                throw new ArgumentException($"{nameof(parseResult)} should contain at least one argument for {nameof(BaseInstallCommand.PackageIdentifierArgument)}", nameof(parseResult));
            }

            Interactive = parseResult.GetValue(installCommand.InteractiveOption);
            AdditionalSources = parseResult.GetValue(installCommand.AddSourceOption);
            Force = parseResult.GetValue(BaseInstallCommand.ForceOption);
        }

        public IReadOnlyList<ITemplateIdentifierArgument> TemplatePackages { get; }

        public bool Interactive { get; }

        public IReadOnlyList<string>? AdditionalSources { get; }

        public bool Force { get; }
    }
}
