// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

//  Install process
// - Resolve version to install from channel
// - Handle writing to install manifest and garbage collection
// - Orchestrate installation so that only one install happens at a time
// - Call into installer implementation


public interface IDotnetInstallManager
{
    GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory);

    string GetDefaultDotnetInstallPath();

    DotnetInstallRootConfiguration? GetConfiguredInstallType();

    string? GetLatestInstalledAdminVersion();

    void InstallSdks(DotnetInstallRoot dotnetRoot, ProgressContext progressContext, IEnumerable<string> sdkVersions);

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
    public string? SdkPath
    {
        get
        {
            return (GlobalJsonContents?.Sdk?.Paths is not null && GlobalJsonContents.Sdk.Paths.Length > 0) ?
                Path.GetFullPath(GlobalJsonContents.Sdk.Paths[0], Path.GetDirectoryName(GlobalJsonPath)!) : null;
        }
    }
}

public record DotnetInstallRootConfiguration(
    DotnetInstallRoot InstallRoot,
    InstallType InstallType,
    bool IsFullyConfigured)
{
    public string Path => InstallRoot.Path;
}
