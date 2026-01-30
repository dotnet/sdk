// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

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
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var changes = _installRootManager.GetUserInstallRootChanges();

                if (!changes.NeedsChange())
                {
                    Console.WriteLine($"User install root already configured for {changes.UserDotnetPath}");
                    return 0;
                }

                Console.WriteLine($"Setting up user install root at: {changes.UserDotnetPath}");

                bool succeeded = _installRootManager.ApplyUserInstallRoot(
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
            else
            {
                Console.Error.WriteLine("Error: Non-Windows platforms not yet supported");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to configure user install root: {ex.ToString()}");
            return 1;
        }
    }

    private int SetAdminInstallRoot()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var changes = _installRootManager.GetAdminInstallRootChanges();

                if (!changes.NeedsChange())
                {
                    Console.WriteLine("Admin install root already configured.");
                    return 0;
                }

                bool succeeded = _installRootManager.ApplyAdminInstallRoot(
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
            else
            {
                Console.Error.WriteLine("Error: Admin install root is only supported on Windows.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to configure admin install root: {ex.ToString()}");
            return 1;
        }
    }
}
