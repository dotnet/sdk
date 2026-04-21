// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils;

public static class Product
{
    public static string LongName => LocalizableStrings.DotNetSdkInfo;
    public static readonly string Version;
    public static readonly string TargetFrameworkVersion;

    static Product()
    {
        DotnetVersionFile versionFile = DotnetFiles.VersionFileObject;
        Version = versionFile.BuildNumber ??
                System.Diagnostics.FileVersionInfo.GetVersionInfo(
                        typeof(Product).GetTypeInfo().Assembly.Location)
                    .ProductVersion ??
                string.Empty;

        int firstDotIndex = Version.IndexOf('.');
        if (firstDotIndex >= 0)
        {
            int secondDotIndex = Version.IndexOf('.', firstDotIndex + 1);
            TargetFrameworkVersion = secondDotIndex >= 0
                ? Version.Substring(0, secondDotIndex)
                : Version;
        }
        else
        {
            TargetFrameworkVersion = string.Empty;
        }
    }
}
