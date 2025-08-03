// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class TransientTestFolderFixture : IDisposable
{
    public readonly DirectoryInfo TestFolder;

    public TransientTestFolderFixture()
    {
        TestFolder = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        TestFolder.Create();
    }

    public void Dispose()
    {
        try
        {
            if (TestFolder.Exists)
            {
                TestFolder.Delete(recursive: true);
            }
        }
        catch
        {
            // Handle exceptions
        }
    }
}
