// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using static System.IO.Path;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

/// <summary>
/// Helper class to clean up after tests that touch the filesystem.
/// </summary>
internal sealed class TransientTestFolder : IDisposable
{
    public readonly string Path = Combine(TestSettings.TestArtifactsDirectory, GetRandomFileName());
    public readonly DirectoryInfo DirectoryInfo;

    public TransientTestFolder()
    {
        DirectoryInfo = Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
