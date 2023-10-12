// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

internal static class Config
{
    public const string DotNetDirectoryEnv = "SMOKE_TESTS_DOTNET_DIR";
    public const string ExcludeOmniSharpEnv = "SMOKE_TESTS_EXCLUDE_OMNISHARP";
    public const string MsftSdkTarballPathEnv = "SMOKE_TESTS_MSFT_SDK_TARBALL_PATH";
    public const string PoisonReportPathEnv = "SMOKE_TESTS_POISON_REPORT_PATH";
    public const string PortableRidEnv = "SMOKE_TESTS_PORTABLE_RID";
    public const string PrereqsPathEnv = "SMOKE_TESTS_PREREQS_PATH";
    public const string CustomPackagesPathEnv = "SMOKE_TESTS_CUSTOM_PACKAGES_PATH";
    public const string SdkTarballPathEnv = "SMOKE_TESTS_SDK_TARBALL_PATH";
    public const string SourceBuiltArtifactsPathEnv = "SMOKE_TESTS_SOURCEBUILT_ARTIFACTS_PATH";
    public const string TargetRidEnv = "SMOKE_TESTS_TARGET_RID";
    public const string WarnSdkContentDiffsEnv = "SMOKE_TESTS_WARN_SDK_CONTENT_DIFFS";
    public const string WarnLicenseScanDiffsEnv = "SMOKE_TESTS_WARN_LICENSE_SCAN_DIFFS";
    public const string RunningInCIEnv = "SMOKE_TESTS_RUNNING_IN_CI";
    public const string LicenseScanPathEnv = "SMOKE_TESTS_LICENSE_SCAN_PATH";

    public static string DotNetDirectory { get; } =
        Environment.GetEnvironmentVariable(DotNetDirectoryEnv) ?? Path.Combine(Directory.GetCurrentDirectory(), ".dotnet");
    public static string? MsftSdkTarballPath { get; } = Environment.GetEnvironmentVariable(MsftSdkTarballPathEnv);
    public static string? PoisonReportPath { get; } = Environment.GetEnvironmentVariable(PoisonReportPathEnv);
    public static string PortableRid { get; } = Environment.GetEnvironmentVariable(PortableRidEnv) ??
        throw new InvalidOperationException($"'{Config.PortableRidEnv}' must be specified");
    public static string? PrereqsPath { get; } = Environment.GetEnvironmentVariable(PrereqsPathEnv);
    public static string? CustomPackagesPath { get; } = Environment.GetEnvironmentVariable(CustomPackagesPathEnv);
    public static string? SdkTarballPath { get; } = Environment.GetEnvironmentVariable(SdkTarballPathEnv);
    public static string? SourceBuiltArtifactsPath { get; } = Environment.GetEnvironmentVariable(SourceBuiltArtifactsPathEnv);
    public static string TargetRid { get; } = Environment.GetEnvironmentVariable(TargetRidEnv) ??
        throw new InvalidOperationException($"'{Config.TargetRidEnv}' must be specified");
    public static string TargetArchitecture { get; } = TargetRid.Split('-')[1];
    public static bool WarnOnSdkContentDiffs { get; } =
        bool.TryParse(Environment.GetEnvironmentVariable(WarnSdkContentDiffsEnv), out bool warnOnSdkContentDiffs) && warnOnSdkContentDiffs;
    public static bool WarnOnLicenseScanDiffs { get; } =
        bool.TryParse(Environment.GetEnvironmentVariable(WarnLicenseScanDiffsEnv), out bool warnOnLicenseScanDiffs) && warnOnLicenseScanDiffs;

    // Indicates whether the tests are being run in the context of a CI pipeline
    public static bool RunningInCI { get; } =
        bool.TryParse(Environment.GetEnvironmentVariable(RunningInCIEnv), out bool runningInCI) && runningInCI;
    
    public static string? LicenseScanPath { get; } = Environment.GetEnvironmentVariable(LicenseScanPathEnv);
}
