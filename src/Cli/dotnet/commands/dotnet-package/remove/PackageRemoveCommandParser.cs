// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Package.Remove;

namespace Microsoft.DotNet.Cli
{
    internal static class PackageRemoveCommandParser
    {
        public static readonly CliArgument<IEnumerable<string>> CmdPackageArgument = new(Tools.Package.Add.LocalizableStrings.CmdPackage)
        {
            Description = LocalizableStrings.AppHelpText,
            Arity = ArgumentArity.OneOrMore,
        };

        public static readonly CliOption<bool> InteractiveOption = new ForwardedOption<bool>("--interactive")
        {
            Description = CommonLocalizableStrings.CommandInteractiveOptionDescription
        }.ForwardAs("--interactive");

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            var command = new CliCommand("remove", LocalizableStrings.AppFullName);

            command.Arguments.Add(CmdPackageArgument);
            command.Options.Add(InteractiveOption);
            command.Options.Add(PackageCommandParser.ProjectOption);

            command.SetAction((parseResult) => new RemovePackageReferenceCommand(parseResult).Execute());

            return command;
        }
    }
}
