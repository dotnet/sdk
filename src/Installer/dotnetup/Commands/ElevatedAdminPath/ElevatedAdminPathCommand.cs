// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedAdminPath;

internal class ElevatedAdminPathCommand : CommandBase
{
    private readonly string _operation;

    public ElevatedAdminPathCommand(ParseResult result) : base(result)
    {
        _operation = result.GetValue(ElevatedAdminPathCommandParser.OperationArgument)!;
    }

    public override int Execute()
    {
        // This command only works on Windows
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Error: The elevatedadminpath command is only supported on Windows.");
            return 1;
        }

        // Check if running with elevated privileges
        if (!WindowsPathHelper.IsElevated())
        {
            Console.Error.WriteLine("Error: This operation requires administrator privileges. Please run from an elevated command prompt.");
            return 1;
        }

        return _operation.ToLowerInvariant() switch
        {
            "removedotnet" => RemoveDotnet(),
            "adddotnet" => AddDotnet(),
            _ => throw new InvalidOperationException($"Unknown operation: {_operation}")
        };
    }

    [SupportedOSPlatform("windows")]
    private int RemoveDotnet()
    {
        try
        {
            Console.WriteLine("Reading current admin PATH from registry...");
            string oldPath = WindowsPathHelper.ReadAdminPath();

            if (!WindowsPathHelper.AdminPathContainsProgramFilesDotnet())
            {
                Console.WriteLine("Program Files dotnet path is not present in admin PATH. No changes needed.");
                return 0;
            }

            Console.WriteLine("Removing Program Files dotnet path from admin PATH...");
            string newPath = WindowsPathHelper.RemoveProgramFilesDotnetFromPath(oldPath);

            Console.WriteLine("Writing updated admin PATH to registry...");
            WindowsPathHelper.WriteAdminPath(newPath);

            // Log the changes
            WindowsPathHelper.LogPathChange("Remove dotnet from admin PATH", oldPath, newPath);

            // Broadcast environment change
            WindowsPathHelper.BroadcastEnvironmentChange();

            Console.WriteLine("Successfully removed Program Files dotnet path from admin PATH.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to remove dotnet from admin PATH: {ex.Message}");
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    private int AddDotnet()
    {
        try
        {
            Console.WriteLine("Reading current admin PATH from registry...");
            string oldPath = WindowsPathHelper.ReadAdminPath();

            if (WindowsPathHelper.AdminPathContainsProgramFilesDotnet())
            {
                Console.WriteLine("Program Files dotnet path is already present in admin PATH. No changes needed.");
                return 0;
            }

            Console.WriteLine("Adding Program Files dotnet path to admin PATH...");
            string newPath = WindowsPathHelper.AddProgramFilesDotnetToPath(oldPath);

            Console.WriteLine("Writing updated admin PATH to registry...");
            WindowsPathHelper.WriteAdminPath(newPath);

            // Log the changes
            WindowsPathHelper.LogPathChange("Add dotnet to admin PATH", oldPath, newPath);

            // Broadcast environment change
            WindowsPathHelper.BroadcastEnvironmentChange();

            Console.WriteLine("Successfully added Program Files dotnet path to admin PATH.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Failed to add dotnet to admin PATH: {ex.Message}");
            return 1;
        }
    }
}
