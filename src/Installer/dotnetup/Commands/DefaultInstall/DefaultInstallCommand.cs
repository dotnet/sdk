// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.DefaultInstall;

internal class DefaultInstallCommand : CommandBase
{
    private readonly string _installType;
    private readonly InstallRootManager _installRootManager;
    private readonly IEnvShellProvider? _shellProvider;

    public DefaultInstallCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null) : base(result)
    {
        _installType = result.GetValue(DefaultInstallCommandParser.InstallTypeArgument)!;
        _installRootManager = new InstallRootManager(dotnetEnvironment);
        _shellProvider = result.GetValue(CommonOptions.ShellOption);
    }

    protected override string GetCommandName() => "defaultinstall";

    protected override int ExecuteCore()
    {
        return _installType.ToLowerInvariant() switch
        {
            DefaultInstallCommandParser.UserInstallType => SetUserInstallRoot(),
            DefaultInstallCommandParser.SystemInstallType => SetSystemInstallRoot(),
            _ => throw new InvalidOperationException($"Unknown install type: {_installType}")
        };
    }

    private int SetUserInstallRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            var userDotnetPath = _installRootManager.GetUserInstallRootChanges().UserDotnetPath;
            return SetUnixShellProfile(dotnetupOnly: false, userDotnetPath);
        }

        var changes = _installRootManager.GetUserInstallRootChanges();

        if (!changes.NeedsChange())
        {
            Console.WriteLine($"User install root already configured for {changes.UserDotnetPath}");
            return 0;
        }

        Console.WriteLine($"Setting up user install root at: {changes.UserDotnetPath}");

        bool succeeded = InstallRootManager.ApplyUserInstallRoot(
            changes,
            Console.WriteLine,
            Console.Error.WriteLine);

        if (!succeeded)
        {
            // UAC prompt was cancelled
            return 1;
        }

        Console.WriteLine("Succeeded. NOTE: You may need to restart your terminal or application for the changes to take effect.");
        return 0;
    }

    private int SetSystemInstallRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            return SetUnixShellProfile(dotnetupOnly: true);
        }

        var changes = _installRootManager.GetAdminInstallRootChanges();

        if (!changes.NeedsChange())
        {
            Console.WriteLine("System install root already configured.");
            return 0;
        }

        bool succeeded = InstallRootManager.ApplyAdminInstallRoot(
            changes,
            Console.WriteLine,
            Console.Error.WriteLine);

        if (!succeeded)
        {
            // Elevation was cancelled
            return 1;
        }

        Console.WriteLine("Succeeded. NOTE: You may need to restart your terminal or application for the changes to take effect.");
        return 0;
    }

    private int SetUnixShellProfile(bool dotnetupOnly, string? dotnetInstallPath = null)
    {
        var dotnetupPath = ShellProviderHelpers.GetDotnetupExecutablePathOrThrow();
        var shellProvider = GetCurrentShellProviderOrThrow();

        var modifiedFiles = ShellProfileManager.AddProfileEntries(
            shellProvider,
            dotnetupPath,
            dotnetupOnly,
            dotnetInstallPath);

        if (modifiedFiles.Count == 0)
        {
            Console.WriteLine(dotnetupOnly
                ? "Shell profile is already configured."
                : "Shell profile is already configured for dotnetup.");
        }
        else
        {
            Console.WriteLine(dotnetupOnly
                ? "Updated shell profile files (dotnetup only, no DOTNET_ROOT or dotnet PATH):"
                : "Updated shell profile files:");

            foreach (var file in modifiedFiles)
            {
                Console.WriteLine($"  {file}");
            }
        }

        if (!dotnetupOnly)
        {
            Console.WriteLine();
            Console.WriteLine("To start using .NET in this terminal, run:");
            Console.WriteLine($"  {shellProvider.GenerateActivationCommand(dotnetupPath, dotnetInstallPath: dotnetInstallPath)}");
        }

        return 0;
    }

    private IEnvShellProvider GetCurrentShellProviderOrThrow()
    {
        var shellProvider = _shellProvider ?? ShellDetection.GetCurrentShellProvider();
        if (shellProvider is null)
        {
            var shellEnv = Environment.GetEnvironmentVariable("SHELL") ?? "(not set)";
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                $"Unable to detect a supported shell. SHELL={shellEnv}. Supported shells: {string.Join(", ", ShellDetection.s_supportedShells.Select(s => s.ArgumentName))}. You can specify one explicitly with --shell.");
        }

        return shellProvider;
    }
}
