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
    private bool HandleWindowsAdminPath()
    {
        // Check if admin PATH needs to be changed
        if (WindowsPathHelper.AdminPathContainsProgramFilesDotnet(out var foundDotnetPaths))
        {
            if (foundDotnetPaths.Count == 1)
            {
                Console.WriteLine($"Removing {foundDotnetPaths[0]} from system PATH.");
            }
            else
            {
                Console.WriteLine("Removing the following dotnet paths from system PATH:");
                foreach (var path in foundDotnetPaths)
                {
                    Console.WriteLine($"  - {path}");
                }
            }

            if (Environment.IsPrivilegedProcess)
            {
                // We're already elevated, modify the admin PATH directly
                using var pathHelper = new WindowsPathHelper();
                pathHelper.RemoveDotnetFromAdminPath();
            }
            else
            {
                // Not elevated, shell out to elevated process
                Console.WriteLine("Launching elevated process to modify system PATH...");

                bool succeeded = WindowsPathHelper.StartElevatedProcess("elevatedadminpath removedotnet");
                if (!succeeded)
                {
                    Console.Error.WriteLine("Warning: Elevation was cancelled. Admin PATH was not modified.");
                    return false;
                }
            }
        }        

        return true;
    }

    private int SetUserInstallRoot()
    {
        // Get the default user dotnet installation path
        string userDotnetPath = _dotnetInstaller.GetDefaultDotnetInstallPath();

        Console.WriteLine($"Setting up user install root at: {userDotnetPath}");


        // Add the user dotnet path to the user PATH
        try
        {
            // On Windows, check if we need to modify the admin PATH
            if (OperatingSystem.IsWindows())
            {
                if (!HandleWindowsAdminPath())
                {
                    //  UAC prompt was cancelled
                    return 1;
                }
            }

            Console.WriteLine($"Adding {userDotnetPath} to user PATH...");

            if (OperatingSystem.IsWindows())
            {
                // On Windows, read both expanded and unexpanded user PATH from registry
                string unexpandedUserPath = WindowsPathHelper.ReadUserPath(expand: false);
                string expandedUserPath = WindowsPathHelper.ReadUserPath(expand: true);

                // Use the helper method to add the path while preserving unexpanded variables
                string newUserPath = WindowsPathHelper.AddPathEntry(unexpandedUserPath, expandedUserPath, userDotnetPath);

                if (newUserPath != unexpandedUserPath)
                {
                    WindowsPathHelper.WriteUserPath(newUserPath);
                    Console.WriteLine($"Successfully added {userDotnetPath} to user PATH.");
                }
                else
                {
                    Console.WriteLine($"User dotnet path is already in user PATH.");
                }
            }
            else
            {
                // On non-Windows, use Environment API which expands variables
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
            Console.Error.WriteLine($"Error: Failed to configure user install root: {ex.ToString()}");
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

                // Read both expanded and unexpanded user PATH from registry to preserve environment variables
                string unexpandedUserPath = WindowsPathHelper.ReadUserPath(expand: false);
                string expandedUserPath = WindowsPathHelper.ReadUserPath(expand: true);

                // Use the helper method to remove the path while preserving unexpanded variables
                string newUserPath = WindowsPathHelper.RemovePathEntries(unexpandedUserPath, expandedUserPath, [userDotnetPath]);

                if (newUserPath != unexpandedUserPath)
                {
                    WindowsPathHelper.WriteUserPath(newUserPath);
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
