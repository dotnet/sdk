// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils;

public static class Product
{
    public static string LongName => LocalizableStrings.DotNetSdkInfo;
    public static readonly string Version;
    public static readonly string TargetFrameworkVersion = "11.0";

    static Product()
    {
        DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
        Version = versionFile.BuildNumber
            ?? (Environment.ProcessPath is { } processPath ? FileVersionInfo.GetVersionInfo(processPath).ProductVersion : null)
            ?? string.Empty;
    }
}
