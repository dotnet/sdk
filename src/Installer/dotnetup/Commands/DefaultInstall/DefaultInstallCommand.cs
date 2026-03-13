// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.DefaultInstall;

internal class DefaultInstallCommand : CommandBase
{
    private readonly string _installType;
    private readonly InstallRootManager _installRootManager;

    public DefaultInstallCommand(ParseResult result, IDotnetInstallManager? dotnetInstaller = null) : base(result)
    {
        _installType = result.GetValue(DefaultInstallCommandParser.InstallTypeArgument)!;
        _installRootManager = new InstallRootManager(dotnetInstaller);
    }

    protected override string GetCommandName() => "defaultinstall";

    protected override int ExecuteCore()
    {
        return _installType.ToLowerInvariant() switch
        {
            DefaultInstallCommandParser.UserInstallType => SetUserInstallRoot(),
            DefaultInstallCommandParser.AdminInstallType => SetAdminInstallRoot(),
            _ => throw new InvalidOperationException($"Unknown install type: {_installType}")
        };
    }

    private int SetUserInstallRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            var dotnetupPath = Environment.ProcessPath
                ?? throw new DotnetInstallException(DotnetInstallErrorCode.Unknown, "Unable to determine the dotnetup executable path.");

            IEnvShellProvider? shellProvider = ShellDetection.GetCurrentShellProvider();
            if (shellProvider is null)
            {
                var shellEnv = Environment.GetEnvironmentVariable("SHELL") ?? "(not set)";
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.PlatformNotSupported,
                    $"Unable to detect a supported shell. SHELL={shellEnv}. Supported shells: {string.Join(", ", PrintEnvScriptCommandParser.s_supportedShells.Select(s => s.ArgumentName))}");
            }

            var modifiedFiles = ShellProfileManager.AddProfileEntries(shellProvider, dotnetupPath);

            if (modifiedFiles.Count == 0)
            {
                Console.WriteLine("Shell profile is already configured for dotnetup.");
            }
            else
            {
                Console.WriteLine("Updated shell profile files:");
                foreach (var file in modifiedFiles)
                {
                    Console.WriteLine($"  {file}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("To start using .NET in this terminal, run:");
            Console.WriteLine($"  {shellProvider.GenerateActivationCommand(dotnetupPath)}");

            return 0;
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

    private int SetAdminInstallRoot()
    {
        if (!OperatingSystem.IsWindows())
        {
            // On Unix, switching to admin means the system manages dotnet.
            // Replace profile entries with dotnetup-only (keeps dotnetup on PATH but removes DOTNET_ROOT and dotnet PATH).
            var dotnetupPath = Environment.ProcessPath
                ?? throw new DotnetInstallException(DotnetInstallErrorCode.Unknown, "Unable to determine the dotnetup executable path.");

            IEnvShellProvider? shellProvider = ShellDetection.GetCurrentShellProvider();
            if (shellProvider is null)
            {
                var shellEnv = Environment.GetEnvironmentVariable("SHELL") ?? "(not set)";
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.PlatformNotSupported,
                    $"Unable to detect a supported shell. SHELL={shellEnv}. Supported shells: {string.Join(", ", PrintEnvScriptCommandParser.s_supportedShells.Select(s => s.ArgumentName))}");
            }

            var modifiedFiles = ShellProfileManager.ReplaceProfileEntries(shellProvider, dotnetupPath, dotnetupOnly: true);

            if (modifiedFiles.Count == 0)
            {
                Console.WriteLine("Shell profile is already configured.");
            }
            else
            {
                Console.WriteLine("Updated shell profile files (dotnetup only, no DOTNET_ROOT or dotnet PATH):");
                foreach (var file in modifiedFiles)
                {
                    Console.WriteLine($"  {file}");
                }
            }

            return 0;
        }

        var changes = _installRootManager.GetAdminInstallRootChanges();

        if (!changes.NeedsChange())
        {
            Console.WriteLine("Admin install root already configured.");
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
}
