// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using Microsoft.NET.TestFramework;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

internal static class TestSettings
{
    private static readonly object _tmpLock = new();
    private static string? _testArtifactsDir;

    /// <summary>
    /// Gets temporary location for test artifacts.
    /// </summary>
    internal static string TestArtifactsDirectory
    {
        get
        {
            if (_testArtifactsDir == null)
            {
                lock(_tmpLock)
                {
                    if(_testArtifactsDir == null)
                    {
                        string tmpDir = Path.Combine(TestContext.Current.TestExecutionDirectory, "ContainersTests", DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture));
                        if (!Directory.Exists(tmpDir))
                        {
                            Directory.CreateDirectory(tmpDir);
                        }
                        return _testArtifactsDir = tmpDir;
                    }
                }
            }
            return _testArtifactsDir;
        }
    }
}
