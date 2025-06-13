// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Telemetry;

public interface ITelemetry : IDisposable
{
    bool Enabled { get; }

    void TrackEvent(string eventName, IDictionary<string, string> properties, IDictionary<string, double> measurements);

    void Flush();
}
