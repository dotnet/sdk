// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Utilities;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class CompositeCommandResolver : ICommandResolver
{
    private const string CommandResolveEvent = "commandresolution/commandresolved";
    private readonly IList<ICommandResolver> _orderedCommandResolvers;

    public IEnumerable<ICommandResolver> OrderedCommandResolvers
    {
        get
        {
            return _orderedCommandResolvers;
        }
    }

    public CompositeCommandResolver()
    {
        _orderedCommandResolvers = [];
    }

    public void AddCommandResolver(ICommandResolver commandResolver)
    {
        _orderedCommandResolvers.Add(commandResolver);
    }

    public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
    {
        foreach (var commandResolver in _orderedCommandResolvers)
        {
            using var resolverActivity = Activities.Source.StartActivity("resolve-command");
            resolverActivity?.AddTag("lookup.type", commandResolver.GetType().Name);
            var commandSpec = commandResolver.Resolve(commandResolverArguments);

            if (commandSpec != null)
            {
                resolverActivity?.AddTag("lookup.command", commandSpec.Path);
                resolverActivity?.AddTag("lookup.status", "found");
                TelemetryEventEntry.TrackEvent(CommandResolveEvent, new Dictionary<string, string>()
                {
                    { "commandName", commandResolverArguments is null ? string.Empty : Sha256Hasher.HashWithNormalizedCasing(commandResolverArguments.CommandName) },
                    { "commandResolver", commandResolver.GetType().ToString() }
                });

                return commandSpec;
            }
            else
            {
                resolverActivity?.AddTag("lookup.status", "notfound");
            }
        }

        return null;
    }
}
