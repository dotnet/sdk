// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Update;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk
{
    internal class SdkCommandParser
    {
        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            Command command = new("sdk", "Manage sdk installations");
            command.Subcommands.Add(SdkInstallCommandParser.GetSdkInstallCommand());
            command.Subcommands.Add(SdkUpdateCommandParser.GetSdkUpdateCommand());

            //command.SetAction((parseResult) => parseResult.HandleMissingCommand());

            return command;
        }
    }
}
