// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.CommandFactory
{
    public class CompositeCommandResolver : ICommandResolver
    {
        private const string CommandResolveEvent = "commandresolution/commandresolved";
        private IList<ICommandResolver> _orderedCommandResolvers;

        public IEnumerable<ICommandResolver> OrderedCommandResolvers
        {
            get
            {
                return _orderedCommandResolvers;
            }
        }

        public CompositeCommandResolver()
        {
            _orderedCommandResolvers = new List<ICommandResolver>();
        }

        public void AddCommandResolver(ICommandResolver commandResolver)
        {
            _orderedCommandResolvers.Add(commandResolver);
        }

        public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
        {
            foreach (var commandResolver in _orderedCommandResolvers)
            {
                var commandSpec = commandResolver.Resolve(commandResolverArguments);

                if (commandSpec != null)
                {
                    TelemetryEventEntry.TrackEvent(CommandResolveEvent, new Dictionary<string, string>()
                    {
                        { "commandName", commandResolverArguments is null ? string.Empty : Sha256Hasher.HashWithNormalizedCasing(commandResolverArguments.CommandName) },
                        { "commandResolver", commandResolver.GetType().ToString() }
                    });

                    return commandSpec;
                }
            }

            return null;
        }
    }
}
