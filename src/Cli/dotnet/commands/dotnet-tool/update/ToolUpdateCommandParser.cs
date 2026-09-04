// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Update;
using LocalizableStrings = Microsoft.DotNet.Tools.Tool.Update.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class ToolUpdateCommandParser
    {
        public static readonly CliArgument<string> PackageIdArgument = new("packageId")
        {
            HelpName = LocalizableStrings.PackageIdArgumentName,
            Description = LocalizableStrings.PackageIdArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly CliOption<bool> UpdateAllOption = ToolAppliedOption.UpdateAllOption;

        public static readonly CliOption<bool> AllowPackageDowngradeOption = ToolInstallCommandParser.AllowPackageDowngradeOption;

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("update", LocalizableStrings.CommandDescription);

            command.Arguments.Add(PackageIdArgument);

            ToolInstallCommandParser.AddCommandOptions(command);
            command.Options.Add(AllowPackageDowngradeOption);
            command.Options.Add(UpdateAllOption);

            command.SetAction((parseResult) => new ToolUpdateCommand(parseResult).Execute());

            return command;
        }
    }
}

