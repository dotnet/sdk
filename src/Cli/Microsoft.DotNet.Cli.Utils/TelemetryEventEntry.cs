// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Utils;

public static class TelemetryEventEntry
{
    public static event EventHandler<InstrumentationEventArgs>? EntryPosted;
    public static ITelemetryFilter TelemetryFilter { get; set; } = new BlockFilter();

    public static void TrackEvent(string eventName, IDictionary<string, string?>? properties = null)
    {
        EntryPosted?.Invoke(typeof(TelemetryEventEntry), new InstrumentationEventArgs(eventName, properties));
    }

    public static void SendFiltered(ParseResult parseResult) =>
        SendFiltered(TelemetryFilter.Filter(parseResult));

    public static void SendFiltered(ParseResultWithGlobalJsonState parseData) =>
        SendFiltered(TelemetryFilter.Filter(parseData));

    public static void SendFiltered(InstallerSuccessReport report) =>
        SendFiltered(TelemetryFilter.Filter(report));

    public static void SendFiltered(Exception exception) =>
        SendFiltered(TelemetryFilter.Filter(exception));

    private static void SendFiltered(IEnumerable<ApplicationInsightsEntryFormat> entries)
    {
        foreach (ApplicationInsightsEntryFormat entry in entries)
        {
            TrackEvent(entry.EventName, entry.Properties);
        }
    }

    public static void Subscribe(Action<string, IDictionary<string, string?>?> subscriber)
    {
        void Handler(object? sender, InstrumentationEventArgs eventArgs)
        {
            subscriber(eventArgs.EventName, eventArgs.Properties);
        }

        EntryPosted += Handler;
    }
}

public class BlockFilter : ITelemetryFilter
{
    private static readonly ApplicationInsightsEntryFormat[] s_emptyEntries = [];

    public IEnumerable<ApplicationInsightsEntryFormat> Filter(ParseResult parseResult) => s_emptyEntries;

    public IEnumerable<ApplicationInsightsEntryFormat> Filter(ParseResultWithGlobalJsonState parseData) => s_emptyEntries;

    public IEnumerable<ApplicationInsightsEntryFormat> Filter(InstallerSuccessReport report) => s_emptyEntries;

    public IEnumerable<ApplicationInsightsEntryFormat> Filter(Exception exception) => s_emptyEntries;
}

public class InstrumentationEventArgs : EventArgs
{
    internal InstrumentationEventArgs(
        string eventName,
        IDictionary<string, string?>? properties)
    {
        EventName = eventName;
        Properties = properties;
    }

    public string EventName { get; }
    public IDictionary<string, string?>? Properties { get; }
}

public class ApplicationInsightsEntryFormat(
    string eventName,
    IDictionary<string, string?>? properties = null)
{
    public string EventName { get; } = eventName;
    public IDictionary<string, string?>? Properties { get; } = properties;

    public ApplicationInsightsEntryFormat WithAppliedToPropertiesValue(Func<string, string> func)
    {
        var appliedProperties = Properties?.ToDictionary(p => p.Key, p => (string?)func(p.Value ?? string.Empty));
        return new ApplicationInsightsEntryFormat(EventName, appliedProperties);
    }
}

public record ParseResultWithGlobalJsonState(ParseResult ParseResult, string? GlobalJsonState);
