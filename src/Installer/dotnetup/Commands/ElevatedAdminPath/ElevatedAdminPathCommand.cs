// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
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

    private void Log(string message)
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
            const string message = "The elevatedadminpath command is only supported on Windows.";
            Log("Error: " + message);
            throw new DotnetInstallException(DotnetInstallErrorCode.PlatformNotSupported, message);
        }

        // Check if running with elevated privileges
        if (!Environment.IsPrivilegedProcess)
        {
            const string message = "This operation requires administrator privileges. Please run from an elevated command prompt.";
            Log("Error: " + message);
            throw new DotnetInstallException(DotnetInstallErrorCode.PermissionDenied, message);
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
            // This command runs in an elevated child process; the parent reads
            // diagnostics from the shared output file, so we must Log before
            // letting the exception bubble up to CommandBase for telemetry.
            Log($"Error: {ex}");
            throw;
        }
    }

    [SupportedOSPlatform("windows")]
    private static int RemoveDotnet()
    {
        using var pathHelper = new WindowsPathHelper();
        return pathHelper.RemoveDotnetFromAdminPath();
    }

    [SupportedOSPlatform("windows")]
    private static int AddDotnet()
    {
        using var pathHelper = new WindowsPathHelper();
        return pathHelper.AddDotnetToAdminPath();
    }
}
