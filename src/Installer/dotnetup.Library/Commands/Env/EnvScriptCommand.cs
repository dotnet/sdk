// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using Microsoft.DotNet.Tools.Bootstrapper.Shell;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;

internal class EnvScriptCommand : CommandBase
{
    private readonly IEnvShellProvider? _shellProvider;
    private readonly string? _dotnetInstallPath;
    private readonly IDotnetEnvironmentManager _dotnetEnvironment;
    private readonly bool _dotnet;
    private readonly bool _dotnetup;
    private readonly bool _dotnetupOnly;

    public EnvScriptCommand(ParseResult result, IDotnetEnvironmentManager? dotnetEnvironment = null) : base(result)
    {
        _dotnetEnvironment = dotnetEnvironment ?? new DotnetEnvironmentManager();
        _shellProvider = result.GetValue(EnvScriptCommandParser.ShellOption);
        _dotnetInstallPath = result.GetValue(EnvScriptCommandParser.DotnetInstallPathOption);
        _dotnet = result.GetValue(EnvScriptCommandParser.DotnetOption);
        _dotnetup = result.GetValue(EnvScriptCommandParser.DotnetupOption);
        _dotnetupOnly = result.GetValue(EnvScriptCommandParser.DotnetupOnlyOption);
    }

    protected override string GetCommandName() => "env script";

    protected override void ExecuteCore()
    {
        // Check if shell provider was successfully determined
        if (_shellProvider == null)
        {
            var shellPath = Environment.GetEnvironmentVariable("SHELL");
            string supportedShells = string.Join(", ", ShellDetection.s_supportedShells.Select(s => s.ArgumentName));
            if (shellPath is null)
            {
                Console.Error.WriteLine(Strings.EnvScriptShellNotDetected);
                Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.EnvScriptSpecifyShellWithSupported, supportedShells));
                throw new DotnetInstallException(
                    DotnetInstallErrorCode.PlatformNotSupported,
                    Strings.EnvScriptShellNotSet);
            }

            var shellName = Path.GetFileName(shellPath);
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.EnvScriptUnsupportedShell, shellName));
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, Strings.EnvScriptSupportedShells, supportedShells));
            Console.Error.WriteLine(Strings.EnvScriptSpecifyShell);
            throw new DotnetInstallException(
                DotnetInstallErrorCode.PlatformNotSupported,
                string.Format(CultureInfo.InvariantCulture, Strings.EnvScriptUnsupportedShellError, shellName));
        }

        EnvScriptSelection selection = EnvScriptSelectionResolver.Resolve(_dotnet, _dotnetup, _dotnetupOnly, DotnetupConfig.Read());

        // Determine the dotnet install path
        string installPath = _dotnetInstallPath ?? _dotnetEnvironment.GetDefaultDotnetInstallPath();

        // Determine the dotnetup directory so it can be added to PATH. Passing an empty
        // string omits the dotnetup PATH entry from the generated script.
        string dotnetupDir = selection.IncludeDotnetup ? ShellProviderHelpers.GetDotnetupDirectoryOrThrow() : string.Empty;

        // Generate the shell script
        string script = _shellProvider.GenerateEnvScript(installPath, dotnetupDir, selection.IncludeDotnet);

        WriteScriptToStandardOutput(script);
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
