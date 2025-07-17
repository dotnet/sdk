
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
        var currentBaggage = Baggage.Current;
        if (currentActivity == null)
        {
            return null;
        }
        var contextToInject = currentActivity.Context;
        if (contextToInject.TraceId == default || contextToInject.SpanId == default || contextToInject.TraceState is null)
        {
            return null;
        }
        var propagationContext = new PropagationContext(contextToInject, currentBaggage);
        var envDict = new Dictionary<string, string>(capacity: 2);
        Propagators.DefaultTextMapPropagator.Inject(propagationContext, envDict, WriteTraceStateIntoEnv);
        return envDict;
    }

    private static void WriteTraceStateIntoEnv(Dictionary<string, string> dictionary, string key, string value)
    {
        switch (key)
        {
            case "traceparent":
                dictionary[Activities.TRACEPARENT] = value;
                break;
            case "tracestate":
                dictionary[Activities.TRACESTATE] = value;
                break;
        }
    }
}