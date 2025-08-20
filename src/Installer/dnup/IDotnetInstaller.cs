// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public interface IDotnetInstaller
{
    GlobalJsonInfo GetGlobalJsonInfo(string initialDirectory);

    string GetDefaultDotnetInstallPath();

    SdkInstallType GetConfiguredInstallType(out string? currentInstallPath);

    string? GetLatestInstalledAdminVersion();
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

    public string? SdkVersion { get; set; }

    public string? AllowPrerelease { get; set; }

    public string? RollForward { get; set; }

    //  The sdk.path specified in the global.json, if any
    public string? SdkPath { get; set; }

}

public interface IReleaseInfoProvider
{
    List<string> GetAvailableChannels();
    string GetLatestVersion(string channel);
}
