// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Format;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class FormatForwardingAppTests
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
    public void FormatForwardingUsesVersionedSdkDirectory()
    {
        string sdkDirectory = Path.Combine(Path.GetTempPath(), "dotnet", "sdk", "test-version");
        using var _ = new SdkDirectoryScope(sdkDirectory + Path.DirectorySeparatorChar);

        string dotnetFormatDirectory = Path.Combine(sdkDirectory, "DotnetTools", "dotnet-format");
        string arguments = new FormatForwardingApp(["--help"]).GetProcessStartInfo().Arguments;

        arguments.Should().Contain(Path.Combine(dotnetFormatDirectory, "dotnet-format.deps.json"));
        arguments.Should().Contain(Path.Combine(dotnetFormatDirectory, "dotnet-format.runtimeconfig.json"));
        arguments.Should().Contain(Path.Combine(dotnetFormatDirectory, "dotnet-format.dll"));
        arguments.Should().Contain("--help");
    }
}
