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
    public const string PrereqsPathEnv = "SMOKE_TESTS_PREREQS_PATH";
    public const string SdkTarballPathEnv = "SMOKE_TESTS_SDK_TARBALL_PATH";
    public const string TargetRidEnv = "SMOKE_TESTS_TARGET_RID";

    public static string DotNetDirectory { get; } =
        Environment.GetEnvironmentVariable(DotNetDirectoryEnv) ?? Path.Combine(Directory.GetCurrentDirectory(), ".dotnet");
    public static string? MsftSdkTarballPath { get; } = Environment.GetEnvironmentVariable(MsftSdkTarballPathEnv);
    public static string? PrereqsPath { get; } = Environment.GetEnvironmentVariable(PrereqsPathEnv);
    public static string? SdkTarballPath { get; } = Environment.GetEnvironmentVariable(SdkTarballPathEnv);
    public static string TargetRid { get; } = Environment.GetEnvironmentVariable(TargetRidEnv) ??
        throw new InvalidOperationException($"'{Config.TargetRidEnv}' must be specified");
}
