// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool;

internal class ToolCommandSpecCreator
{
    public static CommandSpec CreateToolCommandSpec(string toolName, string toolExecutable, string toolRunner, bool allowRollForward, IEnumerable<string> commandArguments)
    {
        if (toolRunner == "dotnet")
        {
            if (allowRollForward)
            {
                commandArguments = ["--allow-roll-forward", .. commandArguments];
            }

            return MuxerCommandSpecMaker.CreatePackageCommandSpecUsingMuxer(
                toolExecutable,
                commandArguments);
        }
        else if (toolRunner == "executable")
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                commandArguments);

            return new CommandSpec(
                toolExecutable,
                escapedArgs);
        }
        else
        {
            throw new GracefulException(string.Format(CliStrings.ToolSettingsUnsupportedRunner,
                toolName, toolRunner));
        }
    }
}
