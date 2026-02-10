// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.Reflection;
using System.Text;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.DefaultInstall;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedAdminPath;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    internal class Parser
    {
        public static ParserConfiguration ParserConfiguration { get; } = new()
        {
            EnablePosixBundling = false,
            //ResponseFileTokenReplacer = TokenPerLine
        };

        public static InvocationConfiguration InvocationConfiguration { get; } = new()
        {
            //EnableDefaultExceptionHandler = false,
        };

        public static ParseResult Parse(string[] args) => RootCommand.Parse(args, ParserConfiguration);
        public static int Invoke(ParseResult parseResult) => parseResult.Invoke(InvocationConfiguration);
        public static int Invoke(string[] args) => Invoke(Parse(args));

        private static RootCommand RootCommand { get; } = ConfigureCommandLine(new()
        {
            Description = Strings.RootCommandDescription,
            Directives = { new DiagramDirective(), new SuggestDirective(), new EnvironmentVariablesDirective() }
        });

        /// <summary>
        /// Gets the version string from the dotnetup assembly.
        /// </summary>
        public static string Version { get; } = typeof(Parser).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        private static RootCommand ConfigureCommandLine(RootCommand rootCommand)
        {
            rootCommand.Subcommands.Add(InfoCommandParser.GetCommand());
            rootCommand.Subcommands.Add(SdkCommandParser.GetCommand());
            rootCommand.Subcommands.Add(RuntimeCommandParser.GetCommand());
            rootCommand.Subcommands.Add(SdkInstallCommandParser.GetRootInstallCommand());
            rootCommand.Subcommands.Add(SdkUpdateCommandParser.GetRootUpdateCommand());
            rootCommand.Subcommands.Add(ElevatedAdminPathCommandParser.GetCommand());
            rootCommand.Subcommands.Add(DefaultInstallCommandParser.GetCommand());
            rootCommand.Subcommands.Add(PrintEnvScriptCommandParser.GetCommand());
            rootCommand.Subcommands.Add(ListCommandParser.GetCommand());

            rootCommand.SetAction(parseResult =>
            {
                // No subcommand - show help
                parseResult.InvocationConfiguration.Output.WriteLine(Strings.RootCommandDescription);
                return 0;
            });

            return rootCommand;
        }
    }
}
