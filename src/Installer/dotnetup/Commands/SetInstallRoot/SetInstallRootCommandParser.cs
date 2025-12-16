// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.SetInstallRoot;

internal static class SetInstallRootCommandParser
{
    public static readonly Argument<string> InstallTypeArgument = new("installtype")
    {
        HelpName = "INSTALL_TYPE",
        Description = "The type of installation root to set: 'user'",
        Arity = ArgumentArity.ExactlyOne,
    };

    private static readonly Command SetInstallRootCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return SetInstallRootCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("setinstallroot", "Sets the dotnet installation root");

        command.Arguments.Add(InstallTypeArgument);

        command.SetAction(parseResult => new SetInstallRootCommand(parseResult).Execute());

        return command;
    }
}
