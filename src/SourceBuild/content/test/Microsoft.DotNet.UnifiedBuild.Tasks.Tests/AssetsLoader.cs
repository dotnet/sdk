// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.DotNet.UnifiedBuild.Tasks.Tests;

public static class AssetsLoader
{
    public static string AssetsDirectory => Path.Combine(Directory.GetCurrentDirectory(), "assets");

    public static Stream OpenAssetFile(string assetPath)
    {
        return File.OpenRead(Path.Combine(AssetsDirectory, assetPath));
    }

    public static string GetAssetFullPath(string assetPath)
    {
        return Path.Combine(AssetsDirectory, assetPath);
    }
}
