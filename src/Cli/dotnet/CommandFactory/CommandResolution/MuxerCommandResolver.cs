// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class MuxerCommandResolver : ICommandResolver
{
    public CommandSpec? Resolve(CommandResolverArguments commandResolverArguments)
    {
        if (commandResolverArguments.CommandName == Muxer.MuxerName)
        {
            var muxer = new Muxer();
            var escapedArgs = ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(
                commandResolverArguments.CommandArguments.OrEmptyIfNull());
            var env = MakeActivityContextEnvironment();
            return new CommandSpec(muxer.MuxerPath, escapedArgs, env);
        }
        return null;
    }

    private Dictionary<string, string>? MakeActivityContextEnvironment()
    {
        var currentActivity = Activity.Current;
        var currentBaggage = Baggage.Current;
        if (currentActivity == null)
        {
            return null;
        }
        var contextToInject = currentActivity.Context;
        var propagationContext = new PropagationContext(contextToInject, currentBaggage);
        var envDict = new Dictionary<string, string>();
        Propagators.DefaultTextMapPropagator.Inject(propagationContext, envDict, WriteTraceStateIntoEnv);
        return envDict;
    }

    private void WriteTraceStateIntoEnv(Dictionary<string, string> dictionary, string key, string value)
    {
        switch (key)
        {
            case "traceparent":
                dictionary[Activities.DOTNET_CLI_TRACEPARENT] = value;
                break;
            case "tracestate":
                dictionary[Activities.DOTNET_CLI_TRACESTATE] = value;
                break;
        }
    }
}
