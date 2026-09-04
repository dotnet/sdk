// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.IntegrationTests;

internal static class TestSettings
{
    internal const string DockerDaemonResource = "DockerDaemon";
    internal const string MSBuildBuildManagerResource = "MSBuildBuildManager";

    private static readonly object _tmpLock = new();
    private static string? _testArtifactsDir;

    internal static string TestRunId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets temporary location for test artifacts.
    /// </summary>
    internal static string TestArtifactsDirectory
    {
        get
        {
            if (_testArtifactsDir == null)
            {
                lock (_tmpLock)
                {
                    if (_testArtifactsDir == null)
                    {
                        string tmpDir = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, "ContainersTests", TestRunId);
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
