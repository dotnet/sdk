// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Microsoft.DotNet.Cli.Utils;

internal static class DotnetFiles
{
    /// <summary>
    /// Get the SDK root folder.
    /// </summary>
    /// <param name="sdkAssemblyPath">The path to any assembly that appears in the SDK root folder.</param>
    private static string GetSdkRootFolder(string sdkAssemblyPath) => Path.Combine(sdkAssemblyPath, "..");

    private static readonly Lazy<DotnetVersionFile> s_versionFileObject = new(
#if NET
        [UnconditionalSuppressMessage("SingleFile", "IL3002", Justification = "All accesses are marked RAF")]
#endif
        () => new DotnetVersionFile(VersionFile));

    public static string GetVersionFilePath(string dotnetDllPath)
    {
        var sdkRootFolder = GetSdkRootFolder(dotnetDllPath);
        return Path.GetFullPath(Path.Combine(sdkRootFolder, ".version"));
    }

    /// <summary>
    /// The SDK ships with a .version file that stores the commit information and SDK version
    /// </summary>
#if NET
    [RequiresAssemblyFiles]
#endif
    public static string VersionFile => GetVersionFilePath(typeof(DotnetFiles).Assembly.Location);

#if NET
    [RequiresAssemblyFiles]
#endif
    internal static DotnetVersionFile VersionFileObject => s_versionFileObject.Value;
}
