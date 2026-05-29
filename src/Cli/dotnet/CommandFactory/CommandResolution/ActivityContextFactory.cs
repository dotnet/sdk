// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
#if TARGET_WINDOWS
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
#endif

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public static class ActivityContextFactory
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

        var environment = new Dictionary<string, string>(capacity: 2);
#if TARGET_WINDOWS
        var propagationContext = new PropagationContext(activityContext, Baggage.Current);
        Propagators.DefaultTextMapPropagator.Inject(propagationContext, environment, WriteTraceStateIntoEnvironment);
#endif
        return environment;
    }

    private static void WriteTraceStateIntoEnvironment(Dictionary<string, string> environment, string key, string value)
    {
        switch (key)
        {
            case "traceparent":
                environment[Activities.TRACEPARENT] = value;
                break;
            case "tracestate":
                environment[Activities.TRACESTATE] = value;
                break;
        }
    }
}
