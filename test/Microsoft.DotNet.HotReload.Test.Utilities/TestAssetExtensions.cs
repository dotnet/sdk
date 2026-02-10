// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

internal static class TestAssetExtensions
{
    public static string GetWatchTestOutputPath(this TestAsset asset)
        => Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT") is { } ciOutputRoot
            ? Path.Combine(ciOutputRoot, ".hotreload", asset.Name)
            : asset.Path + ".hotreload";
}
