// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

#if !CLI_AOT
using System.Reflection;
#endif
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

/// <summary>
/// Invokes MSBuild consistently across different environments - either in-process or out-of-process.
/// It also ensures that the SDK modifications to default MSBuild behaviors are applied - for example
/// <list type="bullet">
/// <item>Consuming MSBuild-engine- and SDK-build-logic-emitted telemetry via the central <see cref="MSBuildLogger"/> and per-worker-node <see cref="MSBuildForwardingLogger"/></item>
/// <item>LLM environment adjustments</item>
/// </list>
/// </summary>
/// <remarks>
/// In AOT mode all MSBuild invocations happen via out-of-process execution, so this should be used with caution - most AOT commands at time of writing
/// do not use MSBuild, and this is mostly intended to make `--help` output for MSBuild-based commands not require jumping into the managed process space.
/// </remarks>
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
#if !CLI_AOT
                Type loggerType = typeof(MSBuildLogger);
                Type forwardingLoggerType = typeof(MSBuildForwardingLogger);
                string loggerTypeFullName = loggerType.FullName!; // not-null because these are part of the same assembly
                string forwardingLoggerTypeFullName = forwardingLoggerType.FullName!; // not-null because these are part of the same assembly
// these come from the dotnet assembly that we are currently in!
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
                string loggerTypeLocation = loggerType.GetTypeInfo().Assembly.Location;
                string forwardingLoggerTypeLocation = forwardingLoggerType.GetTypeInfo().Assembly.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
#else
                string loggerTypeFullName = "Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildLogger";
                string forwardingLoggerTypeFullName = "Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildForwardingLogger";
                string loggerTypeLocation = Path.Combine(AppContext.BaseDirectory, "dotnet.dll");
                string forwardingLoggerTypeLocation = loggerTypeLocation;
#endif

                msbuildArgs.OtherMSBuildArgs.Add($"-distributedlogger:{loggerTypeFullName},{loggerTypeLocation}*{forwardingLoggerTypeFullName},{forwardingLoggerTypeLocation}");
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
    /// Initializes a new instance of the <see cref="MSBuildForwardingApp"/> class with a set of raw MSBuild arguments.
    /// </summary>
    /// <remarks>
    /// Mostly intended for quick/one-shot usage - most 'core' SDK commands should do more hands-on parsing.
    /// </remarks>
    public MSBuildForwardingApp(IEnumerable<string> rawMSBuildArgs, string? msbuildPath = null) : this(
        MSBuildArgs.AnalyzeMSBuildArguments(rawMSBuildArgs.ToArray(), CommonOptions.CreatePropertyOption(), CommonOptions.CreateRestorePropertyOption(), CommonOptions.CreateMSBuildTargetOption(), CommonOptions.CreateVerbosityOption(), CommonOptions.CreateNoLogoOption()),
        msbuildPath)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildForwardingApp"/> class with a parsed set of MSBuild arguments.
    /// These arguments are usually unique per SDK command that needs to invoke MSBuild, because each command may have its own options that
    /// 'forward' as different MSBuild arguments.
    /// </summary>
    /// <param name="msBuildArgs">MSBuild arguments to forward to the builder process, parsed by using <see cref="MSBuildArgs.AnalyzeMSBuildArguments"/> to apply a set of per-command <see cref="System.CommandLine.Option`1"/>s to a list of unparsed command line input tokens.</param>
    /// <param name="msbuildPath">The path to the MSBuild executable. If null, the default MSBuild executable will be used.</param>
    public MSBuildForwardingApp(MSBuildArgs msBuildArgs, string? msbuildPath = null)
    {
        var modifiedMSBuildArgs = CommonRunHelpers.AdjustMSBuildForLLMs(ConcatTelemetryLogger(msBuildArgs));
#if CLI_AOT
        const bool forceOutOfProc = true;
#else
        const bool forceOutOfProc = false;
#endif
        _forwardingAppWithoutLogging = new MSBuildForwardingAppWithoutLogging(
            modifiedMSBuildArgs,
            msbuildPath: msbuildPath,
            forceOutOfProc: forceOutOfProc);
        InitializeRequiredEnvironmentVariables();
    }

    public IEnumerable<string> MSBuildArguments { get { return _forwardingAppWithoutLogging.GetAllArguments(); } }

    public void EnvironmentVariable(string name, string? value)
    {
        _forwardingAppWithoutLogging.EnvironmentVariable(name, value);
    }

    public ProcessStartInfo GetProcessStartInfo() => _forwardingAppWithoutLogging.GetProcessStartInfo();

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
        return _forwardingAppWithoutLogging.Execute();
    }
}
