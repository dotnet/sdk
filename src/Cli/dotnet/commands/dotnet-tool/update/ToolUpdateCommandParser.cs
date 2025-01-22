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
        public static readonly Argument<string> PackageIdArgument = new("packageId")
        {
            HelpName = LocalizableStrings.PackageIdArgumentName,
            Description = LocalizableStrings.PackageIdArgumentDescription,
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> UpdateAllOption = ToolAppliedOption.UpdateAllOption;

        public static readonly Option<bool> AllowPackageDowngradeOption = ToolInstallCommandParser.AllowPackageDowngradeOption;

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("update", LocalizableStrings.CommandDescription);

            command.Arguments.Add(PackageIdArgument);

            ToolInstallCommandParser.AddCommandOptions(command);
            command.Options.Add(AllowPackageDowngradeOption);
            command.Options.Add(UpdateAllOption);

            command.SetAction((parseResult) => new ToolUpdateCommand(parseResult).Execute());

            return command;
        }
    }
}

