// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public interface IBootstrapperController
{
    GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory);

    string GetDefaultDotnetInstallPath();

    InstallType GetConfiguredInstallType(out string? currentInstallPath);

    string? GetLatestInstalledAdminVersion();

    void InstallSdks(string dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions);

    void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null, bool? allowPrerelease = null, string? rollForward = null);

    void ConfigureInstallType(InstallType installType, string? dotnetRoot = null);


}

public class GlobalJsonInfo
{
    public string? GlobalJsonPath { get; set; }
    public GlobalJsonContents? GlobalJsonContents { get; set; }

    // Convenience properties for compatibility
    public string? SdkVersion => GlobalJsonContents?.Sdk?.Version;
    public bool? AllowPrerelease => GlobalJsonContents?.Sdk?.AllowPrerelease;
    public string? RollForward => GlobalJsonContents?.Sdk?.RollForward;
    public string? SdkPath => (GlobalJsonContents?.Sdk?.Paths != null && GlobalJsonContents.Sdk.Paths.Length > 0) ? GlobalJsonContents.Sdk.Paths[0] : null;
}

public interface IReleaseInfoProvider
{
    List<string> GetAvailableChannels();
    string GetLatestVersion(string channel);
}
