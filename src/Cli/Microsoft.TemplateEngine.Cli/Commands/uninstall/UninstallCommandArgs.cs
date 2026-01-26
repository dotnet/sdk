// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal sealed class UninstallCommandArgs : GlobalArgs
    {
        public IReadOnlyList<string> TemplatePackages { get; }

        public UninstallCommandArgs(BaseUninstallCommand uninstallCommand, ParseResult parseResult)
            : base(parseResult)
        {
            TemplatePackages = parseResult.GetValue(uninstallCommand.Definition.NameArgument) ?? [];

            //workaround for --install source1 --install source2 case
            if (uninstallCommand is LegacyUninstallCommand && (TemplatePackages.Contains(uninstallCommand.Name) || uninstallCommand.Aliases.Any(alias => TemplatePackages.Contains(alias))))
            {
                TemplatePackages = TemplatePackages.Where(package => uninstallCommand.Name != package && !uninstallCommand.Aliases.Contains(package)).ToList();
            }
        }
    }
}
