// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public static class ActivityContext
{
    public static Dictionary<string, string>? MakeActivityContextEnvironment()
    {
        var currentActivity = Activity.Current;
        if (currentActivity is null)
        {
            return null;
        }
        var activityContext = currentActivity.Context;
        if (activityContext.TraceState is null && activityContext.TraceId == default && activityContext.SpanId == default)
        {
            return null;
        }
        var propagationContext = new PropagationContext(activityContext, Baggage.Current);
        var environment = new Dictionary<string, string>(capacity: 2);
        Propagators.DefaultTextMapPropagator.Inject(propagationContext, environment, WriteTraceStateIntoEnvironment);
        return environment;
    }

    private static void WriteTraceStateIntoEnvironment(Dictionary<string, string> environment, string key, string value)
    {
        var environmentKey = key switch
        {
            "traceparent" => Activities.TRACEPARENT,
            "tracestate" => Activities.TRACESTATE,
            _ => null
        };

        if (environmentKey == null)
        {
            return;
        }

        environment[environmentKey] = value;
    }
}
