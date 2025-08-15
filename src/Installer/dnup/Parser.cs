// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Completions;
using System.Text;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk;

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

        private static RootCommand RootCommand { get; } = ConfigureCommandLine(new()
        {
            Directives = { new DiagramDirective(), new SuggestDirective(), new EnvironmentVariablesDirective() }
        });

        private static RootCommand ConfigureCommandLine(RootCommand rootCommand)
        {
            rootCommand.Subcommands.Add(SdkCommandParser.GetCommand());

            return rootCommand;
        }
    }
}
