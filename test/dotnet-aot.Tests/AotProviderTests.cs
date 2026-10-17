// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.InternalMicrosoft;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class AotProviderTests
{
    [TestMethod]
    public void ProductionProvidersOnlyIncludeCurrentPlatform()
    {
        var names = InternalMicrosoftDetector.CreateProductionProviders().Select(provider => provider.Name).ToArray();
        Assert.AreEqual(OperatingSystem.IsMacOS(), names.Contains("Mac Platform SSO"));
        Assert.AreEqual(OperatingSystem.IsWindows(), names.Contains("Windows workplace join"));
        Assert.AreEqual(OperatingSystem.IsLinux(), names.Contains("WSL Windows gh.exe GitHub org membership"));
        Assert.Contains("gh CLI GitHub org membership", names);
    }
}
