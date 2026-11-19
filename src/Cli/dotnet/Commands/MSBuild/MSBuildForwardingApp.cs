// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

#if CLI_AOT
using System.Text;
using Microsoft.Build.Server;
#endif
#if !CLI_AOT
using System.Reflection;
#endif
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.CommandFactory.CommandResolution;
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
/// AOT commands normally forward MSBuild out-of-process. Eligible commands can instead use the
/// MSBuild server client without loading the build engine into the native process.
/// </remarks>
public class MSBuildForwardingApp : CommandBase
{
    private readonly MSBuildForwardingAppWithoutLogging _forwardingAppWithoutLogging;
#if CLI_AOT
    private readonly string? _telemetryLoggerArgument;
    private string? _msbuildServerCommandName;
#endif

    /// <summary>
    /// Adds the CLI's telemetry logger to the MSBuild arguments if telemetry is enabled.
    /// </summary>
    private static MSBuildArgs ConcatTelemetryLogger(MSBuildArgs msbuildArgs, out string? telemetryLoggerArgument)
    {
        telemetryLoggerArgument = null;
        if (TelemetryClient.CurrentSessionId != null)
        {
            try
            {
#if !CLI_AOT
                Type loggerType = typeof(MSBuildLogger);
                Type forwardingLoggerType = typeof(MSBuildForwardingLogger);
                string loggerTypeFullName = loggerType.FullName!; // not-null because these are part of the same assembly
                string forwardingLoggerTypeFullName = forwardingLoggerType.FullName!; // not-null because these are part of the same assembly
                // The logger assembly locations come from the dotnet assembly we are currently executing in.
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
                string loggerTypeLocation = loggerType.GetTypeInfo().Assembly.Location;
                string forwardingLoggerTypeLocation = forwardingLoggerType.GetTypeInfo().Assembly.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
#else
                string loggerTypeFullName = "Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildLogger";
                string forwardingLoggerTypeFullName = "Microsoft.DotNet.Cli.Commands.MSBuild.MSBuildForwardingLogger";
                string loggerTypeLocation = Path.Combine(SdkPaths.SdkDirectory, "dotnet.dll");
                string forwardingLoggerTypeLocation = loggerTypeLocation;
#endif

                telemetryLoggerArgument = $"-distributedlogger:{loggerTypeFullName},{loggerTypeLocation}*{forwardingLoggerTypeFullName},{forwardingLoggerTypeLocation}";
                msbuildArgs.OtherMSBuildArgs.Add(telemetryLoggerArgument);
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
    public MSBuildForwardingApp(IEnumerable<string> rawMSBuildArgs, string? msbuildPath, CommandServices services) : this(
        MSBuildArgs.AnalyzeMSBuildArguments(rawMSBuildArgs.ToArray(), CommonOptions.CreatePropertyOption(), CommonOptions.CreateRestorePropertyOption(), CommonOptions.CreateMSBuildTargetOption(), CommonOptions.CreateVerbosityOption(), CommonOptions.CreateNoLogoOption()),
        msbuildPath,
        services)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MSBuildForwardingApp"/> class with a parsed set of MSBuild arguments.
    /// These arguments are usually unique per SDK command that needs to invoke MSBuild, because each command may have its own options that
    /// 'forward' as different MSBuild arguments.
    /// </summary>
    /// <param name="msBuildArgs">MSBuild arguments to forward to the builder process, parsed by using <see cref="MSBuildArgs.AnalyzeMSBuildArguments"/> to apply a set of per-command <see cref="System.CommandLine.Option`1"/>s to a list of unparsed command line input tokens.</param>
    /// <param name="msbuildPath">The path to the MSBuild executable. If null, the default MSBuild executable will be used.</param>
    /// <param name="services">The command's service dependencies.</param>
    public MSBuildForwardingApp(MSBuildArgs msBuildArgs, string? msbuildPath, CommandServices services)
        : base(services)
    {
        var modifiedMSBuildArgs = CommonRunHelpers.AdjustMSBuildForLLMs(
            ConcatTelemetryLogger(msBuildArgs, out string? telemetryLoggerArgument),
            Services);
#if CLI_AOT
        _telemetryLoggerArgument = telemetryLoggerArgument;
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

        if (ActivityContextFactory.MakeActivityContextEnvironment() is { } activityContextEnvironment)
        {
            foreach ((string name, string value) in activityContextEnvironment)
            {
                EnvironmentVariable(name, value);
            }
        }
    }

    /// <summary>
    /// Test hook returning concatenated and escaped command line arguments that would be passed to MSBuild.
    /// </summary>
    internal string GetArgumentsToMSBuild() => ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(GetArgumentTokensToMSBuild());

    internal string[] GetArgumentTokensToMSBuild() => _forwardingAppWithoutLogging.GetAllArguments();

    public override int Execute() => Execute(workingDirectory: null);

    internal virtual int ExecuteOutOfProc(string workingDirectory) => Execute(workingDirectory);

#if CLI_AOT
    internal void UseMSBuildServerClient(string commandName) => _msbuildServerCommandName = commandName;
#endif

    private int Execute(string? workingDirectory)
    {
#if CLI_AOT
        if (_msbuildServerCommandName is not null && workingDirectory is null && _forwardingAppWithoutLogging.CanSetEnvironmentVariablesInProcess)
        {
            return ExecuteServerClient();
        }
#endif
        return ExecuteForwarded(workingDirectory);
    }

    private int ExecuteForwarded(string? workingDirectory)
    {
        // Ignore Ctrl-C for the remainder of the command's execution
        // Forwarding commands will just spawn the child process and exit
        Console.CancelKeyPress += (sender, e) => { e.Cancel = true; };
        return _forwardingAppWithoutLogging.Execute(workingDirectory);
    }

#if CLI_AOT
    private int ExecuteServerClient()
    {
        using var activity = Activities.PerformanceSource.StartActivity("msbuild-submission");
        DateTime startedAt = DateTime.UtcNow;
        string[] arguments = _forwardingAppWithoutLogging.GetAllArguments();
        if (_telemetryLoggerArgument is not null)
        {
            int loggerIndex = Array.IndexOf(arguments, _telemetryLoggerArgument);
            if (loggerIndex < 0)
            {
                throw new InvalidOperationException("The SDK telemetry logger is missing from the MSBuild arguments.");
            }

            arguments[loggerIndex] = MSBuildServerTelemetry.AddClientStartToLogger(_telemetryLoggerArgument, startedAt);
        }

        if (Env.GetEnvironmentVariable(EnvironmentVariableNames.DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING) != "1"
            && UILanguageOverride.OperatingSystemSupportsUtf8())
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (OperatingSystem.IsWindows())
            {
                Console.InputEncoding = Encoding.UTF8;
            }
        }
        using var cancellationSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancellationHandler = (_, e) =>
        {
            if (e.SpecialKey != ConsoleSpecialKey.ControlBreak)
            {
                e.Cancel = true;
                cancellationSource.Cancel();
            }
        };
        MSBuildClientExitResult result;
        Console.CancelKeyPress += cancellationHandler;
        try
        {
            using var environment = _forwardingAppWithoutLogging.SetEnvironmentVariablesForInProcessExecution();
            Reporter.Verbose.WriteLine($"AOT {_msbuildServerCommandName} tier: MSBuildServer.");
            result = new MSBuildClient(
                [_forwardingAppWithoutLogging.MSBuildPath, .. arguments],
                _forwardingAppWithoutLogging.MSBuildPath,
                multiThreaded: false,
                shutdownServerAfterBuild: false).Execute(cancellationSource.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancellationHandler;
        }

        if (result.MSBuildClientExitType == MSBuildClientExitType.Success && result.MSBuildAppExitTypeString is not null)
        {
            Reporter.Verbose.WriteLine("AOT build server client: request completed.");
        }

        return CompleteServerClientResult(result, cancellationSource.IsCancellationRequested, () => ExecuteForwarded(workingDirectory: null));
    }

    internal static int CompleteServerClientResult(MSBuildClientExitResult result, bool cancellationRequested, Func<int> forward)
    {
        if (result.MSBuildClientExitType == MSBuildClientExitType.Success)
        {
            if (result.MSBuildAppExitTypeString == "Success")
            {
                return 0;
            }

            if (!string.IsNullOrEmpty(result.MSBuildAppExitTypeString))
            {
                return 1;
            }
        }
        else if (result.MSBuildClientExitType is MSBuildClientExitType.ServerBusy
            or MSBuildClientExitType.UnableToConnect
            or MSBuildClientExitType.UnknownServerState
            or MSBuildClientExitType.LaunchError)
        {
            return cancellationRequested ? 1 : forward();
        }

        Reporter.Error.WriteLine($"{CliStrings.MSBuildServer}: {CliCommandStrings.Failed} ({result.MSBuildClientExitType}).");
        return 1;
    }
#endif
}
