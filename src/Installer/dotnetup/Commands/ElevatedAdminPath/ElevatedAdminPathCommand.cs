// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedAdminPath;

internal class ElevatedAdminPathCommand : CommandBase
{
    private readonly string _operation;
    private readonly string _outputFile;

    public ElevatedAdminPathCommand(ParseResult result) : base(result)
    {
        _operation = result.GetValue(ElevatedAdminPathCommandParser.OperationArgument)!;
        _outputFile = result.GetValue(ElevatedAdminPathCommandParser.OutputFile)!;
    }

    void Log(string message)
    {
        Console.WriteLine(message);
        File.AppendAllText(_outputFile, message + Environment.NewLine);
    }

    protected override string GetCommandName() => "elevatedadminpath";

    protected override int ExecuteCore()
    {
        // This command only works on Windows
        if (!OperatingSystem.IsWindows())
        {
            Log("Error: The elevatedadminpath command is only supported on Windows.");
            return 1;
        }

        // Check if running with elevated privileges
        if (!Environment.IsPrivilegedProcess)
        {
            Log("Error: This operation requires administrator privileges. Please run from an elevated command prompt.");
            return 1;
        }

        try
        {
            return _operation.ToLowerInvariant() switch
            {
                "removedotnet" => RemoveDotnet(),
                "adddotnet" => AddDotnet(),
                _ => throw new InvalidOperationException($"Unknown operation: {_operation}")
            };
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.ToString()}");
            return 1;
        }
    }

    [SupportedOSPlatform("windows")]
    private int RemoveDotnet()
    {
        using var pathHelper = new WindowsPathHelper();
        return pathHelper.RemoveDotnetFromAdminPath();
    }

    [SupportedOSPlatform("windows")]
    private int AddDotnet()
    {
        using var pathHelper = new WindowsPathHelper();
        return pathHelper.AddDotnetToAdminPath();
    }
}
