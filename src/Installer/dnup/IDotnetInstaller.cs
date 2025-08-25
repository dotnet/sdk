// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public interface IDotnetInstaller
{
    GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory);

    string GetDefaultDotnetInstallPath();

    SdkInstallType GetConfiguredInstallType(out string? currentInstallPath);

    string? GetLatestInstalledAdminVersion();

    void InstallSdks(string dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions);

    void UpdateGlobalJson(string globalJsonPath, string? sdkVersion = null, bool? allowPrerelease = null, string? rollForward = null);

    void ConfigureInstallType(SdkInstallType installType, string? dotnetRoot = null);


}

public enum SdkInstallType
{
    None,
    //  Inconsistent would be when the dotnet on the path doesn't match what DOTNET_ROOT is set to
    Inconsistent,
    Admin,
    User
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
