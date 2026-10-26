// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
[ResourceLock(nameof(SdkDirectoryScope))]
public class EnvironmentProviderTests
{
    [TestMethod]
    public void CommandSearchStartsInVersionedSdkDirectory()
    {
        string sdkDirectory = Path.Combine(Path.GetTempPath(), "dotnet", "sdk", $"test-version-{Guid.NewGuid():N}");
        string commandName = $"sdk-command-{Guid.NewGuid():N}";
        string commandPath = Path.Combine(sdkDirectory, commandName);
        Directory.CreateDirectory(sdkDirectory);
        File.WriteAllText(commandPath, string.Empty);

        try
        {
            using var _ = new SdkDirectoryScope(sdkDirectory);

            Assert.AreEqual(
                commandPath,
                new EnvironmentProvider().GetCommandPath(commandName, string.Empty));
        }
        finally
        {
            Directory.Delete(sdkDirectory, recursive: true);
        }
    }
}
