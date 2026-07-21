// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;

internal class ElevatedSystemPathCommand : CommandBase
{
    private readonly string _operation;
    private readonly string _outputFile;
    private readonly string? _dotnetDir;

    public ElevatedSystemPathCommand(ParseResult result) : base(result, "elevatedsystempath")
    {
        _operation = result.GetValue(ElevatedSystemPathCommandParser.OperationArgument)!;
        _outputFile = result.GetValue(ElevatedSystemPathCommandParser.OutputFile)!;
        _dotnetDir = result.GetValue(ElevatedSystemPathCommandParser.DotnetDir);
    }

    private void Log(string message)
    {
        Console.WriteLine(message);
        File.AppendAllText(_outputFile, message + Environment.NewLine);
    }

    protected override void ExecuteCore()
    {
        // This command only works on Windows
        if (!OperatingSystem.IsWindows())
        {
            const string message = "The elevatedsystempath command is only supported on Windows.";
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
            // Forward the helper's 0/1 exit code so the parent (which spawned
            // this elevated child process) reads success/failure correctly.
            // SetExitCode is the documented escape hatch for forwarding cases.
            SetExitCode(_operation.ToLowerInvariant() switch
            {
                "insertdotnet" => InsertDotnet(_dotnetDir!),
                "removedotnet" => RemoveDotnet(_dotnetDir!),
                _ => throw new InvalidOperationException($"Unknown operation: {_operation}")
            });
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
    private static int InsertDotnet(string dotnetDir)
    {
        using var pathHelper = new WindowsPathHelper();
        return pathHelper.InsertDotnetIntoSystemPath(dotnetDir);
    }

    [SupportedOSPlatform("windows")]
    private static int RemoveDotnet(string dotnetDir)
    {
        using var pathHelper = new WindowsPathHelper();
        return pathHelper.RemoveDotnetFromSystemPath(dotnetDir);
    }
}
