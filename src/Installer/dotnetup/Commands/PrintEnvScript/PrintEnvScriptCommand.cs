// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.PrintEnvScript;

internal class PrintEnvScriptCommand : CommandBase
{
    private readonly IEnvShellProvider? _shellProvider;
    private readonly string? _dotnetInstallPath;
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly bool _dotnetupOnly;

    public PrintEnvScriptCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null) : base(result)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _shellProvider = result.GetValue(PrintEnvScriptCommandParser.ShellOption);
        _dotnetInstallPath = result.GetValue(PrintEnvScriptCommandParser.DotnetInstallPathOption);
        _dotnetupOnly = result.GetValue(PrintEnvScriptCommandParser.DotnetupOnlyOption);
    }

    protected override string GetCommandName() => "print-env-script";

    protected override int ExecuteCore()
    {
        // Check if shell provider was successfully determined
        if (_shellProvider == null)
        {
            var shellPath = Environment.GetEnvironmentVariable("SHELL");
            if (shellPath is null)
            {
                Console.Error.WriteLine("Error: Unable to detect current shell. The SHELL environment variable is not set.");
                Console.Error.WriteLine($"Please specify the shell using --shell option. Supported shells: {string.Join(", ", ShellDetection.s_supportedShells.Select(s => s.ArgumentName))}");
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.PlatformNotSupported,
                    "SHELL environment variable is not set; cannot detect shell.");
            }

            var shellName = Path.GetFileName(shellPath);
            Console.Error.WriteLine($"Error: Unsupported shell '{shellName}'.");
            Console.Error.WriteLine($"Supported shells: {string.Join(", ", ShellDetection.s_supportedShells.Select(s => s.ArgumentName))}");
            Console.Error.WriteLine("Please specify the shell using --shell option.");
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                $"Unsupported shell '{shellName}'.");
        }

        // Determine the dotnet install path
        string installPath = _dotnetInstallPath ?? _dotnetEnvironment.GetDefaultDotnetInstallPath();

        // Determine the dotnetup directory so it can be added to PATH
        string dotnetupDir = ShellProviderHelpers.GetDotnetupDirectoryOrThrow();

        // Generate the shell script
        bool includeDotnet = !_dotnetupOnly;
        string script = _shellProvider.GenerateEnvScript(installPath, dotnetupDir, includeDotnet);

        WriteScriptToStandardOutput(script);

        return 0;
    }

    internal static void WriteScriptToStandardOutput(string script)
    {
        using Stream standardOutput = Console.OpenStandardOutput();
        WriteScript(standardOutput, script);
    }

    internal static void WriteScript(Stream output, string script)
    {
        // Emit machine-readable script output directly to the stdout stream so
        // console formatting state cannot rewrite or decorate the content.
        using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        writer.Write(script);
        writer.Flush();
    }
}
