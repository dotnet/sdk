// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

public class MSBuildForwardingApp : CommandBase
{
    private readonly MSBuildForwardingAppWithoutLogging _forwardingAppWithoutLogging;

    /// <summary>
    /// Adds the CLI's telemetry logger to the MSBuild arguments if telemetry is enabled.
    /// </summary>
    private static MSBuildArgs ConcatTelemetryLogger(MSBuildArgs msbuildArgs)
    {
        if (TelemetryClient.CurrentSessionId != null)
        {
            try
            {
                Type loggerType = typeof(MSBuildLogger);
                Type forwardingLoggerType = typeof(MSBuildForwardingLogger);

#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
                msbuildArgs.OtherMSBuildArgs.Add($"-distributedlogger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}*{forwardingLoggerType.FullName},{forwardingLoggerType.GetTypeInfo().Assembly.Location}");
#pragma warning restore IL3000
                return msbuildArgs;
            }
            catch (Exception)
            {
                // Exceptions during telemetry shouldn't cause anything else to fail
            }
        }
        return msbuildArgs;
    }

    /// <summary>
    /// Mostly intended for quick/one-shot usage - most 'core' SDK commands should do more hands-on parsing.
    /// </summary>
    public MSBuildForwardingApp(IEnumerable<string> rawMSBuildArgs, string? msbuildPath = null) : this(
        MSBuildArgs.AnalyzeMSBuildArguments(rawMSBuildArgs.ToArray(), CommonOptions.CreatePropertyOption(), CommonOptions.CreateRestorePropertyOption(), CommonOptions.CreateMSBuildTargetOption(), CommonOptions.CreateVerbosityOption(), CommonOptions.CreateNoLogoOption()),
        msbuildPath)
    {
    }

    public MSBuildForwardingApp(MSBuildArgs msBuildArgs, string? msbuildPath = null)
    {
        var modifiedMSBuildArgs = CommonRunHelpers.AdjustMSBuildForLLMs(ConcatTelemetryLogger(msBuildArgs));
        _forwardingAppWithoutLogging = new MSBuildForwardingAppWithoutLogging(
            modifiedMSBuildArgs,
            msbuildPath: msbuildPath);
    }

    public IEnumerable<string> MSBuildArguments { get { return _forwardingAppWithoutLogging.GetAllArguments(); } }

    public void EnvironmentVariable(string name, string? value)
    {
        _forwardingAppWithoutLogging.EnvironmentVariable(name, value);
    }

    public ProcessStartInfo GetProcessStartInfo()
    {
        InitializeRequiredEnvironmentVariables();

        return _forwardingAppWithoutLogging.GetProcessStartInfo();
    }

    private void InitializeRequiredEnvironmentVariables()
    {
        EnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_TELEMETRY_SESSIONID, TelemetryClient.CurrentSessionId);
    }

    /// <summary>
    /// Test hook returning concatenated and escaped command line arguments that would be passed to MSBuild.
    /// </summary>
    internal string GetArgumentsToMSBuild() => ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(GetArgumentTokensToMSBuild());

    internal string[] GetArgumentTokensToMSBuild() => _forwardingAppWithoutLogging.GetAllArguments();

    public override int Execute()
    {
        // Ignore Ctrl-C for the remainder of the command's execution
        // Forwarding commands will just spawn the child process and exit
        Console.CancelKeyPress += (sender, e) => { e.Cancel = true; };

        int exitCode;
        if (_forwardingAppWithoutLogging.ExecuteMSBuildOutOfProc)
        {
            ProcessStartInfo startInfo = GetProcessStartInfo();
            exitCode = startInfo.Execute();
        }
        else
        {
            InitializeRequiredEnvironmentVariables();
            string[] arguments = _forwardingAppWithoutLogging.GetAllArguments();
            exitCode = _forwardingAppWithoutLogging.ExecuteInProc(arguments);
        }

        return exitCode;
    }
}
