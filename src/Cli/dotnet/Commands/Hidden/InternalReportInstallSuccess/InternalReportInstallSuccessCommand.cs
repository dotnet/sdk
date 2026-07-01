// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Utilities;

namespace Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;

public class InternalReportInstallSuccessCommand
{
    public static int Run(ParseResult parseResult)
    {
        var telemetry = new ThreadBlockingTelemetry();
        ProcessInputAndSendTelemetry(parseResult, telemetry);
        return 0;
    }

    public static void ProcessInputAndSendTelemetry(string[] args, ITelemetryClient telemetry)
    {
        var result = Parser.Parse(["dotnet", "internal-reportinstallsuccess", .. args]);
        ProcessInputAndSendTelemetry(result, telemetry);
    }

    public static void ProcessInputAndSendTelemetry(ParseResult result, ITelemetryClient telemetry)
    {
        var definition = (InternalReportInstallSuccessCommandDefinition)result.CommandResult.Command;
        var exeName = Path.GetFileName(result.GetValue(definition.Argument));

        var filter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);
        foreach (var e in filter.Filter(new InstallerSuccessReport(exeName)))
        {
            telemetry.TrackEvent(e.EventName, e.Properties);
        }
    }

    internal class ThreadBlockingTelemetry : ITelemetryClient
    {
        private readonly TelemetryClient _telemetry;

        internal ThreadBlockingTelemetry()
        {
            var sessionId = Environment.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_SESSIONID);
            _telemetry = new TelemetryClient(sessionId);
        }

        public bool Enabled => _telemetry.Enabled;

        public void TrackEvent(string eventName, IDictionary<string, string?>? properties)
        {
            _telemetry.ThreadBlockingTrackEvent(eventName, properties);
        }
    }
}
