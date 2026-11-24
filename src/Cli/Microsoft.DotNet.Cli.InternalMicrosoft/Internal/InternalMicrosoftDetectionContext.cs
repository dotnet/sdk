// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli.InternalMicrosoft;

/// <summary>
/// Defines the platform state and external operations that detection providers can use.
/// Tests replace this complete value instead of overriding individual detector constructor parameters.
/// </summary>
internal sealed record InternalMicrosoftDetectionContextOptions(
    HttpMessageHandler? GitHubHttpMessageHandler,
    Func<string, IReadOnlyList<string>, CancellationToken, Task<InternalMicrosoftProcessResult>> RunProcess,
    Func<string, string?> GetEnvironmentVariable,
    Func<IEnumerable<(string Name, string? Value)>> GetEnvironmentVariables,
    Func<string, bool> CommandExists,
    bool IsWindows,
    bool IsMacOS,
    bool IsLinux,
    bool IsWsl,
    string MacPlatformSsoPath,
    string GitHubUserAgentVersion,
    TimeSpan GitHubHttpTimeout,
    TimeSpan GitHubCandidateTimeout);

/// <summary>
/// Provides immutable dependencies and platform state to all providers in one detector run.
/// A detector owns one context and shares it with each selected provider.
/// </summary>
internal sealed class InternalMicrosoftDetectionContext
{
    private const string DefaultMacPlatformSsoPath = "/usr/bin/app-sso";

    private static readonly TimeSpan s_processProbeTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan s_gitHubHttpTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan s_gitHubCandidateTimeout = TimeSpan.FromSeconds(5);

    private readonly bool _isCIEnvironment;

    internal InternalMicrosoftDetectionContext(
        string homeDirectory,
        bool isCIEnvironment,
        InternalMicrosoftDetectionContextOptions options)
    {
        HomeDirectory = homeDirectory;
        _isCIEnvironment = isCIEnvironment;
        GitHubHttpMessageHandler = options.GitHubHttpMessageHandler;
        RunProcess = options.RunProcess;
        GetEnvironmentVariable = options.GetEnvironmentVariable;
        GetEnvironmentVariables = options.GetEnvironmentVariables;
        CommandExists = options.CommandExists;
        IsWindows = options.IsWindows;
        IsMacOS = options.IsMacOS;
        IsLinux = options.IsLinux;
        IsWsl = options.IsWsl;
        MacPlatformSsoPath = options.MacPlatformSsoPath;
        GitHubUserAgentVersion = options.GitHubUserAgentVersion;
        GitHubHttpTimeout = options.GitHubHttpTimeout;
        GitHubCandidateTimeout = options.GitHubCandidateTimeout;
    }

    internal string HomeDirectory { get; }
    internal HttpMessageHandler? GitHubHttpMessageHandler { get; }
    internal Func<string, IReadOnlyList<string>, CancellationToken, Task<InternalMicrosoftProcessResult>> RunProcess { get; }
    internal Func<string, string?> GetEnvironmentVariable { get; }
    internal Func<IEnumerable<(string Name, string? Value)>> GetEnvironmentVariables { get; }
    internal Func<string, bool> CommandExists { get; }
    internal bool IsWindows { get; }
    internal bool IsMacOS { get; }
    internal bool IsLinux { get; }
    internal bool IsWsl { get; }
    internal string MacPlatformSsoPath { get; }
    internal string GitHubUserAgentVersion { get; }
    internal TimeSpan GitHubHttpTimeout { get; }
    internal TimeSpan GitHubCandidateTimeout { get; }
    internal bool IsCIEnvironment => _isCIEnvironment;

    /// <summary>
    /// Creates the immutable context for the current process.
    /// </summary>
    internal static InternalMicrosoftDetectionContext CreateDefault(
        string homeDirectory,
        bool isCIEnvironment,
        string gitHubUserAgentVersion)
    {
        var isLinux = OperatingSystem.IsLinux();
        return new(
            homeDirectory,
            isCIEnvironment,
            new(
                GitHubHttpMessageHandler: null,
                RunProcess: RunProcessAsync,
                GetEnvironmentVariable: Environment.GetEnvironmentVariable,
                GetEnvironmentVariables: GetProcessEnvironmentVariables,
                CommandExists: DefaultCommandExists,
                IsWindows: OperatingSystem.IsWindows(),
                IsMacOS: OperatingSystem.IsMacOS(),
                IsLinux: isLinux,
                IsWsl: DetectWsl(isLinux, Environment.GetEnvironmentVariable),
                MacPlatformSsoPath: DefaultMacPlatformSsoPath,
                GitHubUserAgentVersion: gitHubUserAgentVersion,
                s_gitHubHttpTimeout,
                s_gitHubCandidateTimeout));
    }

    /// <summary>
    /// Runs one bounded process probe and converts expected failures into diagnostic data.
    /// </summary>
    internal async Task<InternalMicrosoftProcessResult> RunProcessProbeAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!CommandExists(fileName))
        {
            return new("", "", null, null);
        }

        using var timeoutSource = new CancellationTokenSource(s_processProbeTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            var result = await RunProcess(fileName, arguments, linkedSource.Token).ConfigureAwait(false);
            return result.ExitCode == 0
                ? result
                : result with
                {
                    Failure = new(
                        InternalMicrosoftProbeFailureCode.ProcessExit,
                        InternalMicrosoftProbeFailureStage.Process,
                        ProcessExitCode: result.ExitCode)
                };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new("", "", null, new(
                InternalMicrosoftProbeFailureCode.ProcessTimeout,
                InternalMicrosoftProbeFailureStage.Process,
                ExceptionType: nameof(TaskCanceledException)));
        }
        catch (Exception exception)
        {
            return new(
                "",
                "",
                null,
                InternalMicrosoftDetectionUtilities.CreateExceptionFailure(
                    exception,
                    InternalMicrosoftProbeFailureStage.Process));
        }
    }

    internal static string GetHomeDirectory() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    internal static bool DetectWsl(bool isLinux, Func<string, string?> getEnvironmentVariable)
    {
        if (!isLinux)
        {
            return false;
        }

        var distroName = getEnvironmentVariable("WSL_DISTRO_NAME");
        var interop = getEnvironmentVariable("WSL_INTEROP");
        if (!string.IsNullOrEmpty(distroName) || !string.IsNullOrEmpty(interop))
        {
            return true;
        }

        try
        {
            const string KernelReleaseFile = "/proc/sys/kernel/osrelease";
            return File.Exists(KernelReleaseFile) &&
                InternalMicrosoftDetectionUtilities.IsWsl(
                    distroName,
                    interop,
                    File.ReadAllText(KernelReleaseFile));
        }
        catch
        {
            return false;
        }
    }

    private static async Task<InternalMicrosoftProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            catch (Win32Exception)
            {
            }

            ObserveFault(standardOutput);
            ObserveFault(standardError);
            throw;
        }

        return new(
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false),
            process.ExitCode,
            null);
    }

    private static void ObserveFault(Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static IEnumerable<(string Name, string? Value)> GetProcessEnvironmentVariables()
    {
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name)
            {
                yield return (name, entry.Value as string);
            }
        }
    }

    private static bool DefaultCommandExists(string command)
    {
        if (Path.IsPathFullyQualified(command))
        {
            return File.Exists(command);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM").Split(';')
            : [string.Empty];
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory.Trim(), command);
                if (Path.HasExtension(candidate) || string.IsNullOrEmpty(extension))
                {
                    if (File.Exists(candidate))
                    {
                        return true;
                    }
                }
                else if (File.Exists(candidate + extension))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
