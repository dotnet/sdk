// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

internal static class MuxerCommandSpecMaker
{
    internal static CommandSpec CreatePackageCommandSpecUsingMuxer(string commandPath, IEnumerable<string> commandArguments, IDictionary<string, string>? environment = null)
    {
        var arguments = new List<string>();
        var rollForwardArgument = commandArguments.Where(arg => arg.Equals("--allow-roll-forward", StringComparison.OrdinalIgnoreCase));
        if (rollForwardArgument.Any())
        {
            arguments.Add("--roll-forward");
            arguments.Add("Major");
        }

        arguments.Add(commandPath);
        var filteredCommandArgs = rollForwardArgument.Any()
            ? commandArguments.Except(rollForwardArgument)
            : commandArguments;
        arguments.AddRange(filteredCommandArgs);

        var host = new Muxer().MuxerPath;
        var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(arguments);
        return new CommandSpec(host, escapedArgs, environment);
    }
}
