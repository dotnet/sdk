// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLineValidation;
using Microsoft.DotNet.Tools.Common;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tests.ParserTests
{
    public class BuildRelatedCommandParserTests
    {

        /// <summary>
        /// These commands all implicitly use MSBuild under the covers and generally should expose
        /// the same set of property- and behavior-impacting options.
        /// </summary>
        private static string[] BuildRelatedCommands = [
            "build",
            "clean",
            "pack",
            "publish",
            "restore",
            "run",
            "test"
        ];

        private static string[] OptionsToVerify = [
            "--artifacts-path"
        ];

        public static TheoryData<string, string> BuildRelatedCommandsAndOptions()
        {
            var data = new TheoryData<string, string>();
            foreach (var cmd in BuildRelatedCommands)
            {
                foreach (var opt in OptionsToVerify)
                {
                    data.Add(cmd, opt);
                }
            }
            return data;
        }

        [MemberData(nameof(BuildRelatedCommandsAndOptions))]
        [Theory]
        public void Build(string command, string option)
        {
            var cliCommand = Parser.Instance.RootCommand.Children.OfType<CliCommand>().FirstOrDefault(c => c.Name == command);
            if (cliCommand is null)
            {
                throw new ArgumentException($"Command {command} not found in the dotnet CLI");
            }
            var cliOption = cliCommand.Children.OfType<CliOption>().FirstOrDefault(o => o.Name == option || o.Aliases.Contains(option));
            if (cliOption is null)
            {
                throw new ArgumentException($"Option {option} not found in the {command} command");
            }
        }
    }
}
