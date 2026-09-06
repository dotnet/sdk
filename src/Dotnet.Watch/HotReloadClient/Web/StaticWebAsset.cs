// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;

namespace Microsoft.DotNet.HotReload;

internal readonly struct StaticWebAsset(string filePath, string relativeUrl, string assemblyName, bool isApplicationProject)
{
    private static readonly StringComparison OSSpecificPathComparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public string FilePath => filePath;
    public string RelativeUrl => relativeUrl;
    public string AssemblyName => assemblyName;
    public bool IsApplicationProject => isApplicationProject;

    public const string WebRoot = "wwwroot";
    public const string ManifestFileName = "staticwebassets.development.json";

    public static bool IsScopedCssFile(string filePath)
        // The extension is case-insensitive:
        // https://github.com/dotnet/sdk/blob/e62d6a2bd7172fdccd1125bfd5703290ac1b32df/src/StaticWebAssetsSdk/Tasks/DiscoverDefaultScopedCssItems.cs#L36
        => filePath.EndsWith(".razor.css", StringComparison.OrdinalIgnoreCase) ||
           filePath.EndsWith(".cshtml.css", StringComparison.OrdinalIgnoreCase);

    public static string GetScopedCssRelativeUrl(string applicationProjectFilePath, string containingProjectFilePath)
        => WebRoot + "/" + GetScopedCssBundleFileName(applicationProjectFilePath, containingProjectFilePath);

    public static string GetScopedCssBundleFileName(string applicationProjectFilePath, string containingProjectFilePath)
    {
        var sourceProjectName = Path.GetFileNameWithoutExtension(containingProjectFilePath);

        return string.Equals(containingProjectFilePath, applicationProjectFilePath, OSSpecificPathComparison)
            ? $"{sourceProjectName}.styles.css"
            : $"{sourceProjectName}.bundle.scp.css";
    }

    public static bool IsScopedCssBundleFile(string filePath)
        // The extension is case-insensitive:
        // https://github.com/dotnet/sdk/blob/e62d6a2bd7172fdccd1125bfd5703290ac1b32df/src/StaticWebAssetsSdk/Tasks/ScopedCss/ResolveAllScopedCssAssets.cs#L35
        => filePath.EndsWith(".bundle.scp.css", StringComparison.OrdinalIgnoreCase) ||
           filePath.EndsWith(".styles.css", StringComparison.OrdinalIgnoreCase);

    public static bool IsCompressedAssetFile(string filePath)
        // The extension is case-insensitive:
        // https://github.com/dotnet/sdk/blob/e62d6a2bd7172fdccd1125bfd5703290ac1b32df/src/StaticWebAssetsSdk/Tasks/Compression/DiscoverPrecompressedAssets.cs#L87
        => filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
           filePath.EndsWith(".br", StringComparison.OrdinalIgnoreCase);

    public static string? GetRelativeUrl(string applicationProjectFilePath, string containingProjectFilePath, string assetFilePath)
        => IsScopedCssFile(assetFilePath)
        ? GetScopedCssRelativeUrl(applicationProjectFilePath, containingProjectFilePath)
        : GetAppRelativeUrlFomDiskPath(containingProjectFilePath, assetFilePath);

    /// <summary>
    /// For non scoped css, the only static files which apply are the ones under the wwwroot folder in that project. The relative path
    /// will always start with wwwroot. eg:  "wwwroot/css/styles.css"
    /// </summary>
    public static string? GetAppRelativeUrlFomDiskPath(string containingProjectFilePath, string assetFilePath)
    {
        var webRoot = "wwwroot" + Path.DirectorySeparatorChar;
        var webRootDir = Path.Combine(Path.GetDirectoryName(containingProjectFilePath)!, webRoot);

        return assetFilePath.StartsWith(webRootDir, StringComparison.OrdinalIgnoreCase)
            ? assetFilePath.Substring(webRootDir.Length - webRoot.Length).Replace("\\", "/")
            : null;
    }

    /// <summary>
    /// Assets with "_content/" prefix live outside the app's  wwwroot.
    /// ASP.NET Core's convention for static assets contributed by external sources — Razor Class Libraries (RCLs) and NuGet packages.
    /// </summary>
    public static bool IsExternalContentUrl(string url)
        => url.StartsWith("_content/", StringComparison.OrdinalIgnoreCase);
}
