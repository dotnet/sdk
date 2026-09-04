// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Eventing.Reader;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    internal static class MuxerCommandSpecMaker
    {
        internal static CommandSpec CreatePackageCommandSpecUsingMuxer(
            string commandPath,
            IEnumerable<string> commandArguments)
        {
            var arguments = new List<string>();

            var muxer = new Muxer();

            var host = muxer.MuxerPath;

            if (host == null)
            {
                throw new Exception(LocalizableStrings.UnableToLocateDotnetMultiplexer);
            }

            var rollForwardArgument = (commandArguments ?? Enumerable.Empty<string>()).Where(arg => arg.Equals("--allow-roll-forward", StringComparison.OrdinalIgnoreCase));

            if (rollForwardArgument.Any())
            {
                arguments.Add("--roll-forward");
                arguments.Add("Major");
            }

            arguments.Add(commandPath);

            if (commandArguments != null)
            {
                if (rollForwardArgument.Any())
                {
                    arguments.AddRange(commandArguments.Except(rollForwardArgument));
                }
                else
                {
                    arguments.AddRange(commandArguments);
                }
            }
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
