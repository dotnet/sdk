// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime;

internal class RuntimeCommandParser
{
    private static readonly Command s_command = ConstructCommand();

    public static Command GetCommand()
    {
        return s_command;
    }

    private static Command ConstructCommand()
    {
        Command command = new("runtime", "Manage runtime installations");
        command.Subcommands.Add(RuntimeInstallCommandParser.GetRuntimeInstallCommand());

        return command;
    }
}
