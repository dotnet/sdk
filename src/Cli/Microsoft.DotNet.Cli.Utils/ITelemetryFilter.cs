// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Utils;

public interface ITelemetryFilter
{
    IEnumerable<ApplicationInsightsEntryFormat> Filter(ParseResult parseResult);

    IEnumerable<ApplicationInsightsEntryFormat> Filter(ParseResultWithGlobalJsonState parseData);

    IEnumerable<ApplicationInsightsEntryFormat> Filter(InstallerSuccessReport report);

    IEnumerable<ApplicationInsightsEntryFormat> Filter(Exception exception);
}
