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
            if (Environment.IsPrivilegedProcess)
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

                // We're already elevated, modify the admin PATH directly
                using var pathHelper = new WindowsPathHelper();
                pathHelper.RemoveDotnetFromAdminPath();
            }
            else
            {
                // Not elevated, shell out to elevated process
                if (foundDotnetPaths.Count == 1)
                {
                    Console.WriteLine($"Launching elevated process to remove {foundDotnetPaths[0]} from system PATH.");
                }
                else
                {
                    Console.WriteLine("Launching elevated process to remove the following dotnet paths from system PATH:");
                    foreach (var path in foundDotnetPaths)
                    {
                        Console.WriteLine($"  - {path}");
                    }
                }

                bool succeeded = WindowsPathHelper.StartElevatedProcess("removedotnet");
                if (!succeeded)
                {
                    Console.Error.WriteLine("Warning: Elevation was cancelled. System PATH was not modified.");
                    return false;
                }
            }
        }        

        return true;
    }

    private int SetUserInstallRoot()
    {

        // Add the user dotnet path to the user PATH
        try
        {
            // On Windows, check if we need to modify the admin PATH
            if (OperatingSystem.IsWindows())
            {
                // Get the default user dotnet installation path
                string userDotnetPath = _dotnetInstaller.GetDefaultDotnetInstallPath();

                bool needToRemoveAdminPath = WindowsPathHelper.AdminPathContainsProgramFilesDotnet();

                // On Windows, read both expanded and unexpanded user PATH from registry
                string unexpandedUserPath = WindowsPathHelper.ReadUserPath(expand: false);
                string expandedUserPath = WindowsPathHelper.ReadUserPath(expand: true);

                // Use the helper method to add the path while preserving unexpanded variables
                string newUserPath = WindowsPathHelper.AddPathEntry(unexpandedUserPath, expandedUserPath, userDotnetPath);

                bool needToAddToUserPath = newUserPath != unexpandedUserPath;

                var existingDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT", EnvironmentVariableTarget.User);
                bool needToSetDotnetRoot = !string.Equals(userDotnetPath, existingDotnetRoot, StringComparison.OrdinalIgnoreCase);

                if (!needToRemoveAdminPath && !needToAddToUserPath && !needToSetDotnetRoot)
                {
                    Console.WriteLine($"User install root already configured for {userDotnetPath}");
                    return 0;
                }

                Console.WriteLine($"Setting up user install root at: {userDotnetPath}");

                if (needToRemoveAdminPath)
                {
                    if (!HandleWindowsAdminPath())
                    {
                        //  UAC prompt was cancelled
                        return 1;
                    }
                }

                if (needToAddToUserPath)
                {
                    Console.WriteLine($"Adding {userDotnetPath} to user PATH.");
                    WindowsPathHelper.WriteUserPath(newUserPath);
                }

                if (needToSetDotnetRoot)
                {
                    // Set DOTNET_ROOT for user
                    Console.WriteLine($"Setting DOTNET_ROOT to {userDotnetPath}");
                    Environment.SetEnvironmentVariable("DOTNET_ROOT", userDotnetPath, EnvironmentVariableTarget.User);
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
            // On Windows, add Program Files dotnet path back to admin PATH and remove user settings
            if (OperatingSystem.IsWindows())
            {
                // Instead of calling WindowsPathHelper.AdminPathContainsProgramFilesDotnet, we want to check to see if the "primary" Program Files dotnet path is present
                // If Program Files (x86)\dotnet is on the PATH but Program Files\dotnet is not, then we still want to modify the admin PATH
                var programFilesDotnetPaths = WindowsPathHelper.GetProgramFilesDotnetPaths();
                bool needToModifyAdminPath = !WindowsPathHelper.SplitPath(WindowsPathHelper.ReadAdminPath(expand: true)).Contains(programFilesDotnetPaths.First());

                // Get the user dotnet installation path
                string userDotnetPath = _dotnetInstaller.GetDefaultDotnetInstallPath();
                // Read both expanded and unexpanded user PATH from registry to preserve environment variables
                string unexpandedUserPath = WindowsPathHelper.ReadUserPath(expand: false);
                string expandedUserPath = WindowsPathHelper.ReadUserPath(expand: true);
                // Use the helper method to remove the path while preserving unexpanded variables
                string newUserPath = WindowsPathHelper.RemovePathEntries(unexpandedUserPath, expandedUserPath, [userDotnetPath]);

                bool needToModifyUserPath = newUserPath != unexpandedUserPath;

                bool needToUnsetDotnetRoot = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_ROOT", EnvironmentVariableTarget.User));

                if (!needToModifyAdminPath && !needToModifyUserPath && !needToUnsetDotnetRoot)
                {
                    Console.WriteLine("Admin install root already configured.");
                    return 0;
                }

                //Console.WriteLine("Setting up admin install root...");

                if (needToModifyAdminPath)
                {
                    // Add Program Files dotnet path back to admin PATH
                    if (Environment.IsPrivilegedProcess)
                    {
                        // We're already elevated, modify the admin PATH directly
                        Console.WriteLine($"Adding {programFilesDotnetPaths[0]} to system PATH.");
                        using var pathHelper = new WindowsPathHelper();
                        pathHelper.AddDotnetToAdminPath();
                    }
                    else
                    {
                        // Not elevated, shell out to elevated process
                        Console.WriteLine($"Launching elevated process to add {programFilesDotnetPaths[0]} to system PATH.");
                        bool succeeded = WindowsPathHelper.StartElevatedProcess("adddotnet");
                        if (!succeeded)
                        {
                            Console.Error.WriteLine("Warning: Elevation was cancelled. System PATH was not modified.");
                            return 1;
                        }

                    }
                }

                if (needToModifyUserPath)
                {
                    // Remove user dotnet path from user PATH
                    Console.WriteLine($"Removing {userDotnetPath} from user PATH.");
                    WindowsPathHelper.WriteUserPath(newUserPath);
                }

                if (needToUnsetDotnetRoot)
                {
                    // Unset user DOTNET_ROOT environment variable
                    Console.WriteLine("Unsetting DOTNET_ROOT environment variable.");
                    Environment.SetEnvironmentVariable("DOTNET_ROOT", null, EnvironmentVariableTarget.User);
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
