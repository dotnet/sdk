// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[DoNotParallelize]
public class AccessModeResolverTests : IDisposable
{
    private readonly AdminModeTestEnvironment _environment = new();

    public void Dispose() => _environment.Dispose();

    [TestMethod]
    public void Resolve_ReturnsUnset_WhenNoConfig()
    {
        AccessModeResolver.Resolve().Should().Be(EffectiveAccessMode.Unset);
    }

    [TestMethod]
    [DataRow(DotnetAccessMode.None)]
    [DataRow(DotnetAccessMode.Shell)]
    [DataRow(DotnetAccessMode.Everywhere)]
    [DataRow(DotnetAccessMode.Admin)]
    internal void Resolve_ReturnsUserConfigMode(DotnetAccessMode mode)
    {
        if (!DotnetAccessModePolicy.IsSupportedOnCurrentPlatform(mode))
        {
            Assert.Inconclusive($"{mode} is not supported on this platform.");
        }

        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = mode });

        AccessModeResolver.Resolve().Should().Be(new EffectiveAccessMode(mode, AccessModeSource.UserConfig));
    }

    [TestMethod]
    public void Resolve_ReturnsUnsetWithoutWarning_WhenUserConfigIsCorrupt()
    {
        File.WriteAllText(DotnetupPaths.ConfigPath, "{ not json");
        var originalOut = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);
        try
        {
            AccessModeResolver.Resolve().Should().Be(EffectiveAccessMode.Unset);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        output.ToString().Should().BeEmpty();
    }
}
