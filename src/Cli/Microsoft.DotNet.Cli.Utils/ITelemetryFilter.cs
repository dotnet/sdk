// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Utils;

public interface ITelemetryFilter
{
    IEnumerable<TelemetryEntryFormat> Filter(ParseResult parseResult);

    IEnumerable<TelemetryEntryFormat> Filter(ParseResultWithGlobalJsonState parseData);

    IEnumerable<TelemetryEntryFormat> Filter(InstallerSuccessReport report);

    IEnumerable<TelemetryEntryFormat> Filter(Exception exception);
}
