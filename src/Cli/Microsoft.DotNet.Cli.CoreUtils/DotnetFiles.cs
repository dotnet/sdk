// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils;

public static class DotnetFiles
{
    private static string SdkRootFolder => AppContext.BaseDirectory;

    private static readonly Lazy<DotnetVersionFile> s_versionFileObject =
        new(() => new DotnetVersionFile(VersionFile));

    /// <summary>
    /// The SDK ships with a .version file that stores the commit information and SDK version
    /// </summary>
    public static string VersionFile => Path.GetFullPath(Path.Combine(SdkRootFolder, ".version"));

    public static DotnetVersionFile VersionFileObject => s_versionFileObject.Value;
}

#endif
