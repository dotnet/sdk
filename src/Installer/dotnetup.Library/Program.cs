// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;
using Microsoft.DotNet.Tools.Bootstrapper.Telemetry;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Entry point class for dotnetup. Invoked by the NativeAOT shim in
/// src/Installer/dotnetup/Program.cs.
/// </summary>
public class DotnetupProgram
{
    public static int Main(string[] args)
    {
        _ = DotnetupProcessInfo.ExecutablePath;
        _ = DotnetupProcessInfo.BuildIdentity;
        // Detached telemetry-drainer fast path: deliver previously-persisted telemetry and exit,
        // before any other work. See DotnetupTelemetryDrainProcess for the full delivery model.
        if (DotnetupTelemetryDrainProcess.TryRunAsDrainer(args, out var drainExitCode))
        {
            return drainExitCode;
        }

        return InvokeCommand(args, static () => new AutomaticEncodingRestorer());
    }

    internal static int InvokeCommand(string[] args, Func<IDisposable> createEncodingRestorer)
    {
        TrackedOperation? rootOperation = null;
        SelfUpdateInvocation? invocation = null;
        int processExitCode = 1;
        bool identityInvocation = false;

        try
        {
            if (args.Contains("--build-identity", StringComparer.Ordinal))
            {
                var parseResult = Parser.Parse(args);
                identityInvocation = ReferenceEquals(parseResult.Action, Parser.BuildIdentityOption.Action);
                if (identityInvocation)
                {
                    return Parser.Invoke(parseResult);
                }
            }

            rootOperation = DotnetupTelemetry.Instance.StartTrackedProcess("dotnetup");
            processExitCode = ExecuteCommand(args, createEncodingRestorer, ref invocation);
            return processExitCode;
        }
        catch (Exception ex)
        {
            processExitCode = 1;
            if (!identityInvocation)
            {
                rootOperation ??= DotnetupTelemetry.Instance.StartTrackedProcess("dotnetup");
                DotnetupTelemetry.Instance.RecordException(rootOperation, ex);
            }

            // Log the error and return non-zero exit code
            Console.Error.WriteLine($"Error: {ex.Message}");
#if DEBUG
            Console.Error.WriteLine(ex.StackTrace);
#endif
            return 1;
        }
        finally
        {
            try
            {
                if (rootOperation is not null)
                {
                    TagRootForExitCode(rootOperation, processExitCode);
                    rootOperation.Dispose();
                    FlushTelemetry(processExitCode);
                }
            }
            finally
            {
                invocation?.Dispose();
            }
        }
    }

    private static int ExecuteCommand(string[] args, Func<IDisposable> createEncodingRestorer, ref SelfUpdateInvocation? invocation)
    {
        DotnetupUILanguage.Setup();
        DotnetupDebugHelper.HandleDebugSwitch(ref args);
        using var encodingRestorer = createEncodingRestorer();
        ConfigureConsoleEncoding();
        ConfigureConsoleOutput();
        FirstRunNotice.ShowIfFirstRun(DotnetupTelemetry.Instance.Enabled);
        if (DotnetupProcessInfo.IsDirectExecution && DotnetupProcessInfo.ExecutablePath is { } executablePath)
        {
            invocation = new SelfUpdateInvocation(executablePath, DotnetupProcessInfo.BuildIdentity);
        }

        return Parser.Invoke(args);
    }

    /// <summary>
    /// Stamps the final exit code, status, and (if applicable) a synthetic
    /// <c>error.type=ParseError</c> tag on the root op. The synthetic tag
    /// covers the case where the parser returned a non-zero exit code
    /// without throwing (e.g., System.CommandLine validation failure that
    /// printed usage and returned non-zero) — without this, the failure
    /// would have no <c>error.*</c> tags anywhere and would disappear from
    /// the dashboard's root-error queries.
    /// </summary>
    private static void TagRootForExitCode(TrackedOperation rootOp, int processExitCode)
    {
        if (processExitCode != 0 && rootOp.Activity?.GetTagItem("error.type") is null)
        {
            rootOp.Tag("error.type", "ParseError");
            rootOp.Tag("error.category", "user");
        }

        rootOp.Tag(TelemetryTagNames.ExitCode, processExitCode);
        rootOp.SetStatus(processExitCode == 0 ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
    }

    private static void FlushTelemetry(int exitCode)
    {
        try
        {
            // Flush, never Dispose: process exit reclaims the providers, and Dispose
            // would trigger the OTel LoggerProvider's unbounded Shutdown drain.
            DotnetupTelemetry.Instance.Flush(exitCode);
            DotnetupTelemetry.Instance.WriteLogIfNecessary();
        }
        catch
        {
            // Telemetry should never delay or crash the process exit.
        }
    }

    /// <summary>
    /// Sets the console output encoding to UTF-8 so Unicode glyphs render correctly.
    /// Uses UILanguageOverride.OperatingSystemSupportsUtf8() from the .NET SDK CLI.
    /// </summary>
    private static void ConfigureConsoleEncoding()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_CLI_CONSOLE_USE_DEFAULT_ENCODING") != "1"
            && UILanguageOverride.OperatingSystemSupportsUtf8())
        {
            // Use UTF-8 without BOM to prevent corrupting piped output.
            // Encoding.UTF8 has encoderShouldEmitUTF8Identifier=true, which causes the
            // runtime to write a 3-byte BOM (0xEF 0xBB 0xBF) to stdout when the encoding
            // is first applied. This corrupts scripts generated by print-env-script when
            // output is redirected to a file (e.g., `dotnetup print-env-script > env.sh`).
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }
    }

    /// <summary>
    /// Configures console output behavior for the current invocation: disables Spectre.Console line
    /// wrapping when output is redirected (piped), and routes the "waiting for another dotnetup
    /// process" notice to stderr so piped stdout (e.g. print-env-script) is not corrupted.
    /// </summary>
    private static void ConfigureConsoleOutput()
    {
        if (Console.IsOutputRedirected)
        {
            AnsiConsole.Profile.Width = int.MaxValue;
        }

        ScopedMutex.OnWaitingForMutex = () =>
        {
            Console.Error.WriteLine("Another dotnetup process is running. Waiting for it to finish...");
        };
    }
}
