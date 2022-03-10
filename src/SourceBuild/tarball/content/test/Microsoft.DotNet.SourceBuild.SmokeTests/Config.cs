// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

internal static class Config
{
    public static string DotNetDirectory { get; } = 
        Environment.GetEnvironmentVariable("DOTNET_DIR") ?? Path.Combine(Directory.GetCurrentDirectory(), ".dotnet");
    public static string DotNetTarballPath { get; } = Environment.GetEnvironmentVariable(DotNetTarballPathEnv) ?? string.Empty;
    public const string DotNetTarballPathEnv = "DOTNET_TARBALL_PATH";
    public static bool ExcludeOmniSharpTests { get; } =
        bool.TryParse(Environment.GetEnvironmentVariable("EXCLUDE_OMNISHARP_TESTS"), out bool excludeOmniSharpTests) ? excludeOmniSharpTests : false;
    public static bool ExcludeOnlineTests { get; } =
        bool.TryParse(Environment.GetEnvironmentVariable("EXCLUDE_ONLINE_TESTS"), out bool excludeOnlineTests) ? excludeOnlineTests : false;
    public static string MsftSdkTarballPath { get; } = Environment.GetEnvironmentVariable(MsftSdkTarballPathEnv) ?? string.Empty;
    public const string MsftSdkTarballPathEnv = "MSFT_SDK_TARBALL_PATH";
    public static string TargetRid { get; } = Environment.GetEnvironmentVariable("TARGET_RID") ?? string.Empty;
}
