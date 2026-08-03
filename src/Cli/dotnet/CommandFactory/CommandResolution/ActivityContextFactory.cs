// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

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

        var environment = new Dictionary<string, string>(capacity: 2)
        {
            [Activities.TRACEPARENT] = $"00-{activityContext.TraceId}-{activityContext.SpanId}-{(activityContext.TraceFlags == ActivityTraceFlags.Recorded ? "01" : "00")}"
        };

        if (!string.IsNullOrEmpty(activityContext.TraceState))
        {
            environment[Activities.TRACESTATE] = activityContext.TraceState;
        }

        return environment;
    }
}
