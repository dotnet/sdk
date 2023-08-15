// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Cli;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
//using Microsoft.DotNet.Tools.Sln.Add;
using LocalizableStrings = Microsoft.DotNet.Tools.Sln.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class ConfigurationAddParser
    {
        public static readonly CliOption<string> ConfigName = new CliOption<string>("-c", "--config")
        {
            Description = LocalizableStrings.ConfigurationAddOptionNewConfigName
        };

        public static readonly CliOption<string> PlatformName = new CliOption<string>("-p", "--platform")
        {
            Description = LocalizableStrings.ConfigurationAddOptionNewPlatformName
        };

        public static readonly CliOption<string> UpdateProject = new CliOption<string>("-up", "--updateproj")
        {
            Description = LocalizableStrings.ConfigurationAddOptionUpdateProjects
        };

        public static readonly CliOption<string> CopyFromConfig = new CliOption<string>("-cf", "--copyfrom")
        {
            Description = LocalizableStrings.ConfigurationAddOptionCopySettingsFrom
        };

        private static readonly CliCommand Command = ConstructCommand();

        public static CliCommand GetCommand()
        {
            return Command;
        }

        private static CliCommand ConstructCommand()
        {
            CliCommand command = new("add", LocalizableStrings.ConfigurationAddCommand);

            command.Options.Add(ConfigName);
            command.Options.Add(PlatformName);
            command.Options.Add(CopyFromConfig);
            command.Options.Add(UpdateProject);

            command.SetAction((parseResult) => new AddConfigToSolutionCommand(parseResult).Execute());

            return command;
        }
    }
}
