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
            "user" => SetUserInstallRoot(),
            _ => throw new InvalidOperationException($"Unknown install type: {_installType}")
        };
    }

    [SupportedOSPlatform("windows")]
    private void HandleWindowsAdminPath()
    {
        try
        {
            // Check if admin PATH needs to be changed
            if (WindowsPathHelper.AdminPathContainsProgramFilesDotnet())
            {
                Console.WriteLine("Program Files dotnet path found in admin PATH. Removing it...");

                if (WindowsPathHelper.IsElevated())
                {
                    // We're already elevated, modify the admin PATH directly
                    Console.WriteLine("Running with elevated privileges. Modifying admin PATH...");
                    string oldPath = WindowsPathHelper.ReadAdminPath(expand: false);
                    string newPath = WindowsPathHelper.RemoveProgramFilesDotnetFromAdminPath();
                    WindowsPathHelper.WriteAdminPath(newPath);
                    WindowsPathHelper.LogPathChange("SetInstallRoot user - Remove dotnet from admin PATH", oldPath, newPath);
                    WindowsPathHelper.BroadcastEnvironmentChange();
                    Console.WriteLine("Successfully removed Program Files dotnet path from admin PATH.");
                }
                else
                {
                    // Not elevated, shell out to elevated process
                    Console.WriteLine("Launching elevated process to modify admin PATH...");
                    int exitCode = WindowsPathHelper.StartElevatedProcess("elevatedadminpath removedotnet");

                    if (exitCode != 0)
                    {
                        Console.Error.WriteLine("Warning: Failed to modify admin PATH. You may need to manually remove the Program Files dotnet path from the system PATH.");
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
}
