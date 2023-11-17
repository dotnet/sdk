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
            var arguments = new List<string>();

            var muxer = new Muxer();

            var host = muxer.MuxerPath;
            if (host == null)
            {
                throw new Exception(LocalizableStrings.UnableToLocateDotnetMultiplexer);
            }
            IEnumerable<string> modifiedArguments = commandArguments;

            // Add --roll-forward argument first if exists
            if (commandArguments.Any(arg => arg.Equals("--roll-forward", StringComparison.OrdinalIgnoreCase)))
            {
                int index = commandArguments.ToList().IndexOf("--roll-forward");
                arguments.Add(commandArguments.ElementAt(index));
                arguments.Add(commandArguments.ElementAt(index + 1));
                modifiedArguments = commandArguments.Where((element, i) => i != index && i != index + 1);
            }

            arguments.Add(commandPath);

            if (modifiedArguments != null)
            {
                arguments.AddRange(modifiedArguments);
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
