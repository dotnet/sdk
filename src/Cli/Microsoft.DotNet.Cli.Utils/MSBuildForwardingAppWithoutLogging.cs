// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.Net.BuildServerUtils;

namespace Microsoft.DotNet.Cli.Utils;

internal class MSBuildForwardingAppWithoutLogging
{
    private static readonly bool AlwaysExecuteMSBuildOutOfProc = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_RUN_MSBUILD_OUTOFPROC");
    private static readonly bool UseMSBuildServer = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_USE_MSBUILD_SERVER", false);
    private static readonly string? TerminalLoggerDefault = Env.GetEnvironmentVariable("DOTNET_CLI_CONFIGURE_MSBUILD_TERMINAL_LOGGER");

    public static string MSBuildVersion
    {
        get => Microsoft.Build.Evaluation.ProjectCollection.DisplayVersion;
    }
    private const string MSBuildExeName = "MSBuild.dll";

    private const string SdksDirectoryName = "Sdks";

    // Null if we're running MSBuild in-proc.
    private ForwardingAppImplementation? _forwardingApp;

    internal static string? MSBuildExtensionsPathTestHook = null;

    /// <summary>
    /// Structure describing the parsed and forwarded MSBuild arguments for this command.
    /// </summary>
    private MSBuildArgs _msbuildArgs;

    // Path to the MSBuild binary to use.
    public string MSBuildPath { get; }

    // True if, given current state of the class, MSBuild would be executed in its own process.
    public bool ExecuteMSBuildOutOfProc => _forwardingApp != null;

    private readonly Dictionary<string, string?> _msbuildRequiredEnvironmentVariables = GetMSBuildRequiredEnvironmentVariables();

    private readonly List<string> _msbuildRequiredParameters = [ "-maxcpucount", "--verbosity:m" ];

    public MSBuildForwardingAppWithoutLogging(MSBuildArgs msbuildArgs, string? msbuildPath = null, bool includeLogo = false, bool isRestoring = true)
    {
        string defaultMSBuildPath = GetMSBuildExePath();
        _msbuildArgs = msbuildArgs;
        if (!includeLogo && !msbuildArgs.OtherMSBuildArgs.Contains("-nologo", StringComparer.OrdinalIgnoreCase))
        {
            // If the user didn't explicitly ask for -nologo, we add it to avoid the MSBuild logo.
            // This is useful for scenarios like restore where we don't want to print the logo.
            // Note that this is different from the default behavior of MSBuild, which prints the logo.
            msbuildArgs.OtherMSBuildArgs.Add("-nologo");
        }
        string? tlpDefault = TerminalLoggerDefault;
        // new for .NET 9 - default TL to auto (aka enable in non-CI scenarios)
        if (string.IsNullOrWhiteSpace(tlpDefault))
        {
            tlpDefault = "auto";
        }

        if (!string.IsNullOrWhiteSpace(tlpDefault))
        {
            _msbuildRequiredParameters.Add($"-tlp:default={tlpDefault}");
        }

        MSBuildPath = msbuildPath ?? defaultMSBuildPath;

        EnvironmentVariable("MSBUILDUSESERVER", UseMSBuildServer ? "1" : "0");

        EnvironmentVariable(BuildServerUtility.DotNetHostServerPath, GetHostServerPath(createDirectory: true));

        // If DOTNET_CLI_RUN_MSBUILD_OUTOFPROC is set or we're asked to execute a non-default binary, call MSBuild out-of-proc.
        if (AlwaysExecuteMSBuildOutOfProc || !string.Equals(MSBuildPath, defaultMSBuildPath, StringComparison.OrdinalIgnoreCase))
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
        return [.. _msbuildRequiredParameters, ..EmitMSBuildArgs(_msbuildArgs) ];
    }

    private string[] EmitMSBuildArgs(MSBuildArgs msbuildArgs) => [
        .. msbuildArgs.GlobalProperties?.Select(kvp => EmitProperty(kvp)) ?? [],
        .. msbuildArgs.RestoreGlobalProperties?.Select(kvp => EmitProperty(kvp, "restoreProperty")) ?? [],
        .. msbuildArgs.RequestedTargets?.Select(target => $"--target:{target}") ?? [],
        .. msbuildArgs.Verbosity is not null ? new string[1] { $"--verbosity:{msbuildArgs.Verbosity}" } : [],
        .. msbuildArgs.OtherMSBuildArgs
    ];

    private static string EmitProperty(KeyValuePair<string, string> property, string label = "property")
    {
        // Escape RestoreSources to avoid issues with semicolons in the value.
        return IsRestoreSources(property.Key)
            ? $"--{label}:{property.Key}={Escape(property.Value)}"
            : $"--{label}:{property.Key}={property.Value}";
    }

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

    public static string GetHostServerPath(bool createDirectory)
    {
        // If the path is set from outside, reuse it.
        var hostServerPath = Env.GetEnvironmentVariable(BuildServerUtility.DotNetHostServerPath);
        if (string.IsNullOrWhiteSpace(hostServerPath))
        {
            // Otherwise, construct a directory path under temp.

            // If we're on a Unix machine then named pipes are implemented using Unix Domain Sockets.
            // Most Unix systems have a maximum path length limit for Unix Domain Sockets, with Mac having a particularly short one.
            // Mac also has a generated temp directory that can be quite long, leaving very little room for the actual pipe name.
            // Fortunately, '/tmp' is mandated by POSIX to always be a valid temp directory, so we can use that instead.
            const string dotnet = "dotnet";
            string baseDirectory = OperatingSystem.IsWindows()
                ? Path.Join(dotnet, Environment.UserName) // it's not a real path on Windows, just a name
                : Path.Join("/tmp/", dotnet, Environment.UserName);

            string sdkPathHashed = XxHash128Hasher.HashWithNormalizedCasing(AppContext.BaseDirectory);

            hostServerPath = Path.Join(baseDirectory, Product.TargetFrameworkVersion, sdkPathHashed);

            const int limit = 104;
            Debug.Assert(hostServerPath.Length < limit,
                $"Path '{hostServerPath}' has length {hostServerPath.Length}. The limit is {limit} on Mac.");
        }

        // Create the directory on Linux (it's not a real directory on Windows, it's a virtual \\.\pipe\ namespace there).
        if (createDirectory && !OperatingSystem.IsWindows())
        {
            try
            {
                PathUtility.CreateUserRestrictedDirectory(hostServerPath);
            }
            catch (Exception ex)
            {
                string details = CommandLoggingContext.IsVerbose ? ex.ToString() : ex.Message;
                Reporter.Error.WriteLine($"Cannot create {BuildServerUtility.DotNetHostServerPath} '{hostServerPath}': {details}");
            }
        }

        return hostServerPath;
    }

    public int Execute()
    {
        if (_forwardingApp != null)
        {
            return GetProcessStartInfo().Execute();
        }
        else
        {
            return ExecuteInProc(GetAllArguments());
        }
    }

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

    private static string GetMSBuildExePath()
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            MSBuildExeName);
    }

    private static string GetMSBuildSDKsPath()
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

    private static string GetDotnetPath()
    {
        return new Muxer().MuxerPath;
    }

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
