// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands.Format;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
[ResourceLock(nameof(SdkDirectoryScope))]
public class FormatForwardingAppTests
{
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
