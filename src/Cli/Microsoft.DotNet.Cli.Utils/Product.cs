// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils;

public class Product
{
    public static string LongName => LocalizableStrings.DotNetSdkInfo;
    public static readonly string Version = GetProductVersion();

    [RequiresAssemblyFiles]
    private static string GetProductVersion()
    {
        return GetProductVersion(typeof(Product).Assembly.Location);
    }

    public static string GetProductVersion(string dotnetDllPath)
    {
        DotnetVersionFile versionFile = new(DotnetFiles.GetVersionFilePath(dotnetDllPath));
        return versionFile.BuildNumber ??
                System.Diagnostics.FileVersionInfo.GetVersionInfo(dotnetDllPath)
                    .ProductVersion ??
                string.Empty;
    }
}
