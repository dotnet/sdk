// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Tools.Configure.Add;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
//using Microsoft.DotNet.Tools.Sln.Add;
using LocalizableStrings = Microsoft.DotNet.Tools.Configure.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    public static class ConfigureAddParser
    {
        public static readonly Option<string> ConfigName = new Option<string>(new string[] { "-c", "--config" }, LocalizableStrings.ConfigureAddOptionNewConfigName);

        public static readonly Option<string> PlatformName = new Option<string>(new string[] { "-p", "--platform" }, LocalizableStrings.ConfigureAddOptionNewPlatformName);

        public static readonly Option<string> UpdateProject = new Option<string>(new string[] { "-up", "--updateproj" }, LocalizableStrings.ConfigureAddOptionUpdateProjects);

        public static readonly Option<string> CopyFromConfig = new Option<string>(new string[] { "-cf", "--copyfrom" }, LocalizableStrings.ConfigureAddOptionCopySettingsFrom);

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new Command("add", LocalizableStrings.ConfigureAddCommand);

            command.AddOption(ConfigName);
            command.AddOption(PlatformName);
            command.AddOption(CopyFromConfig);
            command.AddOption(UpdateProject);

            command.SetHandler((parseResult) => new AddConfigToSolutionCommand(parseResult).Execute());

            return command;
        }
    }
}
