// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.WindowsInstaller;

/// <summary>How dotnetup interprets a Windows installer bundle exit code.</summary>
internal enum BundleExitOutcome
{
    Success,
    SuccessRestartRequired,
    Cancelled,
    Busy,
    ElevationRequired,
    Failed,
}

/// <summary>Maps Windows Installer / Burn exit codes to <see cref="BundleExitOutcome"/>.</summary>
internal static class BundleExitCodes
{
    public const int Success = 0;
    public const int ElevationRequired = 740;      // ERROR_ELEVATION_REQUIRED
    public const int Cancelled = 1223;             // ERROR_CANCELLED (e.g. UAC prompt declined)
    public const int UserExit = 1602;              // ERROR_INSTALL_USEREXIT
    public const int InstallInProgress = 1618;     // ERROR_INSTALL_ALREADY_RUNNING
    public const int RebootInitiated = 1641;       // ERROR_SUCCESS_REBOOT_INITIATED
    public const int RebootRequired = 3010;        // ERROR_SUCCESS_REBOOT_REQUIRED

    public static BundleExitOutcome Classify(int exitCode) => exitCode switch
    {
        Success => BundleExitOutcome.Success,
        RebootRequired or RebootInitiated => BundleExitOutcome.SuccessRestartRequired,
        UserExit or Cancelled => BundleExitOutcome.Cancelled,
        InstallInProgress => BundleExitOutcome.Busy,
        ElevationRequired => BundleExitOutcome.ElevationRequired,
        _ => BundleExitOutcome.Failed,
    };
}
