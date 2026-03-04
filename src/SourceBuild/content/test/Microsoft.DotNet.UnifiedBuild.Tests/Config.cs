// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.DotNet.UnifiedBuild.Tests;

public class Config
{
    public static string PortableRid { get; } = GetRuntimeConfig(PortableRidSwitch);
    const string PortableRidSwitch = RuntimeConfigSwitchPrefix + nameof(PortableRid);

    public static string LogsDirectory { get; } = GetRuntimeConfig(LogsDirectorySwitch);
    const string LogsDirectorySwitch = RuntimeConfigSwitchPrefix + nameof(LogsDirectory);

    public static string TargetRid { get; } = GetRuntimeConfig(TargetRidSwitch);
    const string TargetRidSwitch = RuntimeConfigSwitchPrefix + nameof(TargetRid);

    public static string TargetArchitecture { get; } = TargetRid.Split('-')[1];

    public static bool WarnOnContentDiffs { get; } = TryGetRuntimeConfig(WarnOnContentDiffsSwitch, out bool value) ? value : false;
    const string WarnOnContentDiffsSwitch = RuntimeConfigSwitchPrefix + nameof(WarnOnContentDiffs);

    public const string RuntimeConfigSwitchPrefix = "Microsoft.DotNet.UnifiedBuild.Tests.";

    public static string DownloadCacheDirectory { get; } = Path.Combine(Config.LogsDirectory, "Microsoft.DotNet.UnifiedBuild.Tests", "DownloadCache");

    public static string GetRuntimeConfig(string key)
    {
        return TryGetRuntimeConfig(key, out string? value) ? value : throw new InvalidOperationException($"Runtime config setting '{key}' must be specified");
    }

    public static bool TryGetRuntimeConfig(string key, out bool value)
    {
        string? rawValue = (string?)AppContext.GetData(key);
        if (string.IsNullOrEmpty(rawValue))
        {
            value = default!;
            return false;
        }
        value = bool.Parse(rawValue);
        return true;
    }

    public static bool TryGetRuntimeConfig(string key, [NotNullWhen(true)] out string? value)
    {
        value = (string?)AppContext.GetData(key);
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }
        return true;
    }
}
