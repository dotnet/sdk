// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class RootedCommandResolver : ICommandResolver
{
    public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
    {
        if (commandResolverArguments.CommandName == null)
        {
            return null;
        }

        if (Path.IsPathRooted(commandResolverArguments.CommandName))
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                commandResolverArguments.CommandArguments.OrEmptyIfNull());

            return new CommandSpec(commandResolverArguments.CommandName, escapedArgs);
        }

        return null;
    }
}
