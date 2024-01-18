// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    internal static class MuxerCommandSpecMaker
    {
        internal static CommandSpec CreatePackageCommandSpecUsingMuxer(
            string commandPath,
            IEnumerable<string> commandArguments)
        {
            var muxer = new Muxer();
            var host = muxer.MuxerPath;

            if (host == null)
            {
                throw new Exception(LocalizableStrings.UnableToLocateDotnetMultiplexer);
            }

            var previousArg = string.Empty;

            // Group the arguments by if the previous argument or the current argument is --roll-forward.
            var argGroups = (commandArguments ?? [])
                .GroupBy(a => previousArg.Equals("--allow-roll-forward", StringComparison.OrdinalIgnoreCase)
                | (previousArg = a).Equals("--allow-roll-forward", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            string[] arguments = [..argGroups.Where(g => g.Key).SelectMany(g => g), commandPath, .. argGroups.Where(g => !g.Key).SelectMany(g => g)];
            return CreateCommandSpec(host, arguments);
        }

        private static CommandSpec CreateCommandSpec(
            string commandPath,
            IEnumerable<string> commandArguments)
        {
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(commandArguments);

            return new CommandSpec(commandPath, escapedArgs);
        }
    }
}
