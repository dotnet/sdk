// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;

public class InternalReportInstallSuccessCommand
{
    internal const string TelemetrySessionIdEnvironmentVariableName = "DOTNET_CLI_TELEMETRY_SESSIONID";

    public static int Run(ParseResult parseResult)
    {
        var telemetry = new ThreadBlockingTelemetry();
        ProcessInputAndSendTelemetry(parseResult, telemetry);
        return 0;
    }

    public static void ProcessInputAndSendTelemetry(string[] args, ITelemetry telemetry)
    {
        var parser = Parser.Instance;
        var result = parser.ParseFrom("dotnet internal-reportinstallsuccess", args);
        ProcessInputAndSendTelemetry(result, telemetry);
    }

    public static void ProcessInputAndSendTelemetry(ParseResult result, ITelemetry telemetry)
    {
        var exeName = Path.GetFileName(result.GetValue(InternalReportInstallSuccessCommandParser.Argument));

        var filter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
        foreach (var e in filter.Filter(new InstallerSuccessReport(exeName)))
        {
            telemetry.TrackEvent(e.EventName, e.Properties, null);
        }
    }

    internal class ThreadBlockingTelemetry : ITelemetry
    {
        private readonly Telemetry.Telemetry _telemetry;

        internal ThreadBlockingTelemetry()
        {
            var sessionId = Environment.GetEnvironmentVariable(TelemetrySessionIdEnvironmentVariableName);
            _telemetry = new Telemetry.Telemetry(sessionId);
        }

        public bool Enabled => _telemetry.Enabled;

        public void TrackEvent(string eventName, IDictionary<string, string?>? properties, IDictionary<string, double>? measurements)
        {
            _telemetry.ThreadBlockingTrackEvent(eventName, properties, measurements);
        }
    }
}

internal class InstallerSuccessReport(string? exeName)
{
    public string ExeName { get; } = exeName ?? throw new ArgumentNullException(nameof(exeName));
}
