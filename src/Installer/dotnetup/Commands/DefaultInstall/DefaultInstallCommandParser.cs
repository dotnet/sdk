// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.DefaultInstall;

internal static class DefaultInstallCommandParser
{
    public const string UserInstallType = "user";
    public const string AdminInstallType = "admin";

    public static readonly Argument<string> InstallTypeArgument = CreateInstallTypeArgument();

    private static Argument<string> CreateInstallTypeArgument()
    {
        var argument = new Argument<string>("installtype")
        {
            HelpName = "INSTALL_TYPE",
            Description = $"The type of installation root to set: '{UserInstallType}' or '{AdminInstallType}'",
            Arity = ArgumentArity.ExactlyOne,
        };
        argument.AcceptOnlyFromAmong(UserInstallType, AdminInstallType);
        return argument;
    }

    private static readonly Command DefaultInstallCommand = ConstructCommand();

    public static Command GetCommand()
    {
        return DefaultInstallCommand;
    }

    private static Command ConstructCommand()
    {
        Command command = new("defaultinstall", "Sets the default dotnet installation");

        command.Arguments.Add(InstallTypeArgument);

        command.SetAction(parseResult => new DefaultInstallCommand(parseResult).Execute());

        return command;
    }
}
