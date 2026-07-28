// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Utils;

internal sealed class MSBuildForwardingAppWithoutLogging
{
    /// <summary>
    /// An override flag that determines whether to always execute MSBuild out-of-process. By default the managed dotnet CLI
    /// prefers to execute MSBuild in-process to prevent needing to spawn another process' central/worker node,
    /// but this flag can be used to force out-of-process execution.
    /// </summary>
    private static readonly bool AlwaysExecuteMSBuildOutOfProc = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_RUN_MSBUILD_OUTOFPROC");

    /// <summary>
    /// A flag that determines whether to use the MSBuild server - a persistent central node that can serve
    /// as a place to cache data and prevent re-doing CoreCLR startup/JITting for small builds.
    /// By default, the MSBuild server is enabled, but users that hit stability/correctness concerns with some
    /// 1P tasks that keep static state around can opt out by setting this to false.
    /// </summary>
    private static readonly bool UseMSBuildServer = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_USE_MSBUILD_SERVER", true);

    /// <summary>
    /// What the SDK's opinion is on the default terminal logger. The SDK defaults to '<c>auto</c>' which will use the terminal logger if the output is going to a terminal, otherwise it will use the console logger.
    /// Some users prefer to always use the legacy console logger, so this gives them a way to consistently do so.
    /// </summary>
    private static readonly string? TerminalLoggerDefault = Env.GetEnvironmentVariable("DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER");

    public static string MSBuildVersion
    {
        get => Build.Evaluation.ProjectCollection.DisplayVersion;
    }

    private const string MSBuildExeName = "MSBuild.dll";

    private const string SdksDirectoryName = "Sdks";

    /// <summary>
    /// The SDK's default MSBuild verbosity level - we choose <see cref="VerbosityOptions.Minimal"/> as a good balance between information and terminal noise.
    /// </summary>
    internal const VerbosityOptions DefaultVerbosity = VerbosityOptions.m;

    /// <summary>
    /// The forwarding app implementation for executing MSBuild out-of-process.
    /// </summary>
    /// <remarks>
    /// This is null if we're running MSBuild in-process.
    /// </remarks>
    private ForwardingAppImplementation? _forwardingApp;

    /// <summary>
    /// A test-only hook for the MSBuildExtensionsPath, which is a key location that MSBuild logic is read from by the MSBuild Common Targets.
    /// </summary>
    internal static string? MSBuildExtensionsPathTestHook = null;

    /// <summary>
    /// Structure describing the parsed and forwarded MSBuild arguments for this command.
    /// </summary>
    private MSBuildArgs _msbuildArgs;

    /// <summary>
    /// Path to the MSBuild binary to use - this is set by constructor parameter or looked up via <see cref="GetMSBuildExePath"/>.
    /// </summary>
    public string MSBuildPath { get; }

    /// <summary>
    /// True if, given current state of the class, MSBuild would be executed in its own process.
    /// </summary>
    public bool ExecuteMSBuildOutOfProc => _forwardingApp != null;

    /// <summary>
    /// The set of environment variables that must be set on the MSBuild process (or the current
    /// process when executing in-proc) for the build to behave correctly.
    /// </summary>
    private readonly Dictionary<string, string?> _msbuildRequiredEnvironmentVariables = GetMSBuildRequiredEnvironmentVariables();

    private readonly List<string> _msbuildRequiredParameters = ["-maxcpucount", $"--verbosity:{DefaultVerbosity}"];

    public MSBuildForwardingAppWithoutLogging(MSBuildArgs msbuildArgs, string? msbuildPath = null, bool forceOutOfProc = false)
    {
        string defaultMSBuildPath = GetMSBuildExePath();
        _msbuildArgs = msbuildArgs;

        string? tlpDefault = TerminalLoggerDefault;
        if (string.IsNullOrWhiteSpace(tlpDefault))
        {
            tlpDefault = "auto";
        }

        if (!string.IsNullOrWhiteSpace(tlpDefault))
        {
            _msbuildRequiredParameters.Add($"-tlp:default={tlpDefault}");
        }

        MSBuildPath = msbuildPath ?? defaultMSBuildPath;

        // The MSBuild server is enabled by default. Force MSBUILDUSESERVER on unless the user has opted out
        // via DOTNET_CLI_USE_MSBUILD_SERVER, or has already set MSBUILDUSESERVER themselves - in which case we
        // leave their value untouched so it can toggle the server on its own.
        if (UseMSBuildServer && Env.GetEnvironmentVariable("MSBUILDUSESERVER") is null)
        {
            EnvironmentVariable("MSBUILDUSESERVER", "1");
        }

        // If DOTNET_CLI_RUN_MSBUILD_OUTOFPROC is set, the caller requires it (e.g. the AOT CLI, which
        // cannot host MSBuild in-process), or we're asked to execute a non-default binary, call MSBuild out-of-proc.
        if (AlwaysExecuteMSBuildOutOfProc || forceOutOfProc || !string.Equals(MSBuildPath, defaultMSBuildPath, StringComparison.OrdinalIgnoreCase))
        {
            InitializeForOutOfProcForwarding();
        }
    }

    private void InitializeForOutOfProcForwarding()
    {
        _forwardingApp = new ForwardingAppImplementation(
            MSBuildPath,
            GetAllArguments(),
            environmentVariables: _msbuildRequiredEnvironmentVariables);
    }

    public ProcessStartInfo GetProcessStartInfo()
    {
        Debug.Assert(_forwardingApp != null, "Can't get ProcessStartInfo when not executing out-of-proc");
        return _forwardingApp.GetProcessStartInfo();
    }

    public string[] GetAllArguments()
    {
        return [.. _msbuildRequiredParameters, .. EmitMSBuildArgs(_msbuildArgs)];
    }

    private string[] EmitMSBuildArgs(MSBuildArgs msbuildArgs) => [
        .. msbuildArgs.GlobalProperties?.Select(kvp => EmitProperty(kvp)) ?? [],
        .. msbuildArgs.RestoreGlobalProperties?.Select(kvp => EmitProperty(kvp, "restoreProperty")) ?? [],
        .. msbuildArgs.RequestedTargets?.Select(target => $"--target:{target}") ?? [],
        .. msbuildArgs.Verbosity is not null ? new string[1] { $"--verbosity:{msbuildArgs.Verbosity}" } : [],
        .. msbuildArgs.NoLogo is true ? new string[1] { "--nologo" } : [],
        .. msbuildArgs.OtherMSBuildArgs
    ];

    private static string EmitProperty(KeyValuePair<string, string> property, string label = "property")
    {
        // Escape RestoreSources to avoid issues with semicolons in the value.
        return IsRestoreSources(property.Key)
            ? $"--{label}:{property.Key}={Escape(property.Value)}"
            : $"--{label}:{property.Key}={property.Value}";
    }

    /// <summary>
    /// Add an environment variable to the state that will be passed to MSBuild when it is run.
    /// </summary>
    public void EnvironmentVariable(string name, string? value)
    {
        if (_forwardingApp != null)
        {
            _forwardingApp.WithEnvironmentVariable(name, value);
        }
        else
        {
            _msbuildRequiredEnvironmentVariables.Add(name, value);
        }

        if (value == string.Empty || value == "\0")
        {
            // Unlike ProcessStartInfo.EnvironmentVariables, Environment.SetEnvironmentVariable can't set a variable
            // to an empty value, so we just fall back to calling MSBuild out-of-proc if we encounter this case.
            // https://github.com/dotnet/runtime/issues/50554
            InitializeForOutOfProcForwarding();

            // Disable MSBUILDUSESERVER if any env vars are null as those are not properly transferred to build nodes
            _msbuildRequiredEnvironmentVariables["MSBUILDUSESERVER"] = "0";
        }
    }

    /// <summary>
    /// Run the MSBuild arguments that have been previously specified.
    /// </summary>
    public int Execute()
    {
        if (_forwardingApp != null)
        {
            return GetProcessStartInfo().Execute();
        }
        else
        {
            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                return ExecuteInProc(GetAllArguments());
            }
            else
            {
                throw new PlatformNotSupportedException("Can't invoke MSBuild in-process because this runtime doesn't support dynamic code generation.");
            }
        }
    }

    /// <summary>
    /// Directly executes MSBuild's <see cref="Build.CommandLine.MSBuildApp.Main"/> method in the current process.
    /// Sets up the local environment with required MSBuild environment variables before handing off execution entirely to MSBuild.
    /// After execution, the original environment variables are restored for any remaining cleanup work the dotnet CLI needs to perform.
    /// </summary>
    [RequiresDynamicCode("Calls MSBuildApp.Main, which is not AOT-safe")]
    public int ExecuteInProc(string[] arguments)
    {
        // Save current environment variables before overwriting them.
        Dictionary<string, string?> savedEnvironmentVariables = [];
        try
        {
            foreach (KeyValuePair<string, string?> kvp in _msbuildRequiredEnvironmentVariables)
            {
                savedEnvironmentVariables[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }

            try
            {
                // Execute MSBuild in the current process by calling its Main method.
                return Build.CommandLine.MSBuildApp.Main(arguments);
            }
            catch (Exception exception)
            {
                // MSBuild, like all well-behaved CLI tools, handles all exceptions. In the unlikely case
                // that something still escapes, we print the exception and fail the call. Non-localized
                // string is OK here.
                Console.Error.Write("Unhandled exception: ");
                Console.Error.WriteLine(exception.ToString());

                return unchecked((int)0xe0434352); // EXCEPTION_COMPLUS
            }
        }
        finally
        {
            // Restore saved environment variables.
            foreach (KeyValuePair<string, string?> kvp in savedEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }
        }
    }

    /// <summary>
    /// This is a workaround for https://github.com/Microsoft/msbuild/issues/1622.
    /// Only used historically for RestoreSources property only.
    /// </summary>
    private static string Escape(string propertyValue) =>
        propertyValue.Replace(";", "%3B").Replace("://", ":%2F%2F");

    /// <summary>
    /// Gets the path to the MSBuild executable. By default, this will be the 'MSBuild.dll' file in the same location as the `dotnet.dll` binary.
    /// </summary>
    /// <returns></returns>
    private static string GetMSBuildExePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            MSBuildExeName);
    }

    /// <summary>
    /// Gets the path to the MSBuild SDKs directory - where the SDKs will be loaded from by the default, local-path-based SDK resolver.
    /// By default, this will be the 'SDKs' directory in the same location as the `dotnet.dll` binary, but it can be overridden by the `MSBuildSDKsPath` environment variable.
    /// </summary>
    /// <returns></returns>
    public static string GetMSBuildSDKsPath()
    {
        var envMSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");

        if (envMSBuildSDKsPath != null)
        {
            return envMSBuildSDKsPath;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            SdksDirectoryName);
    }

    private static string GetDotnetPath() => new Muxer().MuxerPath;

    /// <summary>
    /// Gets the required environment variables for MSBuild.
    /// The Common Targets require specific environment variables to be set in order to function correctly:
    /// <list type="bullet">
    /// <item><term>MSBuildExtensionsPath</term><description>The path to the 'MSBuild extensions' - where the Common Targets themselves will be loaded from. Also where SDK Resolvers will be loaded from.</description></item>
    /// <item><term>MSBuildSDKsPath</term><description>The path to the 'MSBuild SDKs' - where the SDKs will be loaded from by the default resolver. </description></item>
    /// <item><term>DOTNET_HOST_PATH</term><description>The path to the .NET SDK host - used to execute .NET applications by targets in the Common Targets that need to run managed .NET binaries that are not shipped with apphosts.</description></item>
    /// </list>
    /// </summary>
    internal static Dictionary<string, string?> GetMSBuildRequiredEnvironmentVariables()
    {
        return new()
        {
            { "MSBuildExtensionsPath", MSBuildExtensionsPathTestHook ?? Environment.GetEnvironmentVariable("MSBuildExtensionsPath") ?? AppContext.BaseDirectory },
            { "MSBuildSDKsPath", GetMSBuildSDKsPath() },
            { "DOTNET_HOST_PATH", GetDotnetPath() },
        };
    }

    private static bool IsRestoreSources(string arg) => arg.Equals("RestoreSources", StringComparison.OrdinalIgnoreCase);
}

#endif
