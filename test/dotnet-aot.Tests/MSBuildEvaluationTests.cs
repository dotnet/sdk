// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.MSBuild;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class MSBuildEvaluationTests
{
    private readonly struct SdkDirectoryScope : IDisposable
    {
        private readonly object? _previousSdkRoot = AppContext.GetData(SdkPaths.DataName);

        public SdkDirectoryScope(string sdkDirectory)
        {
            AppContext.SetData(SdkPaths.DataName, sdkDirectory);
            SdkPaths.ClearSdkDirectoryCacheForTests();
        }

        public void Dispose()
        {
            AppContext.SetData(SdkPaths.DataName, _previousSdkRoot);
            SdkPaths.ClearSdkDirectoryCacheForTests();
        }
    }

    [TestMethod]
    public void MSBuildForwardingUsesVersionedSdkDirectory()
    {
        string sdkDirectory = Path.Combine(Path.GetTempPath(), "dotnet", "sdk", "test-version");
        using var _ = new SdkDirectoryScope(sdkDirectory + Path.DirectorySeparatorChar);

        var forwardingApp = new MSBuildForwardingAppWithoutLogging(
            MSBuildArgs.FromOtherArgs(),
            forceOutOfProc: true);

        Assert.AreEqual(Path.Combine(sdkDirectory, "MSBuild.dll"), forwardingApp.MSBuildPath);
        Assert.AreEqual(
            Path.Combine(sdkDirectory, "Sdks"),
            forwardingApp.GetProcessStartInfo().Environment["MSBuildSDKsPath"]);
        Assert.AreEqual(
            sdkDirectory,
            forwardingApp.GetProcessStartInfo().Environment["MSBuildExtensionsPath"]);
    }
}
