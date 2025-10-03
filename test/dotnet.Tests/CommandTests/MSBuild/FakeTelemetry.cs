// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class FakeTelemetry : ITelemetry
    {
        public bool Enabled { get; set; } = true;
        
        private readonly List<LogEntry> _logEntries = new List<LogEntry>();

        public void TrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements)
        {
            var entry = new LogEntry { EventName = eventName, Properties = properties, Measurement = measurements };
            _logEntries.Add(entry);
        }

        public void Flush()
        {
        }

        public void Dispose()
        {
        }

        public LogEntry LogEntry => _logEntries.Count > 0 ? _logEntries[_logEntries.Count - 1] : null;

        public IReadOnlyList<LogEntry> LogEntries => _logEntries.AsReadOnly();
    }
}
