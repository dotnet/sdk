// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Tests.TelemetryTests;

public class FakeRecordEventNameTelemetry : ITelemetryClient
{
    public bool Enabled { get; set; }

    public string? EventName { get; set; }

    public void TrackEvent(string eventName, IDictionary<string, string?>? properties)
    {
        LogEntries.Add(new LogEntry
        {
            EventName = eventName,
            Properties = properties ?? new Dictionary<string, string?>()
        });
    }

    public ConcurrentBag<LogEntry> LogEntries { get; set; } = [];

    public class LogEntry
    {
        public string? EventName { get; set; }
        public IDictionary<string, string?> Properties { get; set; } = new Dictionary<string, string?>();
    }
}
