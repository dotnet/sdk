// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.SetInstallRoot;

internal class SetInstallRootCommand : CommandBase
{
    private readonly string _installType;
    private readonly IDotnetInstallManager _dotnetInstaller;

    public SetInstallRootCommand(ParseResult result, IDotnetInstallManager? dotnetInstaller = null) : base(result)
    {
        _installType = result.GetValue(SetInstallRootCommandParser.InstallTypeArgument)!;
        _dotnetInstaller = dotnetInstaller ?? new DotnetInstallManager();
    }

    public override int Execute()
    {
        return _installType.ToLowerInvariant() switch
        {
            SetInstallRootCommandParser.UserInstallType => SetUserInstallRoot(),
            SetInstallRootCommandParser.AdminInstallType => SetAdminInstallRoot(),
            _ => throw new InvalidOperationException($"Unknown install type: {_installType}")
        };
    }

    [SupportedOSPlatform("windows")]
    private void HandleWindowsAdminPath()
    {
        try
        {
            // Check if admin PATH needs to be changed
            if (WindowsPathHelper.AdminPathContainsProgramFilesDotnet(out var foundDotnetPaths))
            {
                Console.WriteLine("Program Files dotnet path(s) found in admin PATH:");
                foreach (var path in foundDotnetPaths)
                {
                    Console.WriteLine($"  - {path}");
                }
                Console.WriteLine("Removing them...");

                if (Environment.IsPrivilegedProcess)
                {
                    // We're already elevated, modify the admin PATH directly
                    Console.WriteLine("Running with elevated privileges. Modifying admin PATH...");
                    using var pathHelper = new WindowsPathHelper();
                    pathHelper.RemoveDotnetFromAdminPath();
                }
                else
                {
                    // Not elevated, shell out to elevated process
                    Console.WriteLine("Launching elevated process to modify admin PATH...");
                    try
                    {
                        bool succeeded = WindowsPathHelper.StartElevatedProcess("elevatedadminpath removedotnet");
                        if (!succeeded)
                        {
                            Console.Error.WriteLine("Warning: Elevation was cancelled. Admin PATH was not modified.");
                            // Continue anyway - we can still set up the user PATH
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        Console.Error.WriteLine($"Warning: Failed to modify admin PATH: {ex.Message}");
                        Console.Error.WriteLine("You may need to manually remove the Program Files dotnet path from the system PATH.");
                        // Continue anyway - we can still set up the user PATH
                    }
                }
            }
            else
            {
                Console.WriteLine("Admin PATH does not contain Program Files dotnet path. No changes needed.");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Error while checking/modifying admin PATH: {ex.Message}");
            Console.Error.WriteLine("Continuing with user PATH setup...");
        }
    }

    private int SetUserInstallRoot()
    {
        // Get the default user dotnet installation path
        string userDotnetPath = _dotnetInstaller.GetDefaultDotnetInstallPath();

        Console.WriteLine($"Setting up user install root at: {userDotnetPath}");

        // On Windows, check if we need to modify the admin PATH
        if (OperatingSystem.IsWindows())
        {
            HandleWindowsAdminPath();
        }

        // Add the user dotnet path to the user PATH
        try
        {
            Console.WriteLine($"Adding {userDotnetPath} to user PATH...");

            var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            var pathEntries = userPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();

            // Check if the user dotnet path is already in the user PATH
            bool alreadyExists = pathEntries.Any(entry =>
                Path.TrimEndingDirectorySeparator(entry).Equals(
                    Path.TrimEndingDirectorySeparator(userDotnetPath),
                    StringComparison.OrdinalIgnoreCase));

            if (!alreadyExists)
            {
                // Add to the beginning of PATH
                pathEntries.Insert(0, userDotnetPath);
                var newUserPath = string.Join(Path.PathSeparator, pathEntries);
                Environment.SetEnvironmentVariable("PATH", newUserPath, EnvironmentVariableTarget.User);
                Console.WriteLine($"Successfully added {userDotnetPath} to user PATH.");
            }
            else
            {
                Console.WriteLine($"User dotnet path is already in user PATH.");
            }

            // Set DOTNET_ROOT for user
            Environment.SetEnvironmentVariable("DOTNET_ROOT", userDotnetPath, EnvironmentVariableTarget.User);
            Console.WriteLine($"Set DOTNET_ROOT to {userDotnetPath}");

            Console.WriteLine("User install root configured successfully.");
            Console.WriteLine("Note: You may need to restart your terminal or application for the changes to take effect.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to configure user install root: {ex.Message}");
            return 1;
        }
    }

    private int SetAdminInstallRoot()
    {
        Console.WriteLine("Setting up admin install root...");

        // On Windows, add Program Files dotnet path back to admin PATH and remove user settings
        if (OperatingSystem.IsWindows())
        {
            try
            {
                // Add Program Files dotnet path back to admin PATH
                if (Environment.IsPrivilegedProcess)
                {
                    // We're already elevated, modify the admin PATH directly
                    Console.WriteLine("Running with elevated privileges. Modifying admin PATH...");
                    using var pathHelper = new WindowsPathHelper();
                    pathHelper.AddDotnetToAdminPath();
                }
                else
                {
                    // Not elevated, shell out to elevated process
                    Console.WriteLine("Launching elevated process to modify admin PATH...");
                    try
                    {
                        bool succeeded = WindowsPathHelper.StartElevatedProcess("elevatedadminpath adddotnet");
                        if (!succeeded)
                        {
                            Console.Error.WriteLine("Warning: Elevation was cancelled. Admin PATH was not modified.");
                            return 1;
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        Console.Error.WriteLine($"Error: Failed to modify admin PATH: {ex.Message}");
                        Console.Error.WriteLine("You may need to manually add the Program Files dotnet path to the system PATH.");
                        return 1;
                    }
                }

                // Get the user dotnet installation path
                string userDotnetPath = _dotnetInstaller.GetDefaultDotnetInstallPath();

                // Remove user dotnet path from user PATH
                Console.WriteLine($"Removing {userDotnetPath} from user PATH...");
                var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
                var pathEntries = userPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).ToList();

                // Remove entries that match the user dotnet path
                int removedCount = pathEntries.RemoveAll(entry =>
                    Path.TrimEndingDirectorySeparator(entry).Equals(
                        Path.TrimEndingDirectorySeparator(userDotnetPath),
                        StringComparison.OrdinalIgnoreCase));

                if (removedCount > 0)
                {
                    var newUserPath = string.Join(Path.PathSeparator, pathEntries);
                    Environment.SetEnvironmentVariable("PATH", newUserPath, EnvironmentVariableTarget.User);
                    Console.WriteLine($"Successfully removed {userDotnetPath} from user PATH.");
                }
                else
                {
                    Console.WriteLine($"User dotnet path was not found in user PATH.");
                }

                // Unset user DOTNET_ROOT environment variable
                Console.WriteLine("Unsetting user DOTNET_ROOT environment variable...");
                Environment.SetEnvironmentVariable("DOTNET_ROOT", null, EnvironmentVariableTarget.User);
                Console.WriteLine("Successfully unset DOTNET_ROOT.");

                Console.WriteLine("Admin install root configured successfully.");
                Console.WriteLine("Note: You may need to restart your terminal or application for the changes to take effect.");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Failed to configure admin install root: {ex.Message}");
                return 1;
            }
        }
        else
        {
            Console.Error.WriteLine("Error: Admin install root is only supported on Windows.");
            return 1;
        }
    }
}
