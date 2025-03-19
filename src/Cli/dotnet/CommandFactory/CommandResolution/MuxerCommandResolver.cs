// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class MuxerCommandResolver : ICommandResolver
{
    public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
    {
        if (commandResolverArguments.CommandName == Muxer.MuxerName)
        {
            var muxer = new Muxer();
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                commandResolverArguments.CommandArguments.OrEmptyIfNull());
            return new CommandSpec(muxer.MuxerPath, escapedArgs);
        }
        return null;
    }
}
