// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Dotnet;

/// <summary>
/// Forwards commands to the dotnet executable managed by dotnetup.
/// This allows users to run <c>dotnetup dotnet build</c> (or <c>dotnetup do build</c>)
/// and have it resolve to the correct dotnet installation, even when the system PATH
/// has been overridden by other installers (e.g., Visual Studio).
/// </summary>
internal class DotnetCommand : CommandBase
{
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly string[] _forwardedArgs;

    public DotnetCommand(ParseResult parseResult, IDotnetEnvironmentManager? dotnetEnvironment = null) : base(parseResult)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();

        // Collect all unmatched/forwarded tokens after the "dotnet" or "do" subcommand.
        _forwardedArgs = [.. parseResult.UnmatchedTokens];
    }

    protected override string GetCommandName() => "dotnet";

    protected override int ExecuteCore()
    {
        string dotnetPath = ResolveDotnetPath();
        string dotnetExe = GetDotnetExecutable(dotnetPath);

        if (!File.Exists(dotnetExe))
        {
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.DotnetCommandDotnetNotFound, dotnetExe));
            Console.Error.WriteLine(Strings.DotnetCommandInstallFirst);
            // User-level error: they invoked `dotnetup dotnet ...` before installing
            // a .NET SDK/runtime. Tag for telemetry so it doesn't show as an unknown
            // product failure.
            throw new DotnetInstallException(
                DotnetInstallErrorCode.ContextResolutionFailed,
                $"dotnet executable not found at '{dotnetExe}'.");
        }

        return RunDotnet(dotnetExe, dotnetPath, _forwardedArgs);
    }

    /// <summary>
    /// Resolves the dotnet installation path using the same logic as other dotnetup commands:
    /// configured install type (user install) falls back to the default install path.
    /// </summary>
    private string ResolveDotnetPath()
    {
        var configuredRoot = _dotnetEnvironment.GetCurrentPathConfiguration();
        if (configuredRoot is not null && configuredRoot.InstallType == InstallType.User)
        {
            return configuredRoot.Path;
        }

        return _dotnetEnvironment.GetDefaultDotnetInstallPath();
    }

    /// <summary>
    /// Gets the full path to the dotnet executable within the install root.
    /// </summary>
    private static string GetDotnetExecutable(string dotnetPath)
    {
        string exeName = $"dotnet{DotnetupUtilities.ExeSuffix}";
        return Path.Combine(dotnetPath, exeName);
    }

    /// <summary>
    /// Spawns dotnet with the forwarded arguments, setting DOTNET_ROOT and prepending
    /// the install path to PATH. Inherits the parent console handles (stdin/stdout/stderr)
    /// so that redirection, piping, and interactive mode work transparently.
    /// </summary>
    protected virtual int RunDotnet(string dotnetExe, string dotnetRoot, string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetExe,
            UseShellExecute = false,
            RedirectStandardInput = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };

        // Forward all arguments
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        // Set DOTNET_ROOT to the resolved hive so runtime interactions work correctly
        startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;

        // Prepend the install path to PATH so child processes also resolve correctly
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        startInfo.Environment["PATH"] = dotnetRoot + Path.PathSeparator + currentPath;

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine(Strings.DotnetCommandDotnetStartFailed);
            // Process.Start returning null is a system-level failure we should be
            // able to act on; classify as a product error via the default mapping.
            throw new DotnetInstallException(
                DotnetInstallErrorCode.Unknown,
                $"Failed to start process '{dotnetExe}'.");
        }

        process.WaitForExit();
        return process.ExitCode;
    }
}
