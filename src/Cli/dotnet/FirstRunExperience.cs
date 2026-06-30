// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Hidden.InternalReportInstallSuccess;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.ShellShim;
#if !CLI_AOT
using Microsoft.DotNet.Cli.Utils.Extensions;
#endif
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Cli;

/// <summary>
///  The cross-cutting "first run" experience: first-time-use notice, telemetry opt-out message,
///  ASP.NET Core developer certificate, global-tools PATH registration, and the workload integrity
///  check. This lives in one place so the managed CLI entry point (<see cref="Program"/>) and the
///  NativeAOT bridge (<c>NativeEntryPoint</c>) share one source of truth for first-run setup.
///
///  <para>
///  Both entry points build the <em>same</em> <see cref="DotnetFirstTimeUseConfigurer"/> in the same
///  order, so the two paths are easy to compare for parity. The managed CLI performs the full
///  experience in-process. The NativeAOT binary performs almost all of it in-process too - including
///  ASP.NET Core developer-certificate generation, whose <c>CertificateManager</c> uses only
///  NativeAOT-safe BCL cryptography (the crypto PAL is resolved from the host's already-loaded native
///  library). The one first-run action it cannot perform is workload repair, which needs the NuGet
///  engine. When workload repair is still pending, <see cref="Setup"/> defers the whole invocation to
///  the managed CLI - before writing any sentinel - so the managed CLI performs the complete first-run
///  atomically, exactly once. Everything else (global-tools PATH, the first-time-use notice + telemetry
///  message, the NuGet state migration, and the dev certificate) is AOT-safe and runs in-process on both
///  paths. Subsequent invocations observe the sentinels and run fully in-process on the AOT fast path.
///  </para>
/// </summary>
internal static class FirstRunExperience
{
    private static readonly string s_toolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";

    /// <summary>
    ///  Runs (or, on the AOT path, arranges) the first-run experience for the given parse result.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> when first-run setup is complete and the caller may proceed to execute
    ///  the command in-process; <see langword="false"/> (AOT only) when the invocation must be deferred
    ///  to the managed CLI so it can perform the parts of first-run that are unavailable in NativeAOT.
    ///  The managed CLI always returns <see langword="true"/>.
    /// </returns>
    public static bool Setup(ParseResult parseResult)
    {
        // Options that perform terminating actions are considered to essentially be subcommands.
        // These are special as they should not run the first-run setup.
        // Example: dotnet --version
        if (parseResult.Action is InvocableOptionAction { Terminating: true })
        {
            return true;
        }

        using var activity = Activities.Source.StartActivity("first-time-use");
        IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel = new FirstTimeUseNoticeSentinel();
        IAspNetCertificateSentinel aspNetCertificateSentinel = new AspNetCertificateSentinel();
        string toolPath = Path.Combine(CliFolderPathCalculator.DotnetUserProfileFolderPath, s_toolPathSentinelFileName);
        IFileSentinel toolPathSentinel = new FileSentinel(new FilePath(toolPath));

        var environmentProvider = new EnvironmentProvider();
        bool generateAspNetCertificate = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_GENERATE_ASPNET_CERTIFICATE, defaultValue: true);
        bool addGlobalToolsToPath = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_ADD_GLOBAL_TOOLS_TO_PATH, defaultValue: true);
        bool skipWorkloadIntegrityCheck = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_SKIP_WORKLOAD_INTEGRITY_CHECK,
            // Default the workload integrity check skip to true if the command is being ran in CI. Otherwise, false.
            defaultValue: new CIEnvironmentDetectorForTelemetry().IsCIEnvironment());

        var isDotnetBeingInvokedFromNativeInstaller = false;
        // Note: This should not be special cased like this. Determine if we can skip first run setup entirely for this command.
        if (parseResult.CommandResult.Command is InternalReportInstallSuccessCommandDefinition)
        {
            aspNetCertificateSentinel = new NoOpAspNetCertificateSentinel();
            firstTimeUseNoticeSentinel = new NoOpFirstTimeUseNoticeSentinel();
            toolPathSentinel = new NoOpFileSentinel(exists: false);
            isDotnetBeingInvokedFromNativeInstaller = true;
        }

        string[] getStarOperators = ["getProperty", "getItem", "getTargetResult"];
        char[] switchIndicators = ['-', '/'];
        var skipFirstTimeUseCheck = parseResult.CommandResult.Tokens.Any(t =>
            getStarOperators.Any(o =>
                switchIndicators.Any(i => t.Value.StartsWith(i + o, StringComparison.OrdinalIgnoreCase))));

        var isFirstTimeUse = !firstTimeUseNoticeSentinel.Exists() && !skipFirstTimeUseCheck;

#if CLI_AOT
        // The NativeAOT binary omits only the NuGet engine needed to repair workloads. When workload
        // repair is still pending we defer this entire invocation to the managed CLI - before mutating
        // any state - so it performs the complete first-run atomically, exactly once. The workload repair
        // is detected precisely in-process via WorkloadInstallDetector, so we only defer when workloads
        // are actually installed for this feature band instead of on every first run.
        //
        // Dev-certificate generation IS available in NativeAOT: the ASP.NET Core CertificateManager only
        // uses BCL X509/RSA crypto (no reflection/dynamic code), and the underlying crypto PAL is the
        // host's already-loaded native library, so it runs in-process just like the managed CLI.
        bool workloadRepairPending = isFirstTimeUse
            && !skipWorkloadIntegrityCheck
            && WorkloadInstallDetector.HasInstalledWorkloadsForCurrentBand();
        if (workloadRepairPending)
        {
            return false;
        }
#endif

        IAspNetCoreCertificateGenerator aspnetCertificateGenerator = new AspNetCoreCertificateGenerator();

        bool telemetryOptout = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.TELEMETRY_OPTOUT, defaultValue: CompileOptions.TelemetryOptOutDefault);
        bool nologo = environmentProvider.GetEnvironmentVariableAsBool(EnvironmentVariableNames.DOTNET_NOLOGO, defaultValue: false);

        var dotnetFirstRunConfiguration = new DotnetFirstRunConfiguration(
            generateAspNetCertificate,
            telemetryOptout,
            addGlobalToolsToPath,
            nologo,
            skipWorkloadIntegrityCheck);

        var environmentPath = EnvironmentPathFactory.CreateEnvironmentPath(isDotnetBeingInvokedFromNativeInstaller, environmentProvider);
        var reporter = Reporter.Error;
        var dotnetConfigurer = new DotnetFirstTimeUseConfigurer(
            firstTimeUseNoticeSentinel,
            aspNetCertificateSentinel,
            aspnetCertificateGenerator,
            toolPathSentinel,
            dotnetFirstRunConfiguration,
            reporter,
            environmentPath,
            skipFirstTimeUseCheck);

        dotnetConfigurer.Configure();

#if TARGET_WINDOWS
        if (isDotnetBeingInvokedFromNativeInstaller && OperatingSystem.IsWindows())
        {
            DotDefaultPathCorrector.Correct();
        }
#endif

        if (isFirstTimeUse && !skipWorkloadIntegrityCheck)
        {
#if CLI_AOT
            // Reaching here means WorkloadInstallDetector found no installed workloads for this feature
            // band, so the NuGet-based integrity repair has nothing to do. Any actual repair is handled
            // by the managed CLI, which the AOT bridge falls back to for workload commands.
#else
            try
            {
                WorkloadIntegrityChecker.RunFirstUseCheck(reporter);
            }
            catch (Exception)
            {
                // If the workload check fails for any reason, we want to eat the failure and continue running the command.
                reporter.WriteLine(CliStrings.WorkloadIntegrityCheckError.Yellow());
            }
#endif
        }

        return true;
    }
}
